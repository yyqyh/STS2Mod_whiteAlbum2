using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using STS2_WhiteAlbum2.Core.Combat.Together;
using STS2_WhiteAlbum2.Core.Utils;

using System.Reflection;

namespace STS2_WhiteAlbum2.Core.Patches.Together.Deck;

/// <summary>共享牌堆：重定向、锚点、开局激活、洗牌确定性、进阶之灾去重</summary>
internal static class SharedPilePatches
{
    [HarmonyPatch(typeof(PlayerCombatState), "get_DrawPile")]
    [HarmonyPostfix]
    private static void DrawPileRedirectPostfix(PlayerCombatState __instance, ref CardPile __result)
    {
        SharedPileImpl.Redirect(__instance, PileType.Draw, ref __result);
    }

    [HarmonyPatch(typeof(PlayerCombatState), "get_DiscardPile")]
    [HarmonyPostfix]
    private static void DiscardPileRedirectPostfix(PlayerCombatState __instance, ref CardPile __result)
    {
        SharedPileImpl.Redirect(__instance, PileType.Discard, ref __result);
    }

    [HarmonyPatch(typeof(PlayerCombatState), "get_ExhaustPile")]
    [HarmonyPostfix]
    private static void ExhaustPileRedirectPostfix(PlayerCombatState __instance, ref CardPile __result)
    {
        SharedPileImpl.Redirect(__instance, PileType.Exhaust, ref __result);
    }

    [HarmonyPatch(typeof(PlayerCombatState), "get_PlayPile")]
    [HarmonyPostfix]
    private static void PlayPileRedirectPostfix(PlayerCombatState __instance, ref CardPile __result)
    {
        SharedPileImpl.Redirect(__instance, PileType.Play, ref __result);
    }

    [HarmonyPatch(typeof(Player), "get_Deck")]
    [HarmonyPostfix]
    private static void DeckRedirectPostfix(Player __instance, ref CardPile __result)
    {
        if (!TogetherPair.IsEcho(__instance))
        {
            return;
        }

        if (TogetherPair.Anchor is { } anchor)
        {
            __result = anchor.Deck;
        }
    }

