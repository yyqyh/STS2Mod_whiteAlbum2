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

using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using STS2_WhiteAlbum2.Core.Combat;

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

/// <summary>
/// 原版事件在共生体下的"两个人同时动同一张牌"问题。
/// </summary>
/// <remarks>
/// 联机时每个玩家各有一份事件实例（<c>EventModel.IsShared=false</c>），而共生体<b>共用一副卡组</b>：
/// P1 还停在"选一张牌附魔"时，P2 可能已经把同一张牌附魔 / 移除了 —— P1 再选它就会撞上
/// <c>Cannot enchant …</c>（附魔不可叠加）或 <c>You cannot remove a card that is not in the deck.</c>，
/// 异常抛在 <c>SetEventFinished</c> 之前 → <b>事件不结束、房间出不去</b>。
/// <para>
/// 对策（默认开启，不改变事件形态）：① 共享卡组一变就按最新状态重建本机选牌界面的候选；
/// ② 在附魔 / 移除 / 变牌入口把已失效的选择跳过而不是抛异常；
/// ③ 选牌界面"建到玩家眼前才算数"（见 <see cref="EventEnchantSelection" />）。
/// 事件本身一律照原版"每人一份、各自选"（<c>IsDeterministic =&gt; !IsShared</c>，翻成共享会少一次校验和）。
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

    /// <summary>锚点 netId（诊断用：说明"共享卡组选牌"会落在哪个窗口）。</summary>
    public static ulong? AnchorNetId => TogetherPair.Anchor?.NetId;

    /// <summary>下一次"只给候选列表"的附魔选牌该由谁做（由调用点自己登记，见 <see cref="PendingSelectorRegisterPatch" />）。</summary>
    private static ulong? _pendingSelectorNetId;

    public static void SetPendingSelector(Player? player)
    {
        _pendingSelectorNetId = player?.NetId;
    }

    public static ulong? TakePendingSelector()
    {
        var netId = _pendingSelectorNetId;
        _pendingSelectorNetId = null;
        return netId;
    }

    /// <summary>按 netId 找共生体成员（找不到返回 null）。</summary>
    public static Player? FindMember(ulong netId)
    {
        if (TogetherPair.Anchor is { } anchor && anchor.NetId == netId)
        {
            return anchor;
        }

        foreach (var echo in TogetherPair.Echoes)
        {
            if (echo.NetId == netId)
            {
                return echo;
            }
        }

        return null;
    }
}

/// <summary>
/// "只给候选列表"的附魔选牌调用点（蓝宝石种子「播种」+ 皇家印章）：登记"这次是谁在选"。
/// </summary>
/// <remarks>
/// 那条重载没有玩家参数，本体只能从 <c>cards[0].Owner</c> 反推选择者，而共享卡组里牌的 owner 两端不一致 → 双向死锁。
/// 这两个调用点手里就有 owner，所以在这里登记，选牌时取用并清空。
/// 以后再遇到同类调用点，往 <see cref="TargetMethods" /> 加一行即可。
/// </remarks>
[HarmonyPatch]
internal static class PendingSelectorRegisterPatch
{
    /// <summary>要挂的目标方法：事件侧（蓝宝石种子「播种」）+ 遗物侧（皇家印章）。</summary>
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(SapphireSeed), "Plant");
        yield return AccessTools.Method(typeof(RoyalStamp), "AfterObtained");
    }

    /// <summary>两个目标方法都是 <c>async</c> 方法，前缀在方法体真正跑起来之前执行，所以先登记后调用。</summary>
    [HarmonyPrefix]
    private static void Prefix(object __instance)
    {
        switch (__instance)
        {
            case EventModel evt:
                EventFlow.SetPendingSelector(evt.Owner);
                CappedLog.Info("event.register", $"登记本次选牌的玩家（事件 {evt.GetType().Name}）→ netId{evt.Owner?.NetId}");
                break;

            case RelicModel relic:
                EventFlow.SetPendingSelector(relic.Owner);
                CappedLog.Info("event.register", $"登记本次选牌的玩家（遗物 {relic.GetType().Name}）→ netId{relic.Owner?.NetId}");
                break;
        }
    }
}

