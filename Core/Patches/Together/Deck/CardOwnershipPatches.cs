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

using STS2_WhiteAlbum2.Core.Combat.Together;
using STS2_WhiteAlbum2.Core.Utils;

using MegaCrit.Sts2.Core.Runs;
using System.Reflection;
using System.Runtime.CompilerServices;

using System.Reflection.Emit;

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
        _ticks = Environment.TickCount64;
    }

    /// <summary>把"在为另一半抽牌"纠正回手牌主人。</summary>
    /// <param name="drawCount">这次要抽几张。</param>
    public static bool TryRedirect(ref Player player, int drawCount)
    {
        var intended = _intended;
        if (intended is null || Environment.TickCount64 - _ticks > WindowMs)
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
