using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

using STS2_WhiteAlbum2.Core.Character;
using STS2_WhiteAlbum2.Core.Settings;
using STS2_WhiteAlbum2.Core.Patches.Together.Deck;

namespace STS2_WhiteAlbum2.Core.Together.Multiplayer;

/// <summary>
/// 共生体的「成员注册表」：谁是锚点、谁是回声。
/// </summary>
/// <remarks>
/// <para>
/// <b>锚点（<see cref="Anchor" />）</b>是权威实例持有者——主卡组与四个战斗牌堆都挂在它身上；
/// <b>回声（<see cref="Echoes" />）</b>是组里其他人，访问入口被重定向到锚点，但手牌与能量保持独立。
/// 人数由设置里的"共生体人数上限"决定（2~4），实际人数 = 选人界面按了「共生体」的人数（≥2 即成组）。
/// </para>
/// <para>
/// <b>为什么不用 <c>LocalContext</c> 之类的本机视角来决定锚点</b>：两端必须算出同一个答案，
/// 否则第一次抽牌就会分叉。<c>RunState.Players</c> 的顺序来自大厅，两端天然一致，也会写进存档。
/// </para>
/// </remarks>
internal static class TogetherPair
{
    /// <summary>成组的最少人数（只有一个人按了按钮 = 不成组，按原版打）。</summary>
    public const int MinMembers = 2;

    /// <summary>成组的最多人数（设置里可填的上限）。</summary>
    public const int MaxMembers = 4;

    private static IRunState? _armedRunState;
    private static Player? _anchor;
    private static readonly List<Player> _echoes = [];
    private static PlayerCombatState? _anchorCombatState;
    private static bool _duelUnbound;

    /// <summary>
    /// 激活配对之前，回声自己的主卡组实例。
    /// </summary>
    /// <remarks>
    /// 解绑（进 PVP 决斗）时要把回声的 <c>Deck</c> backing field 换回它自己那一份，
    /// 否则回声会继续读到锚点的共享卡组，分牌等于白做。
    /// </remarks>
    private static readonly Dictionary<Player, CardPile> OriginalDecks = [];

    /// <summary><c>Player.Piles</c> 是首次访问即固化的缓存数组，需要在激活时清一次。</summary>
    private static readonly AccessTools.FieldRef<Player, CardPile[]?> RunPileCache =
        AccessTools.FieldRefAccess<Player, CardPile[]?>("_runPiles");

    /// <summary>
    /// <c>Player.Deck</c> 是 get-only 自动属性，这里直接拿到它的 backing field。
    /// </summary>
    /// <remarks>
    /// 为什么必须换字段、而不能只改 getter：界面（顶部卡组按钮、卡组界面）会在初始化时
    /// 抓一次 <c>PileType.Deck.GetPile(player)</c> 把 <c>CardPile</c> 引用存进自己的字段里，
    /// 之后只看那个引用。只重定向 getter 的话，这份"旧引用"永远是回声自己的卡组
    /// （实测就是"p2 只显示 9 张初始卡"）。把字段换掉之后，此后任何一次读取——
    /// 无论走不走 getter——拿到的都是锚点那份卡组。
    /// </remarks>
    private static readonly AccessTools.FieldRef<Player, CardPile> DeckField =
        AccessTools.FieldRefAccess<Player, CardPile>("<Deck>k__BackingField");

    /// <summary>本局是否已经成组（锚点 + 至少一个回声）。</summary>
    public static bool IsActive => _anchor is not null && _echoes.Count > 0;

    public static Player? Anchor => _anchor;

    /// <summary>组里除锚点以外的成员（1~3 个）。</summary>
    public static IReadOnlyList<Player> Echoes => _echoes;

    /// <summary>组内总人数（未激活时是 0）。</summary>
    public static int MemberCount => _anchor is null ? 0 : _echoes.Count + 1;

    /// <summary>锚点当前的 <see cref="PlayerCombatState" />；所有回声的四个牌堆都指向它。</summary>
    public static PlayerCombatState? AnchorCombatState => _anchorCombatState;