// ======================================================================================
// 1) 本机正在开的"卡组选牌"界面：卡组一变就按最新状态重建候选
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

    private static readonly AccessTools.FieldRef<NDeckEnchantSelectScreen, HashSet<CardModel>> SelectedCardsField =
        AccessTools.FieldRefAccess<NDeckEnchantSelectScreen, HashSet<CardModel>>("_selectedCards");

    public static void Opened(NCardGridSelectionScreen screen)
    {
        _screen = screen;
        _isDeckScreen = IsDeckScreen(screen);

        if (_isDeckScreen)
        {
            var cards = CardsField(screen);
            CappedLog.Info(
                "event.screen",
                $"本机打开了「卡组选牌」界面：候选 {cards?.Count ?? 0} 张，选择者={SelectorOf(screen)?.NetId}"
                + $"（本机 netId={LocalContext.NetId}）");
        }
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

            // 玩家已经在界面上选了牌（正在确认/预览）时不要重建网格，免得把界面搞乱——交给应用前的兜底处理
            if (screen is NDeckEnchantSelectScreen { } enchantScreen
                && SelectedCardsField(enchantScreen) is { Count: > 0 })
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

    /// <summary>本体是按 <c>cards[0].Owner</c> 决定"谁来选"的，这里只把这个值打出来（诊断用）。</summary>
    private static Player? SelectorOf(NCardGridSelectionScreen screen)
    {
        var cards = CardsField(screen);
        return cards is { Count: > 0 } ? cards[0].Owner : null;
    }
}

/// <summary>界面开始等选择时登记 / 被销毁时注销（<c>CardsSelected</c> 是各方共用的入口）。</summary>
[HarmonyPatch]
internal static class DeckSelectionLifecyclePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NCardGridSelectionScreen), nameof(NCardGridSelectionScreen.CardsSelected));
        yield return AccessTools.Method(typeof(NCardGridSelectionScreen), nameof(NCardGridSelectionScreen._ExitTree));
    }

    [HarmonyPostfix]
    private static void Postfix(NCardGridSelectionScreen __instance, MethodBase __originalMethod)
    {
        if (__originalMethod.Name == nameof(NCardGridSelectionScreen.CardsSelected))
        {
            DeckSelectionWatch.Opened(__instance);
        }
    }

    [HarmonyPrefix]
    private static void Prefix(NCardGridSelectionScreen __instance, MethodBase __originalMethod)
    {
        if (__originalMethod.Name == nameof(NCardGridSelectionScreen._ExitTree))
        {
            DeckSelectionWatch.Closed(__instance);
        }
    }
}

/// <summary>共享卡组一变（加牌 / 移除牌）就刷新本机开着的选牌界面。</summary>
[HarmonyPatch]
internal static class DeckChangeRefreshPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(CardPile), nameof(CardPile.AddInternal));
        yield return AccessTools.Method(typeof(CardPile), nameof(CardPile.RemoveInternal));
    }

    [HarmonyPostfix]
    private static void Postfix(CardPile __instance, MethodBase __originalMethod)
    {
        if (__instance.Type == PileType.Deck)
        {
            DeckSelectionWatch.RefreshIfOpen(__originalMethod.Name);
        }
    }
}

// ======================================================================================
// 2) 附魔入口：失效的选择跳过（不抛异常） + 成功附魔后刷新本机选牌界面
// ======================================================================================

/// <summary><c>CardCmd.Enchant</c>：目标牌已不能再附魔就跳过（返回 null，不抛 <c>Cannot enchant …</c>）；改完刷新界面。</summary>
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

    /// <summary>附魔不改牌堆，界面候选得单独刷一次。</summary>
    [HarmonyPostfix]
    private static void Postfix(CardModel card)
    {
        if (card.Pile?.Type == PileType.Deck)
        {
            DeckSelectionWatch.RefreshIfOpen("牌被附魔");
        }
    }
}

