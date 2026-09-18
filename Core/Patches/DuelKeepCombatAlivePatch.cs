using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;

namespace STS_WhiteAlbum2.Core.Pvp;

/// <summary>
/// 决斗里的战斗不能因为「敌方侧没人」就结束。
/// </summary>
/// <remarks>
/// <para>
/// 本体 CombatManager.IsCombatEnding 的判据是：敌方侧只要有活的 PrimaryEnemy 就继续，
/// 否则就认为敌人全灭、战斗结束（胜利）。
/// </para>
/// <para>
/// 决斗是清掉所有怪物、两个玩家互相打，所以一开打敌方侧本来就是空的 ——
/// 于是战斗刚开始就被判成「敌人全灭、胜利」，玩家什么都没做就弹结算
/// （实测表现就是「直接跳过战斗」）。
/// </para>
/// <para>
/// 这里在决斗模式下把这个结果压成 false，让战斗继续；胜负交给
/// DuelPlayerDownPatch（谁先倒下）和 DuelRoundLimit（回合上限）。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(CombatManager), "IsCombatEnding")]
internal static class DuelKeepCombatAlivePatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        if (!__result || !DuelConfig.Enabled || !DuelState.InDuel)
        {
            return;
        }

        Capped.LogOnce("[STS_WhiteAlbum2] 决斗：敌方侧没有怪物，但不结束战斗（避免开局即判定胜利）");
        __result = false;
    }
}