    /// <summary>
    /// 在<b>跑局构造完成之后</b>激活共生体。调用点在 <c>RunStartPatches</c> 的两个 Postfix 里。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>激活时机是这套设计里最容易踩的坑，必须保持"晚于 RunState 构造"。</b>
    /// <c>RunState.CreateShared</c> 的顺序是"先设 p1 的 RunState，再遍历牌组给每张卡设 owner"：
    /// </para>
    /// <code>
    /// foreach (Player player in players) {
    ///     player.RunState = runState;                    // p1 先拿到 RunState
    ///     foreach (CardModel card in player.Deck.Cards)  // ← 若此时已激活，p2.Deck 就是 p1 的卡组
    ///         runState.AddCard(card, player);            // → 同一张牌被设两次 owner → 抛异常
    /// }
    /// </code>
    /// <para>
    /// 早期版本在 <c>Refresh(player.RunState)</c> 里懒激活，正好命中这一点，
    /// 结果是 <c>InvalidOperationException: Card ... already has an owner</c> → 开局中断 → 黑屏。
    /// 所以现在只在跑局工厂返回之后激活：那一刻所有人的 RunState 与卡组 owner 都已经落定。
    /// </para>
    /// </remarks>
    public static void Arm(IRunState? runState, bool isNewRun = false)
    {
        if (runState is null || runState is NullRunState)
        {
            SelfCheck.Write("[together][diag] Arm：还没有跑局，保持现有配对");
            return;
        }

        if (_duelUnbound)
        {
            if (isNewRun)
            {
                // 新一局开始：上一场决斗的解绑标记只负责本局，不能跨局拦人。
                _duelUnbound = false;
            }
            else
            {
                SelfCheck.Write("[together][diag] Arm：本局已因决斗解绑，跳过读档/重连重新配对");
                return;
            }
        }

        if (!TogetherSettingsSync.EffectiveSymbiosisEnabled)
        {
            if (isNewRun)
            {
                ClearPairingState();
            }

            SelfCheck.Write("[together][diag] Arm：共生体开关已关闭，跳过配对");
            return;
        }

        if (ReferenceEquals(runState, _armedRunState))
        {
            return;
        }

        var players = runState.Players;

        // 成员 = 选了本 mod 两个角色的人。选人界面已经保证 together 局只显示这两个角色，
        // 这里再按 RunState.Players 顺序取一次，保证两端算出的 P1/P2 完全一致。
        var picked = new List<Player>(MaxMembers);
        foreach (var player in players)
        {
            if (AlbumCharacterRules.IsAlbumCharacter(player.Character))
            {
                picked.Add(player);
            }
        }

        if (players.Count != 2
            || picked.Count != 2
            || picked[0].Character.Id == picked[1].Character.Id)
        {
            // 读档/重连的临时 RunState 构造不要在这里清空，否则可能把正在跑的一局拆掉；
            // 但"新开一局"不满足条件时要把上一局的残留状态清干净。
            SelfCheck.Write(
                $"[together][diag] Arm：本局没有共生体（players={players.Count}"
                + $" mod角色={picked.Count}），保持现有配对不变");

            if (isNewRun)
            {
                ClearPairingState();
            }

            return;
        }

        _anchor = picked[0];
        _echoes.Clear();
        _echoes.AddRange(picked.Skip(1));
        _armedRunState = runState;
        _anchorCombatState = null;

        // 解绑时要靠这份记录把回声的卡组换回去；必须在合并/重定向之前抓。
        OriginalDecks.Clear();
        foreach (var echo in _echoes)
        {
            OriginalDecks[echo] = DeckField(echo);
        }

        // 下面这两件事都是"开局一次性"的，**只能在新开一局时做**：
        //
        //  - 合并初始卡组：存档是按 player.Deck 序列化的，而回声的 Deck getter 早就重定向到共享卡组了，
        //    所以存档里每个人的卡组都是同一份；读档后回声手里就有了一副"共享卡组的副本"，
        //    这时再合并一次就是**翻倍**（实测重连一次 51 → 102）。读档/重连必须只用存档里的内容。
        //  - 血量上限提升：上限已经写进存档，再抬一次会越滚越大。
        if (isNewRun)
        {
            if (TogetherSettingsSync.EffectiveMergeStarterDecks)
            {
                foreach (var echo in _echoes)
                {
                    MergeStarterDeckInto(echo, picked[0]);
                }
            }

            ApplyHpBonus(picked[0], _echoes);
        }

        // 把每个回声的主卡组字段直接换成锚点那一份（理由见 DeckField 的注释）。
        foreach (var echo in _echoes)
        {
            DeckField(echo) = picked[0].Deck;
            RunPileCache(echo) = null;
        }

        // 共享卡组是同一份，而进阶的"进阶之灾"是**逐玩家**往卡组里塞的 → 会塞成好几张。
        // 新开局的路径由 AscensionBaneDedupePatch 直接管；这里再对齐一次是为了读档/重连
        // （FromSerializable 那条路不会调 ApplyEffectsTo，旧存档里的重复会被原样带回来）。
        SharedPilePatches.DedupeSharedDeck(picked[0]);

        // 金币共享（可选）：新局把所有人的起始金币加起来（99 × n），读档/重连只做对齐。
        GoldMirror.OnArm(isNewRun);

        Log.Info(
            $"[together] 共生体已激活：anchor={Describe(picked[0])} "
            + $"echoes=[{string.Join(",", _echoes.Select(Describe))}]"
            + $"（大厅共 {players.Count} 人，共享卡组 {picked[0].Deck.Cards.Count} 张）");
    }