/// <summary>
/// 「卡组选牌界面到底建出来没有、为什么玩家看不到」的现场取证。
/// </summary>
/// <remarks>
/// 症状：<c>本机打开了「卡组选牌」界面</c> 打了，玩家屏幕上却什么都没有，过一会儿才突然冒出来。
/// 原因在 <c>NOverlayStack</c> 的可见性：<c>Push()</c> 遇到"栈被盖住"（地图开着 / capstone 在用）会立刻对
/// 新界面调 <c>AfterOverlayHidden()</c> → 节点在树里但 <c>Visible=false</c>，之后被别的 overlay 压上来时同样会隐藏。
/// 另一个和 overlay 无关的坑：窗口没在前台（本地双开时 <c>LocalCoopClone</c> 会把当前不用操作的窗口最小化），
/// 界面正常显示玩家也看不见 —— 所以窗口状态也一起打。
/// 取证点：① 建好那一刻；② 同一帧末；③ 之后每次 overlay 栈变化。只写日志、不改游戏状态，排查完可整块删掉。
/// </remarks>
internal static class SelectionScreenProbe
{
    /// <summary>overlay 栈里的完整列表（<c>NOverlayStack._overlays</c>，只读，用来打"到底压了几层、都是谁"）。</summary>
    private static readonly AccessTools.FieldRef<NOverlayStack, List<IOverlayScreen>> OverlaysField =
        AccessTools.FieldRefAccess<NOverlayStack, List<IOverlayScreen>>("_overlays");

    /// <summary>正在盯着的界面 → 它挂在 overlay 栈 <c>Changed</c> 上的回调（用于注销）。</summary>
    private static readonly Dictionary<NCardGridSelectionScreen, NOverlayStack.ChangedEventHandler> Watchers = [];

    /// <summary>界面建好后立刻调用：登记 + 打第一份状态。</summary>
    public static void Attach(NCardGridSelectionScreen screen, Player selector, int candidateCount)
    {
        CappedLog.Info("event.screen", $"「卡组选牌」界面已建：候选 {candidateCount} 张（选择者=netId{selector.NetId}）");
        Dump("建好那一刻", screen);

        // 同一帧末再打一次：Push 里有好几处"被盖住就先隐藏自己"的分支，帧末的状态才是玩家真正看到的。
        Callable.From(() => Dump("同一帧末", screen)).CallDeferred();
        WatchStack(screen);
    }

    /// <summary>界面关掉后调用：注销监听。</summary>
    public static void Detach(NCardGridSelectionScreen screen)
    {
        try
        {
            if (Watchers.Remove(screen, out var handler) && NOverlayStack.Instance is { } stack)
            {
                stack.Changed -= handler;
            }
        }
        catch (Exception)
        {
            // 注销失败无所谓：节点已经没了，Godot 自己会断掉信号。
        }
    }

    /// <summary>盯着 overlay 栈：只要栈一变（有人被压上来 / 被移除），就把本界面的可见性再打一遍。</summary>
    private static void WatchStack(NCardGridSelectionScreen screen)
    {
        try
        {
            if (NOverlayStack.Instance is not { } stack || Watchers.ContainsKey(screen))
            {
                return;
            }

            NOverlayStack.ChangedEventHandler handler = () => Dump("overlay 栈变化", screen);
            Watchers[screen] = handler;
            stack.Changed += handler;
        }
        catch (Exception ex)
        {
            CappedLog.Info("event.screen", $"监听 overlay 栈失败（忽略）：{ex.Message}");
        }
    }

    /// <summary>把"这张界面此刻到底能不能被看见"摊成一行日志。</summary>
    private static void Dump(string tag, NCardGridSelectionScreen screen)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(screen))
            {
                CappedLog.Info("event.screen", $"[{tag}] 界面节点已失效（已释放）");
                return;
            }

            var stack = NOverlayStack.Instance;
            var isTop = stack is not null && ReferenceEquals(stack.Peek(), screen);
            var windowMode = DisplayServer.WindowGetMode();

            CappedLog.Info(
                "event.screen",
                $"[{tag}] Visible={screen.Visible} 在树里={screen.IsInsideTree()} 是否栈顶={isTop}"
                + $" overlay栈={DescribeStack(stack)}"
                + $" capstone={NCapstoneContainer.Instance?.CurrentCapstoneScreen?.GetType().Name ?? "无"}"
                + $" 地图开着={NMapScreen.Instance?.IsOpen ?? false}"
                + $" 焦点={screen.GetViewport()?.GuiGetFocusOwner()?.Name.ToString() ?? "无"}"
                + $" 窗口={windowMode}/有焦点={DisplayServer.WindowIsFocused()}");
        }
        catch (Exception ex)
        {
            CappedLog.Info("event.screen", $"[{tag}] 打印界面状态失败（忽略）：{ex.Message}");
        }
    }

    private static string DescribeStack(NOverlayStack? stack)
    {
        if (stack is null)
        {
            return "无";
        }

        try
        {
            var overlays = OverlaysField(stack);
            return $"{stack.ScreenCount} 层[自下而上 {string.Join(" → ", overlays.Select(o => o.GetType().Name))}]";
        }
        catch (Exception)
        {
            return $"{stack.ScreenCount} 层";
        }
    }
}

