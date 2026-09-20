using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2_WhiteAlbum2.Core.Combat.Duel;
using STS2_WhiteAlbum2.Core.Settings;
using System.Reflection;
using System.Threading.Tasks;

namespace STS2_WhiteAlbum2.Core.Patches.PvpEvent;

/// <summary>决斗回合：轮转，以及站在敌方侧的玩家的回合接管与守卫</summary>
internal static class DuelTurnPatches
{
    /// <summary>
    /// 兜底：决斗里"站在敌方侧的玩家"不能走怪物 AI。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本体的敌方回合（<c>CombatManager.ExecuteEnemyTurn</c>）会对每个敌方单位做两件事：
    /// <c>NCreature.PerformIntent()</c>（玩家没有意图）和 <c>Creature.TakeTurn()</c>。
    /// 而 <c>TakeTurn</c> 的第一句就是：
    /// </para>
    /// <code>
    /// if (!IsMonster || Side != CombatSide.Enemy)
    ///     throw new InvalidOperationException("Only enemy monsters can take automated turns.");
    /// </code>
    /// <para>
    /// 对手被放到敌方侧之后正好会命中这一句 → <b>异常会打死整个敌方回合</b>
    /// （表现是战斗卡在"敌方回合"不动）。这两个 Prefix 就是让玩家单位安全地跳过 AI 环节。
    /// </para>
    /// <para>
    /// 注意：这只是"不崩"。让对手真正能行动（走玩家操作流程）是下一步的事 ——
    /// 本体没有"两个玩家轮流"的原生支持，那一步要接管整个敌方回合。
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(Creature), nameof(Creature.TakeTurn))]
    [HarmonyPrefix]
    private static bool SkipPlayerTakeTurnPrefix(Creature __instance, ref Task __result)
    {
        // 只针对"站在敌方侧的玩家"——F 方案下两个人都在我方侧，这些分支不会再命中，
        // 留着是为了兼容"对手在敌方侧"的老布置；加上阵营判断，避免我方玩家的正常流程也被跳过。
        if (!WhiteAlbumSetting.DuelEnabled || !DuelState.InDuel || !__instance.IsPlayer || __instance.Side != CombatSide.Enemy)
        {
            return true;
        }

        Capped.LogOnce($"[STS2_WhiteAlbum2] 跳过「敌方侧玩家」的怪物回合（netId={__instance.Player?.NetId}）");

        // TakeTurn 是 async 方法：返回 false 必须自己回填 Task，
        // 否则调用方拿到 null，之后 await/Task.WhenAny 就会 NRE。
        __result = Task.CompletedTask;
        return false;
    }

    /// <summary>同样地，玩家单位没有"意图"可演，跳过它避免内部空引用。</summary>
    [HarmonyPatch(typeof(NCreature), nameof(NCreature.PerformIntent))]
    [HarmonyPrefix]
    private static bool SkipPlayerIntentPrefix(NCreature __instance, ref Task __result)
    {
        if (!WhiteAlbumSetting.DuelEnabled)
        {
            return true;
        }

        // NCreature 上的模型属性叫 Entity（不是 Creature）。
        var entity = __instance?.Entity;
        if (!DuelState.InDuel || entity is not { IsPlayer: true } || entity.Side != CombatSide.Enemy)
        {
            return true;
        }

        // PerformIntent 同样是 async：返回 false 要回填 Task。
        __result = Task.CompletedTask;
        return false;
    }

    /// <summary>
    /// 开局就必炸的一处：<c>Creature.AfterAddedToRoom</c> 里
    /// <c>if (Side == CombatSide.Enemy) await Monster.AfterAddedToRoom();</c> ——
    /// 对手被放到敌方侧之后 <c>Monster</c> 是 null，NRE 会把
    /// <c>CombatManager.StartCombatInternal</c> 打断，<b>整场战斗根本不会开始</b>
    /// （表现：进了战斗房间，但双方都没有回合、也不抽牌）。
    /// </summary>
    [HarmonyPatch(typeof(Creature), nameof(Creature.AfterAddedToRoom))]
    [HarmonyPrefix]
    private static bool SkipPlayerAfterAddedToRoomPrefix(Creature __instance, ref Task __result)
    {
        if (!WhiteAlbumSetting.DuelEnabled || !DuelState.InDuel || !__instance.IsPlayer || __instance.Side != CombatSide.Enemy)
        {
            return true;
        }

        Capped.LogOnce($"[STS2_WhiteAlbum2] 跳过「敌方侧玩家」的房间加入回调（没有 Monster 可跑）");

        // AfterAddedToRoom 是 async：返回 false 要回填 Task。
        __result = Task.CompletedTask;
        return false;
    }

    /// <summary>
    /// 战斗启动的另一处：<c>CombatManager.AfterCreatureAdded</c> 在
    /// <c>creature.IsEnemy &amp;&amp; CurrentSide == Player</c> 时会调
    /// <c>creature.Monster.RollMove(...)</c> —— 玩家单位没有 Monster，
    /// 又是一次 NRE，同样会把 <c>StartCombatInternal</c> 打断。
    /// </summary>
    /// <remarks>
    /// 只跳过"玩家单位"，怪物照旧。这个类里 <c>AfterAddedToRoom</c> 已经被单独跳过了，
    /// 所以这里返回 false 不会漏掉任何必要步骤。
    /// </remarks>
    [HarmonyPatch(
        typeof(CombatManager),
        "AfterCreatureAdded",
        new[] { typeof(Creature), typeof(CombatState) })]
    [HarmonyPrefix]
    private static bool SkipPlayerRollMovePrefix(Creature __0, ref Task __result)
    {
        if (!WhiteAlbumSetting.DuelEnabled || !DuelState.InDuel || !__0.IsPlayer || __0.Side != CombatSide.Enemy)
        {
            return true;
        }

        Capped.LogOnce($"[STS2_WhiteAlbum2] 跳过「敌方侧玩家」的 RollMove（没有 Monster 可掷意图）");

        // AfterCreatureAdded 是 async：返回 false 要回填 Task。
        __result = Task.CompletedTask;
        return false;
    }

    /// <summary>只打一次的诊断输出。</summary>
    /// <summary>
    /// 第 5 处同类：<c>Creature.PrepareForNextTurn</c> 开头就是
    /// <c>if (rollNewMove &amp;&amp; Monster.MoveStateMachine != null)</c> —— 玩家没有 Monster，
    /// 于是每到"回合开始准备"就 NRE（实测表现：刚打上「本回合行动者」就战斗启动失败）。
    /// </summary>
    /// <remarks>
    /// 返回值是 <c>void</c>，Prefix 直接返回 false 即可；被跳过的只有
    /// <c>Monster.RollMove</c> 与 <c>RefreshIntents</c> 两件事，对玩家单位都没有意义。
    /// </remarks>
    [HarmonyPatch(typeof(Creature), nameof(Creature.PrepareForNextTurn))]
    [HarmonyPrefix]
    private static bool SkipPlayerPrepareForNextTurnPrefix(Creature __instance)
    {
        if (!WhiteAlbumSetting.DuelEnabled || !DuelState.InDuel || !__instance.IsPlayer || __instance.Side != CombatSide.Enemy)
        {
            return true;
        }

        Capped.LogOnce($"[STS2_WhiteAlbum2] 跳过「敌方侧玩家」的回合准备（没有 Monster 可掷意图）");
        return false;
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
    [HarmonyPrefix]
    private static void ActorTurnPrefix(object __0)
    {
        if (!WhiteAlbumSetting.DuelEnabled || !DuelState.InDuel)
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

    /// <summary>PlayerCombatState 里的私有字段 <c>_player</c>。</summary>
    private static readonly AccessTools.FieldRef<PlayerCombatState, Player> OwnerField =
        AccessTools.FieldRefAccess<PlayerCombatState, Player>("_player");

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
    [HarmonyPrefix]
    private static bool TurnNumberPrefix(PlayerCombatState __instance)
    {
        if (!WhiteAlbumSetting.DuelEnabled || !DuelState.InDuel)
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

/// <summary>只打一次的诊断输出。</summary>
internal static class Capped
{
    private static readonly HashSet<string> Seen = [];

    public static void LogOnce(string message)
    {
        lock (Seen)
        {
            if (!Seen.Add(message))
            {
                return;
            }
        }

        Log.Info(message);
    }
}
