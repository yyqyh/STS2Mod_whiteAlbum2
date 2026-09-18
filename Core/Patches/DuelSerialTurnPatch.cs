using System.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;

namespace STS2_WhiteAlbum2.Core.Pvp;

/// <summary>
/// 读 internal 的 <c>CombatTurnState</c>（类型本身是 internal，只能反射进去）。
/// </summary>
internal static class DuelSerialTurnDiag
{
    private static readonly Type? TurnStateType =
        AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatTurnState");

    private static readonly PropertyInfo? StateProperty =
        TurnStateType is null ? null : AccessTools.Property(TurnStateType, "State");

    private static readonly PropertyInfo? ExtraTurnProperty =
        TurnStateType is null ? null : AccessTools.Property(TurnStateType, "PlayersTakingExtraTurn");

    /// <summary>从回合状态里取 <see cref="ICombatState" />。</summary>
    public static ICombatState? StateOf(object? turnState)
    {
        return turnState is null || StateProperty is null
            ? null
            : StateProperty.GetValue(turnState) as ICombatState;
    }

    /// <summary>
    /// 「这一回合只有这些人参与」的名单 —— 本体给额外回合用的那个列表
    /// （佩尔之眼 <c>PaelsEye</c> 就是靠它实现"这一回合只有我行动"）。
    /// </summary>
    public static List<Player>? ExtraTurnsOf(object? turnState)
    {
        return turnState is null || ExtraTurnProperty is null
            ? null
            : ExtraTurnProperty.GetValue(turnState) as List<Player>;
    }
}

/// <summary>
/// 轮流行动：每个玩家回合开始时，把"这一回合的参与者"限定成当前行动者。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么不再"跳过另一个人的回合开场"</b>：那条路只挡住了 <c>SetupPlayerTurn</c>（抽牌/回能），
/// 却挡不住 <c>Creature.AfterTurnStart</c> —— 后者才是<b>清格挡</b>的地方，
/// 而它遍历的是"这一侧这一回合的参与者"。所以老实现里两个人在每个回合开头都被清一次格挡：
/// 我上了格挡、回合结束、下一个回合还没轮到我，格挡就先没了 → "格挡白上"。
/// </para>
/// <para>
/// <b>本体现在这条</b>：<c>CombatTurnState.PlayersTakingExtraTurn</c>。它在 <c>StartTurn</c> 里决定
/// <c>creaturesStartingTurn</c> / <c>playersStartingTurn</c>，于是：
/// </para>
/// <list type="bullet">
/// <item><description>只有参与者跑 <c>AfterTurnStart</c>（清格挡）和 <c>SetupPlayerTurn</c>（抽牌/回能）；</description></item>
/// <item><description>没参与的玩家会被本体<b>自动置成"已结束回合"</b>（<c>StartTurn</c> 里那段
/// "Setting player ... to ready at start of turn"），不需要我们替他点；</description></item>
/// <item><description>回合结束的钩子（phase one / 弃手牌）也只对参与者跑，另一个人的手牌原样留着；</description></item>
/// <item><description>UI 侧 <c>NPlayerHand</c> / <c>NEndTurnButton</c> 会照这个名单把他判成"本回合不能行动"。</description></item>
/// </list>
/// <para>
/// 一句话：<b>每个回合都由本体按"额外回合"的规格跑一遍</b>，参与者只有当前行动者。
/// 抽牌、能量、格挡清除、回合结束弃牌、各类回合钩子就都落在正确的人身上。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(CombatManager), "StartTurn")]
internal static class DuelActorTurnPatch
{
    [HarmonyPrefix]
    private static void Prefix(object __0)
    {
        if (!DuelConfig.Enabled || !DuelState.InDuel)
        {
            return;
        }

        if (DuelSerialTurnDiag.StateOf(__0) is not { } state)
        {
            return;
        }

        // 敌方回合不碰：决斗里敌方侧没有单位，那一回合本来就是空跑。
        if (state.CurrentSide != CombatSide.Player)
        {
            return;
        }

        if (DuelSerialTurnDiag.ExtraTurnsOf(__0) is not { } participants)
        {
            return;
        }

        // 名单非空 = 本体自己发起的额外回合（佩尔之眼之类）。那是"某人多打一轮"，
        // 不换行动者、不加人，原样放行。
        if (participants.Count > 0)
        {
            Capped.LogOnce("[STS2_WhiteAlbum2] 这一回合是本体的额外回合，原样放行（不换行动者）");
            return;
        }

        DuelTurnOrder.Arm(state);
        DuelTurnOrder.BeginRound(state);

        if (DuelTurnOrder.Actor is not { } actor)
        {
            return;
        }

        participants.Add(actor);

        Log.Info(
            $"[STS2_WhiteAlbum2] 本回合只有 netId={actor.NetId} 行动"
            + "（对手不抽牌、不清格挡、手牌原样留到自己回合）");
    }
}

/// <summary>
/// 回合数：没出手的那位不该被算作过了一回合。
/// </summary>
/// <remarks>
/// <para>
/// 本体在"进入我方侧"时会对所有玩家 <c>IncrementTurnNumber()</c> —— 因为本体每个回合大家都动，
/// 这没有问题。决斗里每回合只有一个人动，照旧全体 +1 的话，第二位玩家的首次回合会变成"第 2 回合"：
/// 回合提示显示错还是小事，更麻烦的是 <c>SetupPlayerTurn</c> 里那段
/// "TurnNumber == 1 时把固有牌 <c>Innate</c> 挪到牌堆顶"就<b>永远不会跑</b>。
/// </para>
/// <para>
/// 所以这里只让"即将行动、且不是第一次行动"的那位 +1，让每个人的 <c>TurnNumber</c> 等于
/// <b>他自己打过的第几个回合</b>。本体的额外回合（某人多打一轮）不受影响，照旧 +1。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.IncrementTurnNumber))]
internal static class DuelTurnNumberPatch
{
    /// <summary>PlayerCombatState 里的私有字段 <c>_player</c>。</summary>
    private static readonly AccessTools.FieldRef<PlayerCombatState, Player> OwnerField =
        AccessTools.FieldRefAccess<PlayerCombatState, Player>("_player");

    [HarmonyPrefix]
    private static bool Prefix(PlayerCombatState __instance)
    {
        if (!DuelConfig.Enabled || !DuelState.InDuel)
        {
            return true;
        }

        var owner = OwnerField(__instance);

        if (owner?.Creature.CombatState is not { } state || state.Players.Count != 2)
        {
            return true;
        }

        // 本体的额外回合：参与者照常 +1。
        if (CombatManager.Instance.PlayersTakingExtraTurn.Contains(owner))
        {
            return true;
        }

        // 普通回合：只给"下一轮要行动、而且已经打过至少一轮"的那位 +1。
        var counts = DuelTurnOrder.IsNextActor(owner) && DuelTurnOrder.HasActedBefore(owner);

        if (!counts)
        {
            Capped.LogOnce("[STS2_WhiteAlbum2] 跳过非行动者的回合数自增（他这一轮没出手）");
        }

        return counts;
    }
}