    /// <summary>进战斗时只让锚点填充战斗牌堆。</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.PopulateCombatState))]
    [HarmonyPrefix]
    private static bool AnchorOnlyPrefix(Player __instance)
    {
        LogDeck(__instance);

        // 返回 false = 跳过原方法。回声不填充：主卡组只有一份，只能克隆一次。
        return !TogetherPair.IsEcho(__instance);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.PopulateCombatState))]
    [HarmonyPostfix]
    private static void AnchorOnlyPostfix(Player __instance)
    {
        if (TogetherPair.IsAnchor(__instance))
        {
            var deckIds = string.Join(",", __instance.Deck.Cards.Select(c => c.Id.Entry));
            SelfCheck.Write(
                $"[together][diag] 填充后 netId={__instance.NetId} "
                + $"draw={__instance.PlayerCombatState?.DrawPile.Cards.Count} "
                + $"hand={__instance.PlayerCombatState?.Hand.Cards.Count} "
                + $"共享卡组({__instance.Deck.Cards.Count})=[{deckIds}]");
        }
    }

    /// <summary>临时诊断：把"卡组是否真的共享、里面几张牌"打出来。</summary>
    private static void LogDeck(Player player)
    {
        if (!TogetherPair.IsActive)
        {
            SelfCheck.Write($"[together][diag] PopulateCombatState netId={player.NetId}（本局非共享角色局）");
            return;
        }

        var anchor = TogetherPair.Anchor;
        var deckShared = anchor is not null
                         && TogetherPair.Echoes.All(echo => ReferenceEquals(anchor.Deck, echo.Deck));

        SelfCheck.Write(
            $"[together][diag] PopulateCombatState netId={player.NetId} "
            + $"isAnchor={TogetherPair.IsAnchor(player)} isEcho={TogetherPair.IsEcho(player)} "
            + $"deck={player.Deck.Cards.Count} "
            + $"anchorDeck={anchor?.Deck.Cards.Count} 成员数={TogetherPair.MemberCount} deckShared={deckShared}");
    }

    [HarmonyPatch(typeof(PlayerCombatState), MethodType.Constructor, new[] { typeof(Player) })]
    [HarmonyPostfix]
    private static void CombatStateCreatedPostfix(PlayerCombatState __instance)
    {
        SharedPileImpl.OnCombatStateCreated(__instance);
    }

    /// <summary>新跑局：等 <c>RunState</c> 完全构造完之后再激活共享配对。</summary>
    [HarmonyPatch(typeof(RunState), nameof(RunState.CreateForNewRun))]
    [HarmonyPostfix]
    private static void RunCreatedPostfix(RunState __result)
    {
        TogetherPair.Arm(__result, isNewRun: true);
    }

    /// <summary>读档 / 重连：同样在 <c>RunState</c> 构造完之后激活。</summary>
    [HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
    [HarmonyPostfix]
    private static void RunLoadedPostfix(RunState __result)
    {
        // 读档 / 重连：不能再加血量上限（存档里已经有了）。
        TogetherPair.Arm(__result, isNewRun: false);
    }

    /// <summary>让卡牌之间的排序变成<b>全序</b>，从而让 <c>StableShuffle</c> 真正"与输入顺序无关"。</summary>
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.CompareTo))]
    [HarmonyPostfix]
    private static void DeterministicComparePostfix(CardModel __instance, AbstractModel? other, ref int __result)
    {
        if (__result != 0 || !TogetherPair.IsActive || other is not CardModel otherCard)
        {
            return;
        }

        __result = string.CompareOrdinal(
            DeterministicCardOrder.SortKey(__instance),
            DeterministicCardOrder.SortKey(otherCard));
    }

    /// <summary><c>CardPile.Cards</c> 是只读包装，真正可重排的是这个列表。</summary>
    private static readonly AccessTools.FieldRef<CardPile, List<CardModel>> CardsField =
        AccessTools.FieldRefAccess<CardPile, List<CardModel>>("_cards");

    /// <summary>初始洗牌（<c>CardPile.RandomizeOrderInternal</c>）前先把牌堆排成两端一致的顺序。</summary>
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.RandomizeOrderInternal))]
    [HarmonyPrefix]
    private static void DeterministicShufflePrefix(CardPile __instance, Player player)
    {
        if (!TogetherPair.IsActive || !TogetherPair.IsMember(player))
        {
            return;
        }

        DeterministicCardOrder.Sort(CardsField(__instance));
    }

    private const string BanIdFragment = "ASCENDERS_BANE";

    /// <summary>进阶之灾去重：共享卡组下本体"逐玩家各加一张"的诅咒会变成两张。</summary>
    [HarmonyPatch(typeof(AscensionManager), nameof(AscensionManager.ApplyEffectsTo))]
    [HarmonyPostfix]
    private static void AscensionBanePostfix(Player __0)
    {
        if (!TogetherPair.IsActive || TogetherPair.Anchor is not { } anchor)
        {
            return;
        }

        // 幂等：每次 ApplyEffectsTo 之后都对齐一次（新开局的两次、以及将来重连/加人再触发时都覆盖到）。
        DedupeSharedDeck(anchor);
    }

    /// <summary>把共享卡组里多出来的进阶之灾摘掉（幂等，可以随便多调）。</summary>
    internal static void DedupeSharedDeck(Player anchor)
    {
        var deck = anchor.Deck;
        var before = deck.Cards.Count;
        var extras = new List<CardModel>();
        var kept = false;

        foreach (var card in deck.Cards)
        {
            if (!IsAscendersBane(card))
            {
                continue;
            }

            if (!kept)
            {
                // 第一张留着：进阶之灾本来就是"卡组里有一张"。
                kept = true;
                continue;
            }

            extras.Add(card);
        }

        if (extras.Count == 0)
        {
            return;
        }

        foreach (var extra in extras)
        {
            deck.RemoveInternal(extra, silent: true);
            anchor.RunState.RemoveCard(extra);
        }

        Log.Info(
            $"[together] 进阶之灾去重：共享卡组本来 {before} 张，"
            + $"摘掉 {extras.Count} 张重复诅咒 → {deck.Cards.Count} 张");
    }

    private static bool IsAscendersBane(CardModel card)
    {
        if (card is AscendersBane)
        {
            return true;
        }

        try
        {
            return card.Id.Entry.Contains(BanIdFragment, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>M1：共享牌库的共享逻辑（不含补丁特性）。</summary>
internal static class SharedPileImpl
{
    internal static readonly AccessTools.FieldRef<PlayerCombatState, Player> PlayerOf =
        AccessTools.FieldRefAccess<PlayerCombatState, Player>("_player");

    /// <summary><c>AllPiles</c> 是首次访问即固化的缓存数组，而构造函数里就会访问它。所以锚点换了战斗状态时必须把回声的缓存清掉，让它按新的锚点重建。</summary>
    internal static readonly AccessTools.FieldRef<PlayerCombatState, CardPile[]?> PileCache =
        AccessTools.FieldRefAccess<PlayerCombatState, CardPile[]?>("_piles");

    // 四个战斗牌堆也是 get-only 自动属性：直接换 backing field，理由同 TogetherPair.DeckField。
    // Hand 刻意不在其中——手牌必须保持各自独立。
    private static readonly AccessTools.FieldRef<PlayerCombatState, CardPile> DrawField =
        AccessTools.FieldRefAccess<PlayerCombatState, CardPile>("<DrawPile>k__BackingField");

    private static readonly AccessTools.FieldRef<PlayerCombatState, CardPile> DiscardField =
        AccessTools.FieldRefAccess<PlayerCombatState, CardPile>("<DiscardPile>k__BackingField");

    private static readonly AccessTools.FieldRef<PlayerCombatState, CardPile> ExhaustField =
        AccessTools.FieldRefAccess<PlayerCombatState, CardPile>("<ExhaustPile>k__BackingField");

    private static readonly AccessTools.FieldRef<PlayerCombatState, CardPile> PlayField =
        AccessTools.FieldRefAccess<PlayerCombatState, CardPile>("<PlayPile>k__BackingField");

    /// <summary>把回声的四个战斗牌堆字段换成锚点那一份（Hand 不动）。</summary>
    internal static void AdoptAnchorPiles(PlayerCombatState echoState, PlayerCombatState anchorState)
    {
        DrawField(echoState) = anchorState.DrawPile;
        DiscardField(echoState) = anchorState.DiscardPile;
        ExhaustField(echoState) = anchorState.ExhaustPile;
        PlayField(echoState) = anchorState.PlayPile;

        // 字段换了 → 缓存数组作废，必须清掉让它按新字段重建。
        PileCache(echoState) = null;
    }

    internal static CardPile? PileOf(PlayerCombatState state, PileType type)
    {
        return type switch
        {
            PileType.Draw => state.DrawPile,
            PileType.Discard => state.DiscardPile,
            PileType.Exhaust => state.ExhaustPile,
            PileType.Play => state.PlayPile,
            _ => null,
        };
    }

    internal static void Redirect(PlayerCombatState state, PileType type, ref CardPile result)
    {
        var player = PlayerOf(state);

        if (!TogetherPair.IsEcho(player))
        {
            return;
        }

        var anchorState = ResolveAnchorState(state);
        if (anchorState is null || ReferenceEquals(anchorState, state))
        {
            LogRedirect(type, $"放弃：anchorState={Describe(anchorState)} selfState={state.GetHashCode()}");
            return;
        }

        var original = result;

        if (PileOf(anchorState, type) is { } shared)
        {
            result = shared;
            LogRedirect(type, $"已重定向（{original.Cards.Count} → {shared.Cards.Count} 张）");
        }
    }

    /// <summary>取锚点当前的战斗状态。</summary>
    private static PlayerCombatState? ResolveAnchorState(PlayerCombatState self)
    {
        if (TogetherPair.Anchor?.PlayerCombatState is { } live && !ReferenceEquals(live, self))
        {
            return live;
        }

        var recorded = TogetherPair.AnchorCombatState;
        return recorded is not null && !ReferenceEquals(recorded, self) ? recorded : null;
    }

    private static readonly HashSet<PileType> _loggedRedirects = [];

    /// <summary>临时诊断：每种牌堆只报前 3 次，避免刷屏。</summary>
    private static void LogRedirect(PileType type, string message)
    {
        lock (_loggedRedirects)
        {
            if (_loggedRedirects.Count >= 12 || !_loggedRedirects.Add(type))
            {
                return;
            }
        }

        SelfCheck.Write($"[together][diag] echo {type} 重定向：{message}");
    }

    private static string Describe(PlayerCombatState? state)
    {
        return state is null ? "null" : $"set({state.GetHashCode()})";
    }

    /// <summary>战斗状态重建时的收尾：维护锚点引用、清缓存、拉平身体数值。</summary>
    internal static void OnCombatStateCreated(PlayerCombatState instance)
    {
        var player = PlayerOf(instance);

        if (!TogetherPair.IsActive)
        {
            SelfCheck.Write($"[together][diag] PlayerCombatState 建立 netId={player.NetId}（本局非共享角色局）");
            return;
        }

        if (TogetherPair.IsAnchor(player))
        {
            TogetherPair.SetAnchorCombatState(instance);
            SelfCheck.Write($"[together][diag] PlayerCombatState 建立 netId={player.NetId} 角色=锚点 → AnchorCombatState 已记录");

            // 回声可能先一步被构造（那时还没有锚点的战斗状态可换），这里补做字段替换 + 清缓存。
            foreach (var echo in TogetherPair.Echoes)
            {
                if (echo.PlayerCombatState is { } echoState)
                {
                    AdoptAnchorPiles(echoState, instance);
                }
            }
        }
        else if (TogetherPair.IsEcho(player))
        {
            TogetherPair.SetAnchorCombatState(TogetherPair.Anchor?.PlayerCombatState);
            SelfCheck.Write(
                $"[together][diag] PlayerCombatState 建立 netId={player.NetId} 角色=回声 → "
                + $"AnchorCombatState={Describe(TogetherPair.AnchorCombatState)}");

            if (TogetherPair.Anchor?.PlayerCombatState is { } anchorState)
            {
                AdoptAnchorPiles(instance, anchorState);
            }
        }
        else
        {
            // ★ 3~4 人局里的"其他玩家"走这里。
            // 这里**必须**是 else if / else 两段：早期版本只有 `if (锚点) … else …`，
            // 于是"不是锚点的人"一律被当成回声去做 AdoptAnchorPiles —— 第三个人的
            // 抽牌堆/弃牌堆/消耗堆/出牌堆的 backing field 全被换成了锚点那一份，
            // 表现就是"三人局里所有牌库混在一起"。非配对玩家要完整保持原版行为。
            SelfCheck.Write(
                $"[together][diag] PlayerCombatState 建立 netId={player.NetId}（非配对玩家 → 牌堆保持独立）");
            return;
        }

        // 关键自检：回声的 getter 现在能不能拿到锚点的牌堆。
        if (TogetherPair.Anchor?.PlayerCombatState is { } anchorNow)
        {
            SelfCheck.Write(
                $"[together][diag] 牌堆共享检查 draw={ReferenceEquals(anchorNow.DrawPile, instance.DrawPile)} "
                + $"discard={ReferenceEquals(anchorNow.DiscardPile, instance.DiscardPile)} "
                + $"hand（应为 False）={ReferenceEquals(anchorNow.Hand, instance.Hand)} "
                + $"| anchorDraw={anchorNow.DrawPile.Cards.Count} 本侧 draw={instance.DrawPile.Cards.Count}");
        }

        PileCache(instance) = null;

        // 生命 / 格挡的初始值也要拉平一次（Creature 构造函数是直接写字段、不走 setter 的）。
        BodyMirror.SyncAll();
    }
}

/// <summary>
/// "取牌"这件事的确定性：<b>挂本体自己的公共入口，不认具体是哪张牌</b>。
/// </summary>
/// <remarks>
/// <para>
/// 背景：所有"打出随机牌 / 随机拿牌"的效果都是"读某个牌堆 → 随机（或取堆顶）挑一张"，
/// 结果取决于<b>那个牌堆当下的顺序</b>，而共生体两端的堆顺序会因为本地时序漂掉。
/// 以前的做法是逐张牌挂（横祸一条、破灭一条…），每出一张新牌就得补一条，太脆。
/// </para>
/// <para>
/// 现在按"牌调用的本体 API"分四类覆盖，新牌只要用的是同一批 API 就自动生效：
/// </para>
/// <list type="number">
/// <item><description><b>弃牌堆 / 主卡组</b>：挂 <c>CardPile.AddInternal</c>（以及 MoveToTop/Bottom），
/// 每次变动后保持规范序 → 任何"从这两口堆随机取"的效果（现在和将来的牌、能力、遗物）自动覆盖。
/// 这两口堆的顺序在玩法上没有语义（全库没有任何"取弃牌堆顶 / 卡组第 N 张"的读法），所以可以一直保持规范序。</description></item>
/// <item><description><b>打出抽牌堆</b>：挂 <c>CardPileCmd.AutoPlayFromDrawPile</c> → 破灭 / 倾泻 / 无敌 / 混沌蒸馏药水 / 混乱之力这一族全覆盖。</description></item>
/// <item><description><b>StableShuffle 取牌</b>：靠 <c>DeterministicCardComparePatch</c> 把 <c>CardModel.CompareTo</c> 变成全序
/// （<c>StableShuffle</c> 内部先 <c>List.Sort()</c>）→ 横祸 / 乱战 / 寻者之击 / 能量电池 / 碎石机这一族自动覆盖，不需要单独挂。</description></item>
/// <item><description><b>候选池</b>：挂 <c>CardFactory.GetDistinctForCombat</c> → 攻击药水 / 发现这类"生成随机候选"全覆盖。</description></item>
/// </list>
/// <para>
/// <b>唯一的例外</b>：<c>TakeRandom</c>（内部是 <c>UnstableShuffle</c>，输入顺序敏感）直接读<b>抽牌堆</b>的写法。
/// 抽牌堆不能像弃牌堆那样一直排成规范序——本体"抽牌"就是取 <c>Cards[0]</c>（<c>CardPileCmd.Draw</c>），
/// 排了就变成"永远按固定顺序抽牌"，洗牌就没意义了。全库目前只有受膏（<c>ANOINTED</c>）这么写，
/// 所以只给它单独挂一条（见文件末尾 <c>AnointedOrderPatch</c>）。
/// 以后新增牌如果也是这种写法（<c>TakeRandom</c> + 抽牌堆），grep 一下 <c>TakeRandom(</c> 加一条即可。
/// </para>
/// </remarks>
internal static class RandomPickOrder
{
    /// <summary>
    /// 顺序<b>没有语义</b>的牌堆：可以一直保持规范序。
    /// </summary>
    /// <remarks>
    /// 只放弃牌堆与主卡组。抽牌堆（抽牌取堆顶）、手牌（左右位置有意义）、
    /// 消耗堆 / 打出堆（没人从里面随机取）都不碰。
    /// </remarks>
    private static bool IsOrderFreePile(PileType type)
    {
        return type is PileType.Discard or PileType.Deck;
    }

    /// <summary>牌堆变动后归一顺序（弃牌堆 / 主卡组）。</summary>
    public static void NormalizeOrderFreePile(CardPile? pile, Player? player, string why)
    {
        if (pile is null || player is null)
        {
            return;
        }

        if (!TogetherPair.IsActive || !TogetherPair.IsMember(player))
        {
            return;
        }

        if (!IsOrderFreePile(pile.Type))
        {
            return;
        }

        DeterministicCardOrder.SortPile(pile, why);
    }

    /// <summary>取牌之前把某口堆排成两端一致（只有明确知道"这口堆此刻只被随机读取"时才用）。</summary>
    public static void SortBeforePick(Player? player, PileType type, string why)
    {
        if (player is null || !TogetherPair.IsActive || !TogetherPair.IsMember(player))
        {
            return;
        }

        DeterministicCardOrder.SortPile(type.GetPile(player), why);
    }
}

// ======================================================================================
// 1) 弃牌堆 / 主卡组：只要堆变了就归一顺序（覆盖所有"从这两口堆随机取"的效果）
// ======================================================================================

/// <summary>加牌之后归一顺序（弃牌堆 / 主卡组）。</summary>
[HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
/// <summary>堆变动（加牌 / 移到堆顶 / 移到堆底）之后归一顺序。</summary>
[HarmonyPatch]
internal static class PileOrderNormalizePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(CardPile), nameof(CardPile.AddInternal));
        yield return AccessTools.Method(typeof(CardPile), nameof(CardPile.MoveToTopInternal));
        yield return AccessTools.Method(typeof(CardPile), nameof(CardPile.MoveToBottomInternal));
    }

    [HarmonyPostfix]
    private static void Postfix(CardPile __instance, CardModel card, MethodBase __originalMethod)
    {
        RandomPickOrder.NormalizeOrderFreePile(__instance, card.Owner, __originalMethod.Name);
    }
}

// ======================================================================================
// 2) 打出抽牌堆：破灭 / 倾泻 / 无敌 / 混沌蒸馏药水 / 混乱之力…
// ======================================================================================

/// <summary>
/// 从抽牌堆自动打牌（<c>Top</c> / <c>Bottom</c> / <c>Random</c>）。
/// </summary>
/// <remarks>
/// <c>Top</c> 分支完全不消耗随机数，只按堆顺序取（<c>Cards.FirstOrDefault()</c>），
/// 所以必须先把堆排齐，否则两端各打各的牌。
/// </remarks>
[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.AutoPlayFromDrawPile))]
internal static class AutoPlayFromDrawPileOrderPatch
{
    [HarmonyPrefix]
    private static void Prefix(Player player, int count, CardPilePosition position)
    {
        RandomPickOrder.SortBeforePick(player, PileType.Draw, $"自动打牌/{position}×{count}");
    }
}

