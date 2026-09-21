using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Runs;
using STS2_WhiteAlbum2.Core.Combat.Together;
using STS2_WhiteAlbum2.Core.Settings;
using STS2_WhiteAlbum2.Core.Utils;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace STS2_WhiteAlbum2.Core.Patches.Together.Deck;

/// <summary>卡牌归属与手牌：归属维护、改写时机、异主校验、回手归属、弃抽目标</summary>
internal static class CardOwnershipPatches
{
    /// <summary>单张牌进堆的入口（抽牌走这里）。</summary>
    [HarmonyPatch(
        typeof(CardPileCmd),
        nameof(CardPileCmd.Add),
        new[] { typeof(CardModel), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool) })]
    [HarmonyPrefix]
    private static void OwnerSinglePrefix(CardModel __0, CardPile __1)
    {
        try
        {
            CardOwnershipImpl.NormalizeForHand(__0, __1);
        }
        catch (Exception ex)
        {
            Log.Warn($"[together] owner 交接检查失败：{ex.Message}");
        }
    }

    /// <summary>批量进堆的入口：只在"混合归属"时把 owner 统一到锚点（典型场景是洗牌）。</summary>
    [HarmonyPatch(
        typeof(CardPileCmd),
        nameof(CardPileCmd.Add),
        new[]
        {
            typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition),
            typeof(AbstractModel), typeof(bool), typeof(bool),
        })]
    [HarmonyPrefix]
    private static void OwnerBatchPrefix(IEnumerable<CardModel> __0)
    {
        // 只在参数本身是实体集合时预先枚举（惰性序列不能安全预枚举，原方法还要再枚举一次）。
        if (__0 is not IReadOnlyList<CardModel> cards)
        {
            return;
        }

        try
        {
            // 已停用（时机过早，会造成幽灵卡）：归属改写改在 SharedPileOwnerLateNormalizePatch。
            // CardOwnershipImpl.NormalizeBatchIfMixed(cards);
        }
        catch (Exception ex)
        {
            Log.Warn($"[together] 批量归属规整失败：{ex.Message}");
        }
    }

    /// <summary><c>AfterCardExhausted</c> 派发期间，把卡牌的归属临时还原成"最后持有它的玩家"。</summary>
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardExhausted))]
    /// <summary>参数位置：combatState=0, choiceContext=1, <b>card=2</b>, causedByEthereal=3。</summary>
    [HarmonyPrefix]
    private static void ExhaustedOwnerForHooksPrefix(CardModel __2, ref Player? __state)
    {
        __state = null;

        if (!TogetherPair.IsActive || __2 is null)
        {
            return;
        }

        if (!CardLastHandOwner.TryGet(__2, out var lastOwner) || lastOwner is null)
        {
            return;
        }

        if (ReferenceEquals(__2.Owner, lastOwner))
        {
            return;
        }

        __state = __2.Owner;
        __2.GiveToAnotherPlayer(lastOwner);

        CappedLog.Info(
            "owner.restore",
            $"消耗结算：把 {__2.Id.Entry} 的归属临时还给 netId={lastOwner.NetId}"
            + $"（结算前已按共享牌堆归一为 netId={__state?.NetId}）");
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardExhausted))]
    [HarmonyPostfix]
    private static void ExhaustedOwnerForHooksPostfix(CardModel __2, Player? __state, ref Task __result)
    {
        if (__state is null || __result is null || __2 is null)
        {
            return;
        }

        __result = RestoreAfterAsync(__result, __2, __state);
    }

    private static async Task RestoreAfterAsync(Task task, CardModel card, Player original)
    {
        try
        {
            await task;
        }
        finally
        {
            card.GiveToAnotherPlayer(original);
        }
    }

    /// <summary>手牌归属不变量：<b>在谁手里就归谁</b>。</summary>
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
    [HarmonyPostfix]
    private static void HandInvariantPostfix(CardPile __instance, CardModel __0)
    {
        if (!TogetherPair.IsActive || __instance.Type != PileType.Hand)
        {
            return;
        }

        if (TogetherPair.Anchor?.RunState is not { } runState
            || CardOwnershipImpl.HandOwnerOf(runState, __instance) is not { } handOwner)
        {
            return;
        }

        // 记下"这张牌最后躺在谁的手牌里"：牌离手后会被归一成锚点归属，而遗物/能力
        // 认领"我的牌"时看的正是 card.Owner（例如金纸 JossPaper）。见 CardLastHandOwner。
        CardLastHandOwner.Remember(__0, handOwner);

        if (ReferenceEquals(__0.Owner, handOwner))
        {
            return;
        }

        CappedLog.Info(
            "hand.owner_fixed",
            $"进手牌归属对齐：card={__0.Id.Entry} → netId={handOwner.NetId}");

        __0.GiveToAnotherPlayer(handOwner);
    }

    /// <summary>归属改写的**正确时机**：牌彻底离开手牌、且搬运动画已经播完之后，再把共享堆里的牌统一归锚点。</summary>
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardChangedPiles))]
    /// <summary>位置参数：runState=0, combatState=1, <b>card=2</b>, oldPileType=3, clonedBy=4。</summary>
    [HarmonyPrefix]
    private static void OwnerLateNormalizePrefix(CardModel __2)
    {
        if (!TogetherPair.IsActive || TogetherPair.Anchor is not { } anchor)
        {
            return;
        }

        // 已经搬完、动画也播完了：此刻读它的当前堆是稳定的。
        var pile = __2.Pile;
        if (pile is null)
        {
            return;
        }

        if (pile.Type is not (PileType.Draw or PileType.Discard or PileType.Exhaust))
        {
            return;
        }

        // 只处理共享堆（= 锚点的那一份）。非配对玩家自己的堆原样不动。
        if (!IsSharedPile(pile))
        {
            return;
        }

        if (!ReferenceEquals(__2.Owner, anchor))
        {
            __2.GiveToAnotherPlayer(anchor);
        }
    }

    /// <summary>这一堆是不是共享堆（锚点的 Draw / Discard / Exhaust）。</summary>
    private static bool IsSharedPile(CardPile pile)
    {
        if (TogetherPair.Anchor?.PlayerCombatState is not { } anchorState)
        {
            return false;
        }

        return ReferenceEquals(pile, anchorState.DrawPile)
               || ReferenceEquals(pile, anchorState.DiscardPile)
               || ReferenceEquals(pile, anchorState.ExhaustPile);
    }

    /// <summary>记下"谁从哪个堆里选牌"，供 <see cref="SelectedFromPile" /> 查询。</summary>
    [HarmonyPatch(
        typeof(CardSelectCmd),
        nameof(CardSelectCmd.FromCombatPile),
        new[]
        {
            typeof(PlayerChoiceContext), typeof(CardPile), typeof(Player),
            typeof(CardSelectorPrefs), typeof(Func<CardModel, bool>),
        })]
    [HarmonyPrefix]
    private static void SelectedFromPilePrefix(Player __2, CardPile __1)
    {
        try
        {
            SelectedFromPile.Record(__2, __1);
        }
        catch (Exception)
        {
            // 记录失败最多是"这次不修正"，不该影响原方法。
        }
    }

    /// <summary>单张：<c>Add(card, PileType.Hand)</c>（全息影像、重磅出击等）。</summary>
    [HarmonyPatch(
        typeof(CardPileCmd),
        nameof(CardPileCmd.Add),
        new[] { typeof(CardModel), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool) })]
    [HarmonyPrefix]
    private static void HandReturnSingleTypePrefix(CardModel __0, PileType __1)
    {
        if (__1 != PileType.Hand)
        {
            return;
        }

        try
        {
            HandReturnOwnership.RetargetToActingHand([__0]);
        }
        catch (Exception ex)
        {
            Log.Warn($"[together] 回手归属修正失败：{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>批量：<c>Add(cards, PileType.Hand)</c>（捏奥之怒、挖掘、预言终局等）。</summary>
    [HarmonyPatch(
        typeof(CardPileCmd),
        nameof(CardPileCmd.Add),
        new[]
        {
            typeof(IEnumerable<CardModel>), typeof(PileType), typeof(CardPilePosition),
            typeof(AbstractModel), typeof(bool),
        })]
    [HarmonyPrefix]
    private static void HandReturnBatchTypePrefix(IEnumerable<CardModel> __0, PileType __1)
    {
        if (__1 != PileType.Hand)
        {
            return;
        }

        try
        {
            HandReturnOwnership.RetargetToActingHand(__0 as IReadOnlyList<CardModel> ?? __0.ToList());
        }
        catch (Exception ex)
        {
            Log.Warn($"[together] 回手归属修正失败（批量）：{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>批量 + 明确给出的手牌堆：把牌归到该手牌的主人（补齐单张路径已有的规则）。</summary>
    [HarmonyPatch(
        typeof(CardPileCmd),
        nameof(CardPileCmd.Add),
        new[]
        {
            typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition),
            typeof(AbstractModel), typeof(bool), typeof(bool),
        })]
    [HarmonyPrefix]
    private static void HandReturnBatchPilePrefix(IEnumerable<CardModel> __0, CardPile __1)
    {
        if (__1.Type != PileType.Hand)
        {
            return;
        }

        foreach (var card in __0 as IReadOnlyList<CardModel> ?? __0.ToList())
        {
            try
            {
                HandReturnOwnership.NormalizeIntoHand(card, __1);
            }
            catch (Exception ex)
            {
                Log.Warn($"[together] 手牌归属规整失败：{ex.GetType().Name}: {ex.Message}");
                return;
            }
        }
    }

    /// <summary>进 <c>DiscardAndDraw</c> 时记下"这批牌在谁手里"。</summary>
    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.DiscardAndDraw))]
    [HarmonyPrefix]
    private static void DiscardAndDrawRememberPrefix(IEnumerable<CardModel> __1)
    {
        try
        {
            DiscardDrawTarget.Remember(__1);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[together] 记录弃牌抽牌对象失败：{ex.Message}");
        }
    }

    /// <summary>那次抽牌如果真的落到了另一半头上，就纠正回手牌主人。</summary>
    [HarmonyPatch(
        typeof(CardPileCmd),
        nameof(CardPileCmd.Draw),
        new[] { typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool) })]
    [HarmonyPrefix]
    private static void DrawRedirectToHandOwnerPrefix(ref Player __2, decimal __1)
    {
        try
        {
            if (DiscardDrawTarget.TryRedirect(ref __2, (int)__1))
            {
                CappedLog.Info("draw.redirect", $"抽牌对象修正回手牌主人：netId={__2.NetId}");
            }
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[together] 抽牌对象修正失败：{ex.Message}");
        }
    }
}

