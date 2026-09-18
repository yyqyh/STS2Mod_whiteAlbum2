using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_WhiteAlbum2.Core.Pvp;

/// <summary>
/// 第三层：把「敌人」从"共享一份列表"改成"每个玩家各有自己的视角"。
/// </summary>
/// <remarks>
/// <para>
/// 本体里"谁是敌人"只有一个来源：<c>CombatState.HittableEnemies</c>
/// （<c>Enemies.Where(e =&gt; e.IsHittable)</c>）。而它同时被两处使用：
/// </para>
/// <list type="bullet">
/// <item><description>规则层：几乎所有攻击牌、AOE、随机目标、能力触发都读它；</description></item>
/// <item><description>UI 层：<c>NCardPlay</c> 拖牌时拿它当可选目标（这就是"能指向谁"的来源）。</description></item>
/// </list>
/// <para>
/// 决斗时对手被我挪到了敌方侧，于是那份共享列表是 <c>[对手, 假人]</c>：
/// 对"我方侧那位"完全正确，但对"站在敌方侧的那位"错了 —— 他自己也在列表里，
/// 而真正的对手（我方侧那位）却不在，导致他既打不到人、候选里还多了个自己。
/// </para>
/// <para>
/// 这里按<b>本机玩家</b>（<c>LocalContext.NetId</c>）重算一次：
/// <c>候选 = (Enemies - 自己) + 另一个玩家</c>。于是两边各看各的：
/// 我方侧看到 <c>[对手, 假人]</c>，敌方侧看到 <c>[我方那位, 假人]</c>。
/// </para>
/// <para>
/// 这是<b>本地视角</b>，不涉及跨端一致性 —— 目标选择本来就是本地 UI 行为，
/// 真正的伤害由同步的动作结算，所以两端视角不同不会分叉。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(CombatState), "get_HittableEnemies")]
internal static class DuelHittableOpponentPatch
{
    [HarmonyPostfix]
    private static void Postfix(CombatState __instance, ref IReadOnlyList<Creature> __result)
    {
        if (!DuelConfig.Enabled || !DuelState.InDuel || __result is null)
        {
            return;
        }

        var players = __instance.Players;
        if (players.Count != 2)
        {
            return;
        }

        var (mine, theirs) = SplitPerspective(players);

        if (mine is null || theirs is null)
        {
            return;
        }

        var merged = new List<Creature>(__result.Count + 1);

        foreach (var creature in __result)
        {
            // 自己绝不能出现在自己的攻击候选里（敌方侧的那位正好是这种情况）。
            if (!ReferenceEquals(creature, mine))
            {
                merged.Add(creature);
            }
        }

        // 把"真正的对手"补进来：我方侧那位本来就在 Enemies 里，
        // 敌方侧那位则需要补（对他而言对手是我方侧的人）。
        if (theirs.IsAlive && !merged.Any(creature => ReferenceEquals(creature, theirs)))
        {
            merged.Add(theirs);
        }

        Capped.LogOnce(
            $"[STS2_WhiteAlbum2] 按本机视角重算可选敌人：本机={mine.Player?.NetId ?? 0} "
            + $"对手={theirs.Player?.NetId ?? 0} 候选={merged.Count} 个");

        __result = merged;
    }

    /// <summary>按本机玩家把两位分成「我」与「对手」；认不出本机时退回 Players 顺序。</summary>
    private static (Creature? Mine, Creature? Theirs) SplitPerspective(IReadOnlyList<Player> players)
    {
        var localId = LocalContext.NetId;

        if (localId is { } id)
        {
            Creature? mine = null;
            Creature? theirs = null;

            foreach (var player in players)
            {
                if (player.NetId == id)
                {
                    mine = player.Creature;
                }
                else
                {
                    theirs = player.Creature;
                }
            }

            if (mine is not null && theirs is not null)
            {
                return (mine, theirs);
            }
        }

        return (players[0].Creature, players[1].Creature);
    }
}