/// <summary>
/// 附魔选牌的界面流程（选择者由 <see cref="EnchantSelectionPatches" /> 定）。
/// </summary>
/// <remarks>
/// 本机负责这次选择时走 <see cref="ShowLocalSelectionAsync" />；否则 <c>WaitForRemoteChoice</c> 等对面，
/// 由对面的 <c>SyncLocalChoice</c> 把结果送回（选择者按 netId 认，两端必然一致）。
/// </remarks>
internal static class EventEnchantSelection
{
    /// <summary>本机这次选牌总共愿意等多久；超了才认输（按空选择收场）。</summary>
    private const long LocalSelectionBudgetMs = 60_000;

    /// <summary>被盖住 / 界面被销毁之后，隔多久重弹一次。</summary>
    private const double RetryDelaySeconds = 0.7;

    /// <summary>本体重写：选择者由调用方指定，不再用 <c>cards[0].Owner</c> 猜。</summary>
    public static async Task<IEnumerable<CardModel>> RunAsync(
        Player selector,
        IReadOnlyList<CardModel> candidates,
        EnchantmentModel enchantment,
        int amount,
        CardSelectorPrefs prefs)
    {
        if (enchantment is null)
        {
            return Array.Empty<CardModel>();
        }

        // 每次调用都重算：共享卡组随时可能被对面改（附魔 / 移除 / 加牌）。
        List<CardModel> Filter()
        {
            return candidates.Where(card => card.Pile?.Type == PileType.Deck && enchantment!.CanEnchant(card)).ToList();
        }

        var cards = Filter();
        if (cards.Count == 0)
        {
            return Array.Empty<CardModel>();
        }

        if (cards.Count <= prefs.MinSelect)
        {
            return cards;
        }

        if (selector.Creature.IsDead)
        {
            return Array.Empty<CardModel>();
        }

        CappedLog.Info(
            "event.selector",
            $"共享卡组选牌（附魔 {enchantment?.Id.Entry}×{amount}）：选择者=netId{selector.NetId}"
            + $"（锚点=netId{EventFlow.AnchorNetId}，本机 netId={LocalContext.NetId}"
            + $"，本机是否弹界面={LocalContext.IsMe(selector)}，候选 {cards.Count} 张）");

        var choiceId = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(selector);

        if (LocalContext.IsMe(selector))
        {
            // 【先建再等】把界面真正建给玩家看，然后等结果。细节见 ShowLocalSelectionAsync。
            var chosen = await ShowLocalSelectionAsync(selector, Filter, enchantment!, amount, prefs);

            RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(
                selector,
                choiceId,
                PlayerChoiceResult.FromMutableDeckCards(chosen));

            return chosen;
        }

        return (await RunManager.Instance.PlayerChoiceSynchronizer.WaitForRemoteChoice(selector, choiceId)).AsDeckCards();
    }