/// <summary>M1：卡牌归属（owner）的维护规则。</summary>
internal static class CardOwnershipImpl
{
    /// <summary>进手牌：把牌改成手牌主人。</summary>
    internal static void NormalizeForHand(CardModel card, CardPile hand)
    {
        if (!TogetherPair.IsActive || hand.Type != PileType.Hand)
        {
            return;
        }

        var runState = TogetherPair.Anchor?.RunState;
        if (runState is null)
        {
            return;
        }

        var handOwner = HandOwnerOf(runState, hand);
        if (handOwner is null || ReferenceEquals(card.Owner, handOwner))
        {
            return;
        }

        card.RemoveFromCurrentPile(true);
        card.GiveToAnotherPlayer(handOwner);
    }

    /// <summary><b>只在"混合归属"的批量操作里</b>把 owner 统一到锚点。</summary>
    internal static void NormalizeBatchIfMixed(IReadOnlyList<CardModel> cards)
    {
        // ⚠️ 已停用（2026-09-15）：调用点已经注释掉（见文件末尾 CardOwnerBatchPatch）。
        // 原因：这条"批量入堆前统一归属"和"入堆后统一归属"都属于**时机过早**的改写，
        // 会让 owner 与实际所在手牌脱钩 → 界面漏掉移除手牌节点 → 幽灵卡。
        // 现在归属改写统一交给 SharedPileOwnerLateNormalizePatch（挂在 AfterCardChangedPiles，
        // 即"搬完 + 动画播完"之后）。要恢复本方法，把调用点那行取消注释即可 ——
        // 但请先确认幽灵卡不会因此回归。
        if (!TogetherPair.IsActive || TogetherPair.Anchor is not { } anchor || cards.Count == 0)
        {
            return;
        }

        Player? first = null;
        var mixed = false;

        foreach (var card in cards)
        {
            var owner = OwnerOf(card);
            if (owner is null)
            {
                return;
            }

            if (first is null)
            {
                first = owner;
            }
            else if (!ReferenceEquals(first, owner))
            {
                mixed = true;
                break;
            }
        }

        if (!mixed)
        {
            return;
        }

        foreach (var card in cards)
        {
            if (!ReferenceEquals(OwnerOf(card), anchor))
            {
                card.GiveToAnotherPlayer(anchor);
            }
        }
    }