    /// <summary>
    /// 进 PVP 决斗前解除共生体：关闭开关、清空成员，并把当前共享卡组按奇偶分成两副。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这是"本局不再自动恢复"的入口：设置里的共生体开关会被写成 false，
    /// 已确定的成员名单也会清空并广播；同时当前运行中的配对引用会被拆掉，
    /// 所有 <c>TogetherPair.IsActive / IsMember</c> 判定立刻回到普通联机局。
    /// </para>
    /// <para>
    /// 分牌口径：把解绑瞬间的共享主卡组按当前顺序列出来（锚点卡组在前、回声卡组在后），
    /// <b>1-based 序号奇数给 P1、偶数给 P2</b>。决斗事件在事件房里触发，不在战斗中，
    /// 所以这里只切主卡组；下一场战斗会按各自的新卡组重建抽/弃/消耗堆。
    /// </para>
    /// </remarks>
    public static bool UnbindForDuel(string reason)
    {
        if (!IsActive)
        {
            Log.Info($"[together] 决斗解绑：当前没有激活的共生体配对（{reason}）");
            return false;
        }

        var netService = RunManager.Instance?.NetService;

        WhiteAlbumSettingStore.Update(settings => settings.SymbiosisEnabled = false);
        SymbiosisMembers.Reset(netService, reason);
        TogetherSettingsSync.PublishHostSettings(netService, reason);
        _duelUnbound = true;

        var memberCount = MemberCount;
        DeactivateAndSplitForDuel();

        Log.Info($"[together] 决斗解绑完成（{reason}）：原有 {memberCount} 人；共生体开关已关闭，成员已清空");
        return true;
    }

    private static void DeactivateAndSplitForDuel()
    {
        var anchor = _anchor;
        var echoes = _echoes.ToList();

        if (anchor is not null && echoes.Count == 1)
        {
            SplitSharedDeckByParity(anchor, echoes[0]);
        }
        else
        {
            RestoreEchoDecks(echoes);

            if (anchor is not null && echoes.Count > 1)
            {
                Log.Warn(
                    $"[together] 决斗解绑：共生体有 {echoes.Count + 1} 人，"
                    + "PVP 只支持两人；已恢复回声卡组但未做奇偶分牌，请带 log 反馈");
            }
        }

        ClearPairingState();
    }

