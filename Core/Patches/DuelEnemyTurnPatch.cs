using System.Reflection;
using System.Threading.Tasks;

using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS_WhiteAlbum2.Core.Pvp;

/// <summary>
/// 路线 A：接管敌方回合，让"站在敌方侧的玩家"真的能操作。
/// </summary>
/// <remarks>
/// <para>
/// 本体敌方回合（<c>CombatManager.ExecuteEnemyTurn</c>）是给怪物用的：对每个敌方单位
/// 演意图 + 跑 AI 回合。对手站在敌方侧之后,那里必须换一套流程 ——
/// 否则 <c>Creature.TakeTurn()</c> 会直接抛
/// 「Only enemy monsters can take automated turns」把敌方回合打死。
/// </para>
/// <para>
/// 这里把整个敌方回合接管下来，对"玩家单位"改成跑一遍<b>玩家回合的开场</b>
/// （重置能量 → 抽牌 → 回合开始钩子），把 phase 设成可出牌，然后<b>等他点结束回合</b>。
/// 开场那几步是照着本体 <c>CombatManager.SetupPlayerTurn</c> 复刻的 —— 它本身是 private，
/// 而且需要一个由 <c>StartTurn</c> 内部创建的 <c>HookPlayerChoiceContext</c>，外面拿不到；
/// 但那个 context 的构造函数是公开的（<c>new HookPlayerChoiceContext(player, netId, type)</c>），
/// 所以复刻是可行的：用到的 <c>Hook.*</c> 与 <c>CardPileCmd.Draw</c> 都是公开 API。
/// </para>
/// <para>
/// 阻塞等待靠 <see cref="DuelTurnBridge" />（登记等待 + <c>SetReadyToEndTurn</c> 放行）。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(CombatManager), "ExecuteEnemyTurn")]
internal static class DuelEnemyTurnPatch
{
    /// <summary>本体的收尾方法（private）：发校验和之后要靠它推进到下一轮。</summary>
    private static readonly MethodInfo? EndEnemyTurnMethod =
        AccessTools.Method(typeof(CombatManager), "EndEnemyTurn");

    private static readonly PropertyInfo? StateProperty =
        AccessTools.Property(AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatTurnState"), "State");

    private static readonly AccessTools.FieldRef<CombatManager, bool> ActionsDisabledField =
        AccessTools.FieldRefAccess<CombatManager, bool>("_playerActionsDisabled");

    [HarmonyPrefix]
    private static bool Prefix(CombatManager __instance, object __0, object __1, ref Task __result)
    {
        if (!DuelConfig.Enabled || !DuelState.InDuel || EndEnemyTurnMethod is null || StateProperty is null)
        {
            return true;
        }

        __result = RunEnemyTurnAsync(__instance, __0, __1);
        return false;
    }

    private static async Task RunEnemyTurnAsync(CombatManager manager, object turnState, object actionDuringEnemyTurn)
    {
        if (StateProperty?.GetValue(turnState) is not ICombatState state)
        {
            return;
        }

        // 本体原本的第一步：等外部注入的动作（测试用；正常流程里是 null）。
        if (actionDuringEnemyTurn is Func<Task> action)
        {
            await action();
        }

        foreach (var enemy in state.Enemies.ToList())
        {
            if (!state.ContainsCreature(enemy))
            {
                continue;
            }

            if (enemy.IsPlayer && enemy.Player is { } opponent)
            {
                await RunOpponentTurnAsync(state, opponent);
                continue;
            }

            // 决斗里不该出现怪物（AddCreature 已经拦下了）；真出现也不驱动它，避免误伤玩家。
            Capped.LogOnce($"[STS_WhiteAlbum2] 敌方侧出现非玩家单位，已跳过：{enemy.Monster?.Id.Entry ?? "?"}");
        }

        RunManager.Instance.ChecksumTracker.GenerateChecksum("After enemy turn end", null);

        try
        {
            if (EndEnemyTurnMethod?.Invoke(manager, [turnState]) is Task endTask)
            {
                await endTask;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[STS_WhiteAlbum2] 收尾敌方回合失败：{ex}");
        }
    }

    /// <summary>对手的"回合"：复刻本体的玩家回合开场，然后等他自己结束回合。</summary>
    private static async Task RunOpponentTurnAsync(ICombatState state, Player opponent)
    {
        if (opponent.Creature.IsDead || opponent.PlayerCombatState is null)
        {
            return;
        }

        Log.Info($"[STS_WhiteAlbum2] 对手 netId={opponent.NetId} 的回合开始");

        // 这个 context 就是本体 StartTurn 里造的那个东西；构造函数是公开的，所以能自己造。
        var choiceContext = new HookPlayerChoiceContext(
            opponent,
            LocalContext.NetId ?? opponent.NetId,
            GameActionType.CombatPlayPhaseOnly);

        // —— 以下照抄 CombatManager.SetupPlayerTurn（本体 880-927 行）——
        if (Hook.ShouldPlayerResetEnergy(state, opponent))
        {
            opponent.PlayerCombatState.ResetEnergy();
        }
        else
        {
            opponent.PlayerCombatState.AddMaxEnergyToCurrent();
        }

        await Hook.AfterEnergyReset(state, opponent);

        await Hook.BeforeHandDraw(state, opponent, choiceContext);
        var handDraw = Hook.ModifyHandDraw(state, opponent, 5m, out var modifiers);
        await Hook.AfterModifyingHandDraw(state, modifiers);

        await CardPileCmd.Draw(choiceContext, handDraw, opponent, fromHandDraw: true);
        await Hook.AfterPlayerTurnStart(state, choiceContext, opponent);
        // —— 照抄结束 ——

        // 让他能出牌（本体 RunAutoPrePlayPhase 最后做的就是这一步）。
        opponent.PlayerCombatState.Phase = PlayerTurnPhase.Play;

        // 敌方回合里本体把"玩家操作"禁用了（那回合本来是给怪物的）；决斗里这回合是玩家的，要放开，
        // 否则对手看得到手牌却点不动、也就永远点不了结束回合。
        ActionsDisabledField(CombatManager.Instance) = false;
        Capped.LogOnce("[STS_WhiteAlbum2] 敌方回合里放开了玩家操作（这个回合其实是玩家的）");

        // 等他自己点"结束回合"。
        var completion = DuelTurnBridge.Expect(opponent);
        await completion.Task;

        Log.Info($"[STS_WhiteAlbum2] 对手 netId={opponent.NetId} 结束了回合");
    }
}

/// <summary>玩家点"结束回合"时放行对应的等待。</summary>
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetReadyToEndTurn))]
internal static class DuelEndTurnSignalPatch
{
    [HarmonyPostfix]
    private static void Postfix(Player player)
    {
        if (!DuelConfig.Enabled || player is null)
        {
            return;
        }

        DuelTurnBridge.Signal(player.NetId);
    }
}