    /// <summary>
    /// 「先建再等」：界面建到玩家眼前才算数 —— 被盖住就等、被销毁就重弹。
    /// </summary>
    /// <remarks>
    /// 本体 <c>NOverlayStack.Push</c> 有一条"栈被盖住就把新界面藏起来"的分支：只要此刻地图开着
    /// （事件结束后 <c>NEventRoom.Proceed()</c> 会打开地图让玩家点下一个节点）或 capstone 在用，
    /// 刚 Push 的界面会被立刻 <c>AfterOverlayHidden()</c> —— 节点在树里但 <c>Visible=false</c>，不会自己恢复；
    /// 而 <c>run.tscn</c> 里地图画在 overlay 之上，硬显示也没用。更糟的是那张"隐形界面"还压在栈里，
    /// 玩家点地图换房间把栈清掉时它会被销毁 → 我们收到 TaskCanceled → 这次附魔直接落空
    /// （实测 14:49 log：<c>Visible=False … 地图开着=True</c> → 4.2 秒后 TaskCanceled → 选了 0 张）。
    /// <para>
    /// 所以：① 被盖住就不弹（每 <see cref="RetryDelaySeconds" /> 秒试一次）；② 弹完复核 <c>Visible</c>，
    /// 仍是 false 就立刻从栈里撤掉（不留隐形界面）；③ 被销毁就用当前共享卡组重算候选重弹。
    /// 兜底：超过 <see cref="LocalSelectionBudgetMs" /> 才放弃（记日志 + 按空选择收场）；重试期间不阻塞游戏。
    /// </para>
    /// </remarks>
    private static async Task<List<CardModel>> ShowLocalSelectionAsync(
        Player selector,
        Func<List<CardModel>> buildCandidates,
        EnchantmentModel enchantment,
        int amount,
        CardSelectorPrefs prefs)
    {
        var deadline = Time.GetTicksMsec() + LocalSelectionBudgetMs;
        var attempt = 0;

        while (true)
        {
            attempt++;

            if (IsStackCovered())
            {
                if (attempt == 1)
                {
                    CappedLog.Info(
                        "event.screen",
                        "地图/capstone 正开着：先等它关掉再弹「卡组选牌」界面"
                        + "（现在弹的话本体会把界面藏起来，玩家看不到）");
                }
            }
            else
            {
                // 每轮都重算候选：共享卡组随时可能被对面改（附魔 / 移除 / 加牌）。
                var valid = buildCandidates();
                if (valid.Count == 0)
                {
                    return [];
                }

                if (valid.Count <= prefs.MinSelect)
                {
                    return valid;
                }

                var list = OrderByDeck(selector, valid);
                var screen = NDeckEnchantSelectScreen.ShowScreen(list, enchantment, amount, prefs);
                SelectionScreenProbe.Attach(screen, selector, list.Count);

                if (screen.Visible)
                {
                    var startedAt = Time.GetTicksMsec();
                    try
                    {
                        var chosen = (await screen.CardsSelected()).ToList();
                        CappedLog.Info(
                            "event.submit",
                            $"「卡组选牌」界面交回结果：选择者=netId{selector.NetId}，选了 {chosen.Count} 张，"
                            + $"界面存活 {Time.GetTicksMsec() - startedAt} ms");
                        return chosen;
                    }
                    catch (TaskCanceledException)
                    {
                        CappedLog.Info(
                            "event.screen",
                            $"[第 {attempt} 次] 界面在选择完成前被销毁（多半是换房间把 overlay 栈清了）→ 稍后重弹一次");
                    }
                    finally
                    {
                        SelectionScreenProbe.Detach(screen);
                    }
                }
                else
                {
                    CappedLog.Info(
                        "event.screen",
                        $"[第 {attempt} 次] 界面 Push 后仍是 Visible=false（被地图/capstone 盖住）→ 立刻撤掉它，稍后重弹");
                    SelectionScreenProbe.Detach(screen);
                    DismissScreen(screen);
                }
            }

            if (Time.GetTicksMsec() > deadline)
            {
                CappedLog.Info(
                    "event.screen",
                    $"「卡组选牌」等了 {LocalSelectionBudgetMs / 1000} 秒还是没能让玩家选（试了 {attempt} 次）"
                    + "→ 本次按空选择收场（遗物/事件的这次附魔会落空）");
                return [];
            }

            await WaitAsync(RetryDelaySeconds);
        }
    }

    /// <summary>现在把界面 Push 进去会不会被本体当场藏起来（地图开着 / capstone 在用）。</summary>
    private static bool IsStackCovered()
    {
        return (NMapScreen.Instance?.IsOpen ?? false) || (NCapstoneContainer.Instance?.InUse ?? false);
    }