    /// <summary>把当前配对引用全部清掉（不处理卡组拆分；拆分只走决斗解绑那条路）。</summary>
    private static void ClearPairingState()
    {
        _anchor = null;
        _echoes.Clear();
        _anchorCombatState = null;
        _armedRunState = null;
        OriginalDecks.Clear();
    }

    /// <summary>
    /// 把两个人的共享主卡组按 1-based 序号奇偶拆开：奇数张 → 锚点（P1），偶数张 → 回声（P2）。
    /// </summary>
    private static void SplitSharedDeckByParity(Player p1, Player p2)
    {
        var p1Deck = p1.Deck;
        var p2Deck = OriginalDecks.TryGetValue(p2, out var originalDeck) ? originalDeck : DeckField(p2);

        // 先换回回声自己的卡组字段：此后 p2.Deck / 卡组界面读到的才是分给他的那一副。
        DeckField(p2) = p2Deck;
        RunPileCache(p2) = null;

        if (ReferenceEquals(p1Deck, p2Deck))
        {
            Log.Warn("[together] 决斗解绑：P1/P2 卡组是同一实例，无法按奇偶拆分（请带 log 反馈）");
            return;
        }

        var ordered = new List<(CardModel Card, CardPile Source)>();
        var seen = new HashSet<CardModel>(ReferenceEqualityComparer.Instance);

        foreach (var card in p1Deck.Cards.ToList())
        {
            if (seen.Add(card))
            {
                ordered.Add((card, p1Deck));
            }
        }

        foreach (var card in p2Deck.Cards.ToList())
        {
            if (seen.Add(card))
            {
                ordered.Add((card, p2Deck));
            }
        }

        p1Deck.Clear(silent: true);
        p2Deck.Clear(silent: true);

        for (var i = 0; i < ordered.Count; i++)
        {
            var card = ordered[i].Card;
            var toP1 = (i + 1) % 2 == 1;
            var targetPlayer = toP1 ? p1 : p2;
            var targetPile = toP1 ? p1Deck : p2Deck;

            targetPile.AddInternal(card, -1, silent: true);
            card.GiveToAnotherPlayer(targetPlayer);
        }

        Log.Info(
            $"[together] 决斗解绑分牌：共享卡组 {ordered.Count} 张 → "
            + $"P1({Describe(p1)})={p1Deck.Cards.Count} 张（奇数位），"
            + $"P2({Describe(p2)})={p2Deck.Cards.Count} 张（偶数位）");
    }

    private static void RestoreEchoDecks(IEnumerable<Player> echoes)
    {
        foreach (var echo in echoes)
        {
            if (!OriginalDecks.TryGetValue(echo, out var deck))
            {
                continue;
            }

            DeckField(echo) = deck;
            RunPileCache(echo) = null;
        }
    }

    /// <summary>
    /// 把 <paramref name="from" />（回声）的初始卡组并进 <paramref name="to" />（锚点）的卡组：
    /// 共享卡组 = p1 + p2 + …。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 逐张走"从原卡组摘掉 → 放进目标卡组 → 归属改成锚点"：牌还是那些牌，只是换了一副卡组，
    /// 所以<b>不会留下无主的牌</b>。
    /// </para>
    /// <para>必须发生在"回声的 Deck 字段被换成锚点那份"<b>之前</b>，否则读到的就是同一副卡组了。</para>
    /// <para>
    /// 必须直接读 <c>Deck</c> 的<b>字段</b>：此时 <c>_anchor/_echoes</c> 已赋值，
    /// 回声的 <c>Player.Deck</c> getter 会被重定向到锚点，用 getter 读会拿到目标那一副（合并会空转）。
    /// </para>
    /// </remarks>
    private static void MergeStarterDeckInto(Player from, Player to)
    {
        var fromDeck = DeckField(from);
        var toDeck = to.Deck;
        var cards = fromDeck.Cards.ToList();

        if (ReferenceEquals(fromDeck, toDeck))
        {
            Log.Warn("[together] 共生体合并初始卡组：源与目标是同一副卡组，已跳过（不应该发生，请带 log 反馈）");
            return;
        }

        foreach (var card in cards)
        {
            fromDeck.RemoveInternal(card, silent: true);
            toDeck.AddInternal(card, -1, silent: true);
            card.GiveToAnotherPlayer(to);
        }

        Log.Info(
            $"[together] 共生体合并初始卡组：把 {from.Character.GetType().Name} 的 {cards.Count} 张"
            + $"并入共享卡组（合计 {toDeck.Cards.Count} 张）");
    }