// ======================================================================================
// 3) 候选池：攻击药水 / 发现这类"生成随机候选"，TakeRandom 内部是 UnstableShuffle
// ======================================================================================

/// <summary>
/// 战斗中的"生成随机候选牌"（攻击药水、发现一类）。
/// </summary>
/// <remarks>
/// 这里排的是<b>候选池的副本</b>，不动任何牌堆：输入顺序一致，<c>UnstableShuffle</c> 的结果就一致。
/// </remarks>
[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.GetDistinctForCombat))]
internal static class CardFactoryDistinctOrderPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref IEnumerable<CardModel> cards)
    {
        if (!TogetherPair.IsActive)
        {
            return;
        }

        cards = DeterministicCardOrder.SortedCopy(cards);
    }
}

// ======================================================================================
// 4) 诊断：每次"自动打出某张牌"留一行，便于两份 log 对第一个分歧点
// ======================================================================================

/// <summary>每一次"自动打出某张牌"都留一行日志。</summary>
[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.AutoPlay))]
internal static class AutoPlayDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix(CardModel card, Creature? target)
    {
        if (!TogetherPair.IsActive)
        {
            return;
        }

        var drawPile = card.Owner is { } owner ? PileType.Draw.GetPile(owner) : null;

        CappedLog.Info(
            "autoplay",
            $"自动打出：{DeterministicCardOrder.DescribeCards([card], 1)}"
            + $" owner=netId{card.Owner?.NetId}"
            + $" target={(target is null ? "<null>" : target.GetType().Name)}"
            + $" 抽牌堆={DeterministicCardOrder.DescribeCards(drawPile?.Cards)}");
    }
}

// ======================================================================================
// 5) 例外：TakeRandom（= UnstableShuffle）直接读抽牌堆的牌
// ======================================================================================

/// <summary>
/// 受膏（<c>ANOINTED</c>）：<c>TakeRandom</c> 从<b>抽牌堆</b>抽稀有牌进手牌。
/// </summary>
/// <remarks>
/// 抽牌堆不能一直保持规范序（抽牌就是取 <c>Cards[0]</c>），而 <c>TakeRandom</c> 是泛型扩展方法
/// ——.NET 对引用类型实参只生成一份代码，挂上去会连累遗物抓包 / 地图生成（实测直接崩），
/// 所以这一类只能挂在具体牌上。全库目前只有这一张。
/// </remarks>
[HarmonyPatch(typeof(Anointed), "OnPlay")]
internal static class AnointedOrderPatch
{
    [HarmonyPrefix]
    private static void Prefix(Anointed __instance)
    {
        RandomPickOrder.SortBeforePick(__instance.Owner, PileType.Draw, "受膏");
    }
}
