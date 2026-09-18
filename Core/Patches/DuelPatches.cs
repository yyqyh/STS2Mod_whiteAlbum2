using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_WhiteAlbum2.Core.Pvp;

/// <summary>
/// 战斗初始化：每加进来一名玩家就对齐一次决斗布置。
/// </summary>
/// <remarks>
/// 两个玩家是逐个加入战斗的，所以这里用 Postfix 在"第二位玩家进来之后"做改造。
/// <see cref="DuelMode.EnsureApplied" /> 本身是幂等的，多调几次没有副作用。
/// </remarks>
[HarmonyPatch(typeof(CombatState), nameof(CombatState.AddPlayer))]
internal static class DuelArmOnPlayerAddedPatch
{
    [HarmonyPostfix]
    private static void Postfix(CombatState __instance)
    {
        DuelMode.EnsureApplied(__instance);

        // 场景这时肯定已经起来了：顺手把开关按键挂上（幂等）。

    }
}

/// <summary>
/// 纯玩家对决：怪物一律不进战斗。
/// </summary>
/// <remarks>
/// 与其"先放进来再摘掉"（会牵动意图、血条、AI 一堆初始化），不如在这一步直接拦下 ——
/// <c>AddCreature</c> 是怪物进入战斗的唯一入口，而 <c>Creature.Side</c> 决定它会被放到哪一侧，
/// 未加入战斗的 creature 不会参与任何结算。
/// 玩家自己的 creature 不受影响（决斗双方都还是正常的 Player）。
/// </remarks>
[HarmonyPatch(typeof(CombatState), nameof(CombatState.AddCreature))]
internal static class DuelStripMonstersPatch
{
    [HarmonyPrefix]
    private static bool Prefix(CombatState __instance, Creature __0)
    {
        if (!DuelConfig.Enabled || __0 is null || __0.IsPlayer)
        {
            return true;
        }

        // 只有决斗那一场才拦怪物；普通战斗照旧生成。
        if (!DuelState.InDuel)
        {
            return true;
        }

        Log.Info($"[STS2_WhiteAlbum2] 决斗模式：拦下怪物 {__0.Monster?.Id.Entry ?? "?"} 不进战斗");
        return false;
    }
}

/// <summary>
/// 胜负：决斗里任意一方倒下就该结束。
/// </summary>
/// <remarks>
/// <para>
/// 对手站在敌方侧，所以"对手倒下"本体会按"敌人全灭"判成<b>胜利</b>，不需要我们管。
/// 需要接管的只有另一种情况：<b>我方侧（Players[0]）的玩家倒下</b>。
/// 本体的失败判据是"所有玩家都死了"（<c>RunManager.Players.All(p =&gt; p.Creature.IsDead)</c>），
/// 而对手此刻还活着、又在敌方侧，这个条件永远不成立 → 战斗会卡在那里，没人结束它。
/// </para>
/// <para>所以这里显式走一遍本体的失败收尾。</para>
/// </remarks>
[HarmonyPatch(
    typeof(CreatureCmd),
    nameof(CreatureCmd.Kill),
    new[] { typeof(IReadOnlyCollection<Creature>), typeof(bool) })]
internal static class DuelPlayerDownPatch
{
    [HarmonyPrefix]
    private static void Prefix(bool force, out bool __state)
    {
        // 主动放弃这一局（force）不算决斗分出胜负，走本体原本的流程即可。
        __state = force;
    }

    [HarmonyPostfix]
    private static void Postfix(IReadOnlyCollection<Creature> __0, bool __state)
    {
        if (__state || !DuelConfig.Enabled || !DuelState.InDuel || __0 is null)
        {
            return;
        }

        var deadPlayer = __0.FirstOrDefault(creature => creature.IsPlayer && creature.IsDead);
        if (deadPlayer?.CombatState is not { } state)
        {
            return;
        }

        if (!DuelMode.IsDuelActive(state))
        {
            return;
        }

        // F 方案下两个人都在我方侧，本体的"所有玩家都死了"只在真正全灭时成立；
        // 决斗要的是"谁先倒下谁输"，所以这里判断"倒下的是不是本机那位"。
        var me = LocalContext.GetMe(state)?.Creature;
        var iLost = ReferenceEquals(deadPlayer, me);

        DuelMode.EndRun(victory: !iLost);
    }
}