    /// <summary>
    /// 共生体血量上限提升：把每个回声最大生命的 <c>HpBonusPercent</c>% 加进共享血池。
    /// </summary>
    /// <remarks>
    /// 上限和当前血一起抬（否则开局不是满血）。回声那边由 <see cref="BodyMirror" /> 对齐。
    /// </remarks>
    private static void ApplyHpBonus(Player anchor, IReadOnlyList<Player> echoes)
    {
        var percent = TogetherSettingsSync.EffectiveHpBonusPercent;
        if (percent <= 0 || echoes.Count == 0)
        {
            return;
        }

        var to = anchor.Creature;
        if (to is null)
        {
            return;
        }

        // 必须在抬锚点之前读：一旦抬上去，镜像会立刻把其他人那份也改成新值。
        var sourceMaxHp = 0;
        foreach (var echo in echoes)
        {
            if (echo.Creature is { } creature)
            {
                sourceMaxHp += creature.MaxHp;
            }
        }

        var bonus = (int)Math.Round(sourceMaxHp * (percent / 100.0), MidpointRounding.AwayFromZero);
        if (bonus <= 0)
        {
            return;
        }

        to.SetMaxHpInternal(to.MaxHp + bonus);
        to.SetCurrentHpInternal(to.CurrentHp + bonus);
        BodyMirror.SyncAll();

        Log.Info(
            $"[together] 共生体血量上限提升：{echoes.Count} 位回声最大生命合计 {sourceMaxHp} 的 {percent}%"
            + $" = +{bonus} → 共享血池 {to.CurrentHp}/{to.MaxHp}");
    }

    public static bool IsAnchor(Player? player)
    {
        return player is not null && ReferenceEquals(player, _anchor);
    }

    /// <summary>组里的"非锚点成员"。牌堆/卡组重定向、共享堆归属等判断都用它。</summary>
    public static bool IsEcho(Player? player)
    {
        if (player is null || _anchor is null)
        {
            return false;
        }

        foreach (var echo in _echoes)
        {
            if (ReferenceEquals(echo, player))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>这位玩家是不是组内成员（含锚点）。</summary>
    public static bool IsMember(Player? player)
    {
        return IsAnchor(player) || IsEcho(player);
    }

    /// <summary>旧名字，等价于 <see cref="IsMember" />。</summary>
    public static bool IsPaired(Player? player)
    {
        return IsMember(player);
    }

    /// <summary>组内所有成员（锚点在前）。</summary>
    public static IEnumerable<Player> Members()
    {
        if (_anchor is null)
        {
            return [];
        }

        return new[] { _anchor }.Concat(_echoes);
    }

    /// <summary>组内<b>除这位以外</b>的其他成员（镜像就是往这些人身上推）。</summary>
    public static IEnumerable<Player> OthersOf(Player? player)
    {
        if (!IsMember(player))
        {
            return [];
        }

        return Members().Where(member => !ReferenceEquals(member, player));
    }

    /// <summary>组内除这位以外的其他 creature。</summary>
    public static IEnumerable<Creature> OtherCreaturesOf(Creature? creature)
    {
        return creature is null ? [] : OthersOf(creature.Player).Select(p => p.Creature);
    }

    /// <summary>
    /// 记录锚点当前的战斗状态。锚点重建 <see cref="PlayerCombatState" /> 时必须调用，
    /// 否则回声会一直指向上一场战斗的牌堆。
    /// </summary>
    public static void SetAnchorCombatState(PlayerCombatState? state)
    {
        _anchorCombatState = state;
    }

    private static string Describe(Player player)
    {
        return $"{player.NetId}({player.Character.GetType().Name})";
    }

}