    /// <summary>把一张"不该留着"的界面撤掉（本体 <c>Remove</c> 里会 QueueFree，并让下面的界面重新显示）。</summary>
    private static void DismissScreen(NCardGridSelectionScreen screen)
    {
        try
        {
            if (NOverlayStack.Instance is { } stack)
            {
                stack.Remove(screen);
            }
            else if (GodotObject.IsInstanceValid(screen))
            {
                screen.QueueFree();
            }
        }
        catch (Exception ex)
        {
            CappedLog.Info("event.screen", $"撤掉界面失败（忽略）：{ex.Message}");
        }
    }

    /// <summary>按"卡组里的顺序"排候选（两端算出来一致，界面里牌的次序才稳定）。</summary>
    private static List<CardModel> OrderByDeck(Player selector, List<CardModel> cards)
    {
        var deck = PileType.Deck.GetPile(selector).Cards;
        var order = new Dictionary<CardModel, int>();
        for (var i = 0; i < deck.Count; i++)
        {
            order[deck[i]] = i;
        }

        return cards.OrderBy(card => order.TryGetValue(card, out var index) ? index : int.MaxValue).ToList();
    }

    /// <summary>等一小会儿（走场景树的 Timer，不阻塞主线程）。</summary>
    private static async Task WaitAsync(double seconds)
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            return;
        }

        var timer = tree.CreateTimer(seconds);
        await timer.ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
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

// ======================================================================================
// 3) 选择同步的两端埋点：只给一份 log 也能看出"谁在等谁"
// ======================================================================================

/// <summary>
/// 选择同步两端各打一条：<c>event.wait</c>（本机开始等远端）与 <c>event.submit</c>（本机把结果发出去）。
/// </summary>
/// <remarks>
/// 只看到 wait、两边都没有"界面已建"，就是"选择者算不一致"的死锁（修法见 <see cref="EnchantSelectionPatches" />）。
/// </remarks>
[HarmonyPatch]
internal static class ChoiceSyncDiagPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PlayerChoiceSynchronizer), nameof(PlayerChoiceSynchronizer.WaitForRemoteChoice));
        yield return AccessTools.Method(typeof(PlayerChoiceSynchronizer), nameof(PlayerChoiceSynchronizer.SyncLocalChoice));
    }

    [HarmonyPrefix]
    private static void Prefix(Player player, uint choiceId, MethodBase __originalMethod)
    {
        if (!EventFlow.Applies)
        {
            return;
        }

        var waiting = __originalMethod.Name == nameof(PlayerChoiceSynchronizer.WaitForRemoteChoice);
        CappedLog.Info(
            waiting ? "event.wait" : "event.submit",
            $"{(waiting ? "本机开始等待远端选择" : "本机提交选择")}：player=netId{player?.NetId} choiceId={choiceId}"
            + $"（本机 netId={LocalContext.NetId}）");
    }
}

// ======================================================================================
// 5) 附魔选牌入口（三个重载）
// ======================================================================================