    private static Player? OwnerOf(CardModel card)
    {
        try
        {
            return card.Owner;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>手牌堆没有被共享，所以"这个 Hand 属于谁"是唯一的。</summary>
    internal static Player? HandOwnerOf(IRunState runState, CardPile pile)
    {
        foreach (var player in runState.Players)
        {
            if (player.PlayerCombatState?.Hand is { } hand && ReferenceEquals(hand, pile))
            {
                return player;
            }
        }

        return null;
    }

}

/// <summary>记住每张牌"最后一次躺在谁的手牌里"。</summary>
internal static class CardLastHandOwner
{
    private static readonly ConditionalWeakTable<CardModel, Player> LastHand = new();

    public static void Remember(CardModel card, Player owner)
    {
        lock (LastHand)
        {
            LastHand.Remove(card);
            LastHand.Add(card, owner);
        }
    }

    public static bool TryGet(CardModel card, out Player? owner)
    {
        return LastHand.TryGetValue(card, out owner);
    }
}

/// <summary>"从共享堆拿牌回手"这一类效果的归属修正。</summary>
internal static class HandReturnOwnership
{
    /// <summary>当前正在结算卡牌/药水效果的那名配对玩家；判不出来时返回 null。</summary>
    public static Player? ActingPairMember()
    {
        if (!TogetherPair.IsActive)
        {
            return null;
        }

        if (TogetherPair.Anchor is not { } anchor || TogetherPair.Echoes.Count == 0)
        {
            return null;
        }

        if (CombatManager.Instance is not { } combat)
        {
            return null;
        }

        // 只有"组里恰好一个人正在结算效果"才敢下判断；多个/零个（嵌套效果或不在出牌期间）都保持原版行为。
        Player? acting = null;
        foreach (var member in TogetherPair.Members())
        {
            if (!combat.IsExecutingCardOrPotionEffect(member))
            {
                continue;
            }

            if (acting is not null)
            {
                return null;
            }

            acting = member;
        }

        if (acting is null)
        {
            return null;
        }

        return acting;
    }

    /// <summary>这批牌要进"当前效果执行者的手牌"→ 先把它们改成那个人，本体随后自己会推出正确的目标堆。</summary>
    /// <returns>是否真的做了改写。</returns>
    public static bool RetargetToActingHand(IReadOnlyList<CardModel> cards)
    {
        if (cards.Count == 0)
        {
            return false;
        }

        // 优先信"谁正在结算效果"；认不出来（能力/遗物在钩子里触发，不在出牌期间）时，
        // 退回"这批牌是不是刚从某个共享堆里被某位配对玩家选出来的"。
        var acting = ActingPairMember() ?? SelectedFromPile.PlayerFor(cards);
        if (acting is null)
        {
            return false;
        }

        if (acting.PlayerCombatState?.Hand is null)
        {
            // 不是战斗期（没有手牌堆）→ 本体自己那句 GetPile 会抛，不关我们的事。
            return false;
        }

        // 先整体判断：只处理"全都还躺在共享堆里"的批次。
        // 只要有一张正在别人手里，就整批不动 —— 那说明这不是"从共享堆回手"这条路径。
        foreach (var card in cards)
        {
            if (PileOf(card) is not { } pile)
            {
                return false;
            }

            if (pile.Type is not (PileType.Draw or PileType.Discard or PileType.Exhaust))
            {
                return false;
            }
        }

        foreach (var card in cards)
        {
            if (ReferenceEquals(card.Owner, acting))
            {
                continue;
            }

            // 顺序照抄本体 CardPileCmd.GiveToAnotherPlayer / CardOwnershipImpl.NormalizeForHand：
            // 必须先把牌从原堆摘出来再改 owner（反过来 owner 变了会按 owner 反查不到旧堆）。
            card.RemoveFromCurrentPile(true);
            card.GiveToAnotherPlayer(acting);
        }

        SelfCheck.Write(
            $"[together][diag] 回手归属修正：{cards.Count} 张 → 手牌主人 netId={acting.NetId} "
            + $"(anchor={TogetherPair.IsAnchor(acting)} echo={TogetherPair.IsEcho(acting)})");

        return true;
    }

    /// <summary>目标堆是明确给出的手牌堆时，把牌改成该手牌的主人。</summary>
    public static void NormalizeIntoHand(CardModel card, CardPile hand)
    {
        if (PileOf(card) is { Type: PileType.Hand })
        {
            return;
        }

        CardOwnershipImpl.NormalizeForHand(card, hand);
    }

    private static CardPile? PileOf(CardModel card)
    {
        try
        {
            return card.Pile;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>记录"最近一次从战斗牌堆里选牌的是谁、选的是哪个堆"。</summary>
internal static class SelectedFromPile
{
    private static Player? _player;

    private static CardPile? _pile;

    public static void Record(Player player, CardPile pile)
    {
        _player = player;
        _pile = pile;
    }

    /// <summary>这批牌确实来自刚才那次选牌的那个堆 → 返回当时的发起者。</summary>
    public static Player? PlayerFor(IReadOnlyList<CardModel> cards)
    {
        if (_player is null || _pile is null || !TogetherPair.IsPaired(_player))
        {
            return null;
        }

        foreach (var card in cards)
        {
            if (!ReferenceEquals(PileOf(card), _pile))
            {
                return null;
            }
        }

        return _player;
    }

    private static CardPile? PileOf(CardModel card)
    {
        try
        {
            return card.Pile;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>"弃掉整手牌、再抽同样数量"（计算下注 / 赌徒之酿 / 赌徒筹码）的抽牌对象修正。</summary>
internal static class DiscardDrawTarget
{
    private const long WindowMs = 5000;

    private static Player? _intended;

    private static int _intendedCount;

    private static long _ticks;

    /// <summary>记下"这批要弃掉的牌原本在谁的手里"。</summary>
    public static void Remember(IEnumerable<CardModel>? cards)
    {
        _intended = null;

        if (!TogetherPair.IsActive || cards is null)
        {
            return;
        }

        var first = cards as IReadOnlyList<CardModel> is { Count: > 0 } list
            ? list[0]
            : cards.FirstOrDefault();

        if (first is null)
        {
            return;
        }

        if (PileOf(first) is not { Type: PileType.Hand } hand)
        {
            return;
        }

        if (TogetherPair.Anchor?.RunState is not { } runState)
        {
            return;
        }

        if (CardOwnershipImpl.HandOwnerOf(runState, hand) is not { } owner)
        {
            return;
        }

        _intended = owner;
        _intendedCount = cards.Count();
        _ticks = System.Environment.TickCount64;
    }

    /// <summary>把"在为另一半抽牌"纠正回手牌主人。</summary>
    /// <param name="drawCount">这次要抽几张。</param>
    public static bool TryRedirect(ref Player player, int drawCount)
    {
        var intended = _intended;
        if (intended is null || System.Environment.TickCount64 - _ticks > WindowMs)
        {
            return false;
        }

        if (drawCount != _intendedCount)
        {
            return false;
        }

        if (ReferenceEquals(intended, player))
        {
            // 本来就是对的（锚点自己打）：消费掉记录，不做改动。
            _intended = null;
            return false;
        }

        // 只有"这次抽牌本来按组里另一位成员算、但实际该给手牌主人"才改写。
        if (ReferenceEquals(player, intended)
            || !TogetherPair.IsMember(player)
            || !TogetherPair.IsMember(intended))
        {
            return false;
        }

        _intended = null;
        player = intended;
        return true;
    }

    private static CardPile? PileOf(CardModel card)
    {
        try
        {
            return card.Pile;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

    /// <summary>方法 1（重做版）：打掉批量 <c>CardPileCmd.Add</c> 里那条"同批 owner 必须一致"的校验，让共享牌堆里的牌保持<b>自然归属</b>。</summary>
[HarmonyPatch]
internal static class DifferentOwnersCheckPatch
{
    /// <summary>错误信息的关键片段。刻意只匹配片段：本体不同位置的措辞不完全一致。</summary>
    private const string MessageFragment = "different owners";

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        foreach (var nested in typeof(CardPileCmd).GetNestedTypes(AccessTools.all))
        {
            if (!nested.Name.Contains("Add", StringComparison.Ordinal))
            {
                continue;
            }

            var moveNext = AccessTools.Method(nested, "MoveNext");
            if (moveNext is not null && ContainsFragment(moveNext))
            {
                return moveNext;
            }
        }

        throw new InvalidOperationException(
            $"在 CardPileCmd 的所有内嵌状态机里都没找到含 \"{MessageFragment}\" 的 MoveNext。" +
            "本体可能改过这条校验（或它不在状态机里），请重新核对后再启用本补丁。");
    }

    /// <summary>粗查：这条方法的 IL 里是否出现目标字符串（用于挑出正确的状态机）。</summary>
    private static bool ContainsFragment(MethodBase method)
    {
        try
        {
            var body = method.GetMethodBody();
            if (body is null)
            {
                return false;
            }

            foreach (var instruction in PatchProcessor.GetCurrentInstructions(method, out _)
                         ?? Enumerable.Empty<CodeInstruction>())
            {
                if (instruction.opcode == OpCodes.Ldstr
                    && instruction.operand is string text
                    && text.Contains(MessageFragment, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // 读不出 IL 就当没找到。
        }

        return false;
    }


    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DifferentOwnersCheckTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var list = instructions.ToList();

        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].opcode != OpCodes.Ldstr
                || list[i].operand is not string text
                || !text.Contains(MessageFragment, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 往后找 newobj（构造异常对象）与其后的 throw，允许中间夹 nop 等填充指令。
            for (var j = i + 1; j < Math.Min(i + 8, list.Count); j++)
            {
                if (list[j].opcode != OpCodes.Newobj)
                {
                    continue;
                }

                // newobj 消耗刚 push 的字符串并留下异常对象；换成 pop 保持栈平衡。
                list[j] = new CodeInstruction(OpCodes.Pop);

                for (var k = j + 1; k < Math.Min(j + 4, list.Count); k++)
                {
                    if (list[k].opcode == OpCodes.Throw)
                    {
                        list[k] = new CodeInstruction(OpCodes.Nop);
                        return list;
                    }
                }
            }

            throw new InvalidOperationException(
                $"在 \"{text}\" 附近没找到 newobj + throw 的常规形状，无法安全改写这段 IL。");
        }

        throw new InvalidOperationException(
            $"MoveNext 里没找到含 \"{MessageFragment}\" 的字符串，无法改写。");
    }
}

/// <summary>
/// 原版事件在共生体下的"两个人同时动同一张牌"问题。
/// </summary>
/// <remarks>
/// <para>
/// <b>问题</b>：联机时每个玩家各有一份事件实例（<c>EventModel.IsShared=false</c>），而共生体<b>共用一副卡组</b>。
/// 于是 P1 还停在"选一张牌附魔"的界面时，P2 可能已经把同一张牌附魔/移除了 —— P1 再选它就会撞上：
/// </para>
/// <list type="bullet">
/// <item><description>附魔：<c>EnchantmentModel.CanEnchant</c> 对"已有附魔的牌"返回 false（Sharp/Nimble/Swift 都不可叠加）
/// → <c>CardCmd.Enchant</c> 抛 <c>Cannot enchant …</c>；</description></item>
/// <item><description>移除：<c>CardPileCmd.RemoveFromDeck</c> 抛 <c>You cannot remove a card that is not in the deck.</c>。</description></item>
/// </list>
/// <para>
/// 异常抛在事件 <c>SetEventFinished(...)</c> 之前 → <b>事件不结束、房间出不去</b>（两端都会抛，所以不是"不同步"，
/// 但是卡死）。
/// </para>
/// <para>
/// <b>这里给的两层保护（默认开启，不影响原版流程）</b>：
/// </para>
/// <list type="number">
/// <item><description><b>刷新界面</b>：共享卡组一变（加牌 / 移除 / 附魔），就把本机正在开的选牌界面按"现在还有效"
/// 的候选重建一次 → 另一边已经改过的牌会从界面里消失，选不到。</description></item>
/// <item><description><b>应用前兜底</b>：万一在刷新落地之前就点了确认，则在 <c>CardCmd.Enchant</c> / <c>CardPileCmd.RemoveFromDeck</c>
/// 入口处把失效的那几张丢掉（跳过而不是抛异常）→ 事件照常走到结束，不卡房。</description></item>
/// </list>
/// <para>
/// 另外提供设置项 <c>ShareEvents</c>（设置页「事件改为共享」）：把原版事件整体换成共享事件（两人投票、只有一次选择），
/// 从根上消除并发；代价是事件奖励变成"整组一份"，并且共享事件结束时不再发校验和。
/// </para>
/// </remarks>
internal static class EventFlow
{
    /// <summary>本局是否管事件（共生体激活 + 联机）。</summary>
    public static bool Applies
    {
        get
        {
            if (!TogetherPair.IsActive)
            {
                return false;
            }

            return RunManager.Instance?.NetService is { } net && net.Type.IsMultiplayer();
        }
    }
}

// ======================================================================================
// 1) 设置项：把原版事件改成共享事件（两人投票）
// ======================================================================================

/// <summary>
/// 打开 <c>ShareEvents</c> 时，把非共享事件当作共享事件处理。
/// </summary>
/// <remarks>
/// <para>
/// 只打基类的 <c>get_IsShared</c>：本体已经有 9 个事件自己覆写成 <c>true</c>（它们走各自的实现，不受影响），
/// 这里只把"默认 false"的那些翻成 true → 变成"两人投票、票高的选项对所有人执行"，即<b>只有一次选择</b>。
/// </para>
/// <para>
/// 注意连带效果：<c>IsDeterministic =&gt; !IsShared</c> 会跟着变 false，该事件结束时不再发校验和（本体对共享事件就是这么设计的）。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(EventModel), "get_IsShared")]
internal static class EventSharePatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        if (__result || !EventFlow.Applies || !TogetherSettingsSync.EffectiveShareEvents)
        {
            return;
        }

        __result = true;
        CappedLog.Info("event.share", "[settings] 事件按共享事件处理（两人投票，只有一次选择）");
    }
}

// ======================================================================================
// 2) 本机正在开的"卡组选牌"界面：卡组一变就按最新状态重建候选
// ======================================================================================

/// <summary>记录本机当前打开的卡组选牌界面，并在共享卡组变化时刷新它。</summary>
internal static class DeckSelectionWatch
{
    private static NCardGridSelectionScreen? _screen;

    /// <summary>本机这个界面是不是"从主卡组选牌"（战斗中"从抽牌堆/弃牌堆选"的界面不能被我们清候选）。</summary>
    private static bool _isDeckScreen;

    private static readonly AccessTools.FieldRef<NCardGridSelectionScreen, IReadOnlyList<CardModel>> CardsField =
        AccessTools.FieldRefAccess<NCardGridSelectionScreen, IReadOnlyList<CardModel>>("_cards");

    private static readonly AccessTools.FieldRef<NCardGridSelectionScreen, NCardGrid> GridField =
        AccessTools.FieldRefAccess<NCardGridSelectionScreen, NCardGrid>("_grid");

    private static readonly AccessTools.FieldRef<NDeckEnchantSelectScreen, EnchantmentModel> EnchantmentField =
        AccessTools.FieldRefAccess<NDeckEnchantSelectScreen, EnchantmentModel>("_enchantment");

    public static void Opened(NCardGridSelectionScreen screen)
    {
        _screen = screen;
        _isDeckScreen = IsDeckScreen(screen);
    }

    public static void Closed(NCardGridSelectionScreen screen)
    {
        if (ReferenceEquals(_screen, screen))
        {
            _screen = null;
            _isDeckScreen = false;
        }
    }

    /// <summary>共享卡组变了：把界面里"已经无效"的候选去掉，重建一次网格。</summary>
    public static void RefreshIfOpen(string why)
    {
        var screen = _screen;
        if (screen is null || !_isDeckScreen || !GodotObject.IsInstanceValid(screen))
        {
            return;
        }

        try
        {
            var cards = CardsField(screen);
            if (cards is null || cards.Count == 0)
            {
                return;
            }

            var valid = cards.Where(StillValid).ToList();
            if (valid.Count == cards.Count)
            {
                return;
            }

            CardsField(screen) = valid;
            GridField(screen)?.SetCards(valid, PileType.None, new List<SortingOrders> { SortingOrders.Ascending });

            CappedLog.Info(
                "event.refresh",
                $"选牌界面已按共享卡组的最新状态刷新（{why}）：候选 {cards.Count} → {valid.Count}");
        }
        catch (Exception ex)
        {
            // 刷新只影响体验，失败绝不能影响原版流程
            CappedLog.Info("event.refresh", $"刷新选牌界面失败（忽略）：{ex.Message}");
        }
    }

    /// <summary>这张候选现在还有效吗（还在卡组里 + 附魔界面的话还满足该附魔的条件）。</summary>
    private static bool StillValid(CardModel card)
    {
        if (card.Pile?.Type != PileType.Deck)
        {
            return false;
        }

        if (_screen is NDeckEnchantSelectScreen enchantScreen
            && EnchantmentField(enchantScreen) is { } enchantment
            && !enchantment.CanEnchant(card))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 开屏那一刻判断：候选是不是都在主卡组里。
    /// </summary>
    /// <remarks>
    /// 只有"从主卡组选牌"的界面（事件附魔 / 商店删牌 / 升级）才该被刷新；
    /// 战斗中"从抽牌堆 / 弃牌堆选牌"的界面候选不在卡组里，一旦被我们按"不在卡组就删"过滤就会整屏空掉。
    /// 开屏时所有候选都是合法的，所以这里判断最准；之后再遇到"牌被移出卡组"也不会误判成非卡组界面。
    /// </remarks>
    private static bool IsDeckScreen(NCardGridSelectionScreen screen)
    {
        try
        {
            var cards = CardsField(screen);
            return cards is { Count: > 0 } && cards.All(card => card.Pile?.Type == PileType.Deck);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>界面开始等待选择时登记（<c>CardsSelected</c> 是各方共用的入口，非虚方法）。</summary>
[HarmonyPatch(typeof(NCardGridSelectionScreen), nameof(NCardGridSelectionScreen.CardsSelected))]
internal static class DeckSelectionOpenedPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCardGridSelectionScreen __instance)
    {
        DeckSelectionWatch.Opened(__instance);
    }
}

/// <summary>界面销毁时注销。</summary>
[HarmonyPatch(typeof(NCardGridSelectionScreen), nameof(NCardGridSelectionScreen._ExitTree))]
internal static class DeckSelectionClosedPatch
{
    [HarmonyPrefix]
    private static void Prefix(NCardGridSelectionScreen __instance)
    {
        DeckSelectionWatch.Closed(__instance);
    }
}

/// <summary>卡组里加牌 → 刷新（对面的"复制一张牌进卡组"之类）。</summary>
[HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
internal static class DeckChangeRefreshOnAddPatch
{
    [HarmonyPostfix]
    private static void Postfix(CardPile __instance)
    {
        if (__instance.Type == PileType.Deck)
        {
            DeckSelectionWatch.RefreshIfOpen("卡组加牌");
        }
    }
}

/// <summary>卡组里移除牌 → 刷新（对面的"删一张牌"）。</summary>
[HarmonyPatch(typeof(CardPile), nameof(CardPile.RemoveInternal))]
internal static class DeckChangeRefreshOnRemovePatch
{
    [HarmonyPostfix]
    private static void Postfix(CardPile __instance)
    {
        if (__instance.Type == PileType.Deck)
        {
            DeckSelectionWatch.RefreshIfOpen("卡组移除牌");
        }
    }
}

/// <summary>附魔不改牌堆，得单独盯一下。</summary>
[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Enchant), new[] { typeof(EnchantmentModel), typeof(CardModel), typeof(decimal) })]
internal static class DeckChangeRefreshOnEnchantPatch
{
    [HarmonyPostfix]
    private static void Postfix(CardModel card)
    {
        if (card.Pile?.Type == PileType.Deck)
        {
            DeckSelectionWatch.RefreshIfOpen("卡组里的牌被附魔");
        }
    }
}

// ======================================================================================
// 3) 应用前兜底：刷新没赶上时，丢掉失效的选择而不是抛异常
// ======================================================================================

/// <summary>附魔入口：目标牌已不能再附魔 → 跳过（返回 null），不再抛 <c>Cannot enchant …</c>。</summary>
[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Enchant), new[] { typeof(EnchantmentModel), typeof(CardModel), typeof(decimal) })]
internal static class EnchantApplyGuardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnchantmentModel enchantment, CardModel card, ref EnchantmentModel? __result)
    {
        if (!EventFlow.Applies)
        {
            return true;
        }

        bool canEnchant;
        try
        {
            canEnchant = enchantment.CanEnchant(card);
        }
        catch (Exception)
        {
            canEnchant = false;
        }

        if (canEnchant)
        {
            return true;
        }

        __result = null;
        CappedLog.Info(
            "event.conflict",
            $"附魔跳过：{DeterministicCardOrder.DescribeCards([card], 1)} ← {enchantment?.Id.Entry}"
            + "（这张牌此刻已经不满足附魔条件，另一个人先动手了；跳过而不是抛异常，避免事件卡住）");
        return false;
    }
}

/// <summary>选牌入口：把"开屏前就已经失效"的候选先剔掉，避免 <c>ArgumentException("All cards must be in the player's deck and enchantable.")</c>。</summary>
[HarmonyPatch(
    typeof(CardSelectCmd),
    nameof(CardSelectCmd.FromDeckForEnchantment),
    new[] { typeof(IReadOnlyList<CardModel>), typeof(EnchantmentModel), typeof(int), typeof(CardSelectorPrefs) })]
internal static class EnchantSelectFilterPatch
{
    [HarmonyPrefix]
    private static void Prefix(EnchantmentModel enchantment, ref IReadOnlyList<CardModel> cards)
    {
        if (!EventFlow.Applies || cards is null || cards.Count == 0)
        {
            return;
        }

        var valid = cards.Where(card => card.Pile?.Type == PileType.Deck && enchantment.CanEnchant(card)).ToList();
        if (valid.Count == cards.Count)
        {
            return;
        }

        CappedLog.Info("event.conflict", $"选牌候选已剔除失效的牌：{cards.Count} → {valid.Count}");
        cards = valid;
    }
}

/// <summary>移除入口：只移除还在卡组里的牌；全都失效就整批跳过。</summary>
[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.RemoveFromDeck), new[] { typeof(IReadOnlyList<CardModel>), typeof(bool) })]
internal static class RemoveFromDeckGuardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref IReadOnlyList<CardModel> cards, ref Task __result)
    {
        if (!EventFlow.Applies || cards is null || cards.Count == 0)
        {
            return true;
        }

        var valid = cards.Where(card => card.Pile?.Type == PileType.Deck).ToList();
        if (valid.Count == cards.Count)
        {
            return true;
        }

        CappedLog.Info("event.conflict", $"从卡组移除：跳过已失效的牌 {cards.Count} → {valid.Count}");

        if (valid.Count == 0)
        {
            __result = Task.CompletedTask;
            return false;
        }

        cards = valid;
        return true;
    }
}
