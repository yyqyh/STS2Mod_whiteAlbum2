using HarmonyLib;

using System.Threading.Tasks;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace STS2_WhiteAlbum2.Core.Pvp;

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
internal static class DuelSkipPlayerTakeTurnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Creature __instance, ref Task __result)
    {
        // 只针对"站在敌方侧的玩家"——F 方案下两个人都在我方侧，这些分支不会再命中，
        // 留着是为了兼容"对手在敌方侧"的老布置；加上阵营判断，避免我方玩家的正常流程也被跳过。
        if (!DuelConfig.Enabled || !DuelState.InDuel || !__instance.IsPlayer || __instance.Side != CombatSide.Enemy)
        {
            return true;
        }

        Capped.LogOnce($"[STS2_WhiteAlbum2] 跳过「敌方侧玩家」的怪物回合（netId={__instance.Player?.NetId}）");

        // TakeTurn 是 async 方法：返回 false 必须自己回填 Task，
        // 否则调用方拿到 null，之后 await/Task.WhenAny 就会 NRE。
        __result = Task.CompletedTask;
        return false;
    }
}

/// <summary>同样地，玩家单位没有"意图"可演，跳过它避免内部空引用。</summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.PerformIntent))]
internal static class DuelSkipPlayerIntentPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NCreature __instance, ref Task __result)
    {
        if (!DuelConfig.Enabled)
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
}

/// <summary>
/// 开局就必炸的一处：<c>Creature.AfterAddedToRoom</c> 里
/// <c>if (Side == CombatSide.Enemy) await Monster.AfterAddedToRoom();</c> ——
/// 对手被放到敌方侧之后 <c>Monster</c> 是 null，NRE 会把
/// <c>CombatManager.StartCombatInternal</c> 打断，<b>整场战斗根本不会开始</b>
/// （表现：进了战斗房间，但双方都没有回合、也不抽牌）。
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.AfterAddedToRoom))]
internal static class DuelSkipPlayerAfterAddedToRoomPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Creature __instance, ref Task __result)
    {
        if (!DuelConfig.Enabled || !DuelState.InDuel || !__instance.IsPlayer || __instance.Side != CombatSide.Enemy)
        {
            return true;
        }

        Capped.LogOnce($"[STS2_WhiteAlbum2] 跳过「敌方侧玩家」的房间加入回调（没有 Monster 可跑）");

        // AfterAddedToRoom 是 async：返回 false 要回填 Task。
        __result = Task.CompletedTask;
        return false;
    }
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
internal static class DuelSkipPlayerRollMovePatch
{
    [HarmonyPrefix]
    private static bool Prefix(Creature __0, ref Task __result)
    {
        if (!DuelConfig.Enabled || !DuelState.InDuel || !__0.IsPlayer || __0.Side != CombatSide.Enemy)
        {
            return true;
        }

        Capped.LogOnce($"[STS2_WhiteAlbum2] 跳过「敌方侧玩家」的 RollMove（没有 Monster 可掷意图）");

        // AfterCreatureAdded 是 async：返回 false 要回填 Task。
        __result = Task.CompletedTask;
        return false;
    }
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
internal static class DuelSkipPlayerPrepareForNextTurnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Creature __instance)
    {
        if (!DuelConfig.Enabled || !DuelState.InDuel || !__instance.IsPlayer || __instance.Side != CombatSide.Enemy)
        {
            return true;
        }

        Capped.LogOnce($"[STS2_WhiteAlbum2] 跳过「敌方侧玩家」的回合准备（没有 Monster 可掷意图）");
        return false;
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