/// <summary>
/// 附魔选牌的三个重载统统由我们接管：<b>选择者由调用方指定的玩家决定</b>（本体是拿 <c>cards[0].Owner</c> 猜的）。
/// </summary>
/// <remarks>
/// 本体那条重载里是 <c>Player player = cards[0].Owner;</c>，再由 <c>ShouldSelectLocalCard(player)</c> 决定谁弹界面。
/// 共享卡组里混着两个人的牌，而牌的 <c>Owner</c> 引用两端并不一致（归属归一补丁会在动画时机改它），
/// 于是两台机器各自等对方 → 双向死锁、事件卡住。
/// 修法：带玩家参数的两条用调用方传进来的玩家（两端必然一致）；只给候选列表的那条
/// （<c>SapphireSeed</c>「播种」/ <c>RoyalStamp</c>）用调用点登记的玩家，没登记就退回锚点。
/// </remarks>
[HarmonyPatch]
internal static class EnchantSelectionPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // 4 参：玩家 + 附魔（多数事件 / 遗物走这条）
        yield return AccessTools.Method(
            typeof(CardSelectCmd),
            nameof(CardSelectCmd.FromDeckForEnchantment),
            new[] { typeof(Player), typeof(EnchantmentModel), typeof(int), typeof(CardSelectorPrefs) });

        // 5 参：带额外过滤（自助指南走这条）
        yield return AccessTools.Method(
            typeof(CardSelectCmd),
            nameof(CardSelectCmd.FromDeckForEnchantment),
            new[]
            {
                typeof(Player), typeof(EnchantmentModel), typeof(int),
                typeof(Func<CardModel?, bool>), typeof(CardSelectorPrefs),
            });

        // 只给候选列表（蓝宝石种子「播种」/ 皇家印章）
        yield return AccessTools.Method(
            typeof(CardSelectCmd),
            nameof(CardSelectCmd.FromDeckForEnchantment),
            new[] { typeof(IReadOnlyList<CardModel>), typeof(EnchantmentModel), typeof(int), typeof(CardSelectorPrefs) });
    }

    [HarmonyPrefix]
    private static bool Prefix(object[] __args, ref Task<IEnumerable<CardModel>> __result)
    {
        if (!EventFlow.Applies)
        {
            return true;
        }

        var enchantment = (EnchantmentModel)__args[1];
        var amount = (int)__args[2];
        var prefs = (CardSelectorPrefs)__args[^1];

        if (__args[0] is Player player)
        {
            IReadOnlyList<CardModel> candidates = PileType.Deck.GetPile(player).Cards;

            if (__args.Length == 5 && __args[3] is Func<CardModel?, bool> filter)
            {
                candidates = candidates.Where(card => filter(card)).ToList();
            }

            __result = EventEnchantSelection.RunAsync(player, candidates, enchantment, amount, prefs);
            return false;
        }

        if (__args[0] is not IReadOnlyList<CardModel> cards || cards.Count == 0)
        {
            return true;
        }

        // 选择者：优先用调用点登记的玩家，没有就退回锚点（共享卡组的持有者）——两种都两端一致，不会死锁。
        var pendingNetId = EventFlow.TakePendingSelector();
        var selector = (pendingNetId is { } netId ? EventFlow.FindMember(netId) : null)
            ?? TogetherPair.Anchor
            ?? cards[0].Owner;
        if (selector is null)
        {
            return true;
        }

        CappedLog.Info(
            "event.selector",
            $"共享卡组选牌（附魔 {enchantment.Id.Entry}×{amount}）：调用方只给了候选列表"
            + $"（{(pendingNetId is null ? "没有登记玩家，退回锚点" : $"调用点登记了 netId{pendingNetId}")}）"
            + $"→ 选择者=netId{selector.NetId}"
            + $"（本机 netId={LocalContext.NetId}，本机是否弹界面={LocalContext.IsMe(selector)}，候选 {cards.Count} 张）");

        __result = EventEnchantSelection.RunAsync(selector, cards, enchantment, amount, prefs);
        return false;
    }
}

// ======================================================================================
// 6) 变牌兜底：另一份事件实例先动过的牌，别再硬变（本体在这两处是直接抛异常的）
// ======================================================================================

/// <summary>
/// 变牌前把"已经不能再变"的牌剔掉（本体对每张牌都是<b>直接抛异常</b>的，两份事件实例各动一次手很容易撞上）。
/// </summary>
/// <remarks>
/// 本体 <c>CardCmd.Transform</c>：<c>!IsTransformable</c> → "… is un-transformable."、<c>Pile == null</c> → "… has no pile."，
/// 抛出去事件就中断。这里先过滤；全被过滤就整批跳过，事件照常往下走。
/// </remarks>
[HarmonyPatch(
    typeof(CardCmd),
    nameof(CardCmd.Transform),
    new[] { typeof(IEnumerable<CardTransformation>), typeof(Rng), typeof(CardPreviewStyle) })]
internal static class TransformGuardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        ref IEnumerable<CardTransformation> transformations,
        ref Task<IEnumerable<CardPileAddResult>> __result)
    {
        if (!EventFlow.Applies || transformations is null)
        {
            return true;
        }

        var all = transformations as IList<CardTransformation> ?? transformations.ToList();
        var valid = all
            .Where(t => t.Original is { } card && card.Pile is not null && card.IsTransformable)
            .ToList();

        if (valid.Count == all.Count)
        {
            return true;
        }

        CappedLog.Info(
            "event.conflict",
            $"变牌跳过已失效的牌：{all.Count} → {valid.Count}（另一份事件实例先动过它）");

        if (valid.Count == 0)
        {
            __result = Task.FromResult(Enumerable.Empty<CardPileAddResult>());
            return false;
        }

        transformations = valid;
        return true;
    }
}
