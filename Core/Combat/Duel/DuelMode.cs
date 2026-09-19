using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Runs;
using STS2_WhiteAlbum2.Core.Settings;
using STS2_WhiteAlbum2.Core.Patches.PvpEvent;

namespace STS2_WhiteAlbum2.Core.Pvp;

/// <summary>
/// 决斗模式的战斗改造：清掉怪物，把第二名玩家挪到敌方侧。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么是"挪到敌方侧"而不是"两人都在我方、互相可选"</b>：本体的目标选择、
/// AOE（"对所有敌人"）、随机目标、意图这些全都是以 <c>Creature.Side</c> 与
/// <c>CombatState.Enemies</c> 为准的。让对手真的站在敌方侧，这些语义天然就是对的。
/// </para>
/// <para>
/// <b>为什么不会把"玩家"这个概念弄坏</b>：本体的
/// <c>CombatState.Players</c> 是从两侧一起收集玩家的
/// （<c>Creatures = Allies + Enemies</c>，再筛 <c>IsPlayer</c>），
/// 所以对手即使站在敌方侧，它依然是一个正常的 <c>Player</c>（有手牌、有能量、有 PlayerCombatState）。
/// </para>
/// <para>
/// <b>顺序很关键</b>：<c>Creature.Side</c> 是只读属性（构造时定死），而
/// <c>CombatState.AddCreature</c> 是按 <c>creature.Side</c> 决定放进哪一侧的。
/// 所以必须先改 Side 的 backing field，再 Remove、再 Add —— 三步顺序反了就会抛
/// "Creature is already in this combat"。
/// </para>
/// <para>
/// <b>谁是"对手"必须两端算出同一个答案</b>：这里固定用
/// <c>Players[1]</c>（大厅顺序里靠后的那位），绝不能用"本机视角"去判断 ——
/// 否则主机和客户端会把不同的人挪到敌方侧，当场分叉。
/// </para>
/// </remarks>
internal static class DuelMode
{
    /// <summary>Creature.Side 是只读属性，改它得动 backing field。</summary>
    private static readonly AccessTools.FieldRef<Creature, CombatSide> SideField =
        AccessTools.FieldRefAccess<Creature, CombatSide>("<Side>k__BackingField");

    /// <summary>直接往敌方列表里塞 creature 用（绕过被我们拦住的 AddCreature）。</summary>
    private static readonly AccessTools.FieldRef<CombatState, List<Creature>> EnemiesField =
        AccessTools.FieldRefAccess<CombatState, List<Creature>>("_enemies");

    /// <summary>
    /// 在敌方侧放一只"绝对不还手"的训练假人。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 用本体的 <c>BattleFriendV1</c>：它的移动表里只有 <c>NOTHING_MOVE</c>（什么都不做），
    /// 75 点生命，正是"站着挨打"的靶子。
    /// </para>
    /// <para>
    /// 为什么要它：本体的回合循环在"敌方侧空着"时行为很怪 ——
    /// 实测会出现"对手的回合开始了、但没人给他跑回合开场（没费没牌）"。
    /// 有个哑巴敌人站着，敌方回合就有正常的宿主，<c>ExecuteEnemyTurn</c> 也能按部就班地跑完。
    /// </para>
    /// <para>
    /// 直接往 <c>_enemies</c> 里加，是因为 <c>AddCreature</c> 被我们的"决斗不放怪物进来"补丁拦着；
    /// creature 本身仍然走 <c>CreateCreature</c> 正常创建（CombatState、CombatId 都会挂好）。
    /// </para>
    /// </remarks>
    private static void SpawnDummy(CombatState state)
    {
        try
        {
            if (state.Enemies.Any(creature => creature?.Monster is BattleFriendV1))
            {
                return;
            }

            var dummy = state.CreateCreature(
                ModelDb.Monster<BattleFriendV1>().ToMutable(),
                CombatSide.Enemy,
                null);

            EnemiesField(state).Add(dummy);

            Log.Info("[STS2_WhiteAlbum2] 已在敌方侧放置训练假人（不还手，避免敌方回合空转）");
        }
        catch (Exception ex)
        {
            Log.Error($"[STS2_WhiteAlbum2] 放置训练假人失败：{ex}");
        }
    }

    private static CombatState? _announced;

    /// <summary>当前这场决斗（供按键心跳检查回合数用）。</summary>
    public static CombatState? Current { get; private set; }

    /// <summary>这局是不是已经在按决斗跑了（对手确实站在敌方侧）。</summary>
    /// <remarks>
    /// 参数用接口类型：<c>Creature.CombatState</c> 暴露的是 <c>ICombatState</c>，
    /// 而"两侧玩家的收集"（Players 来自 Allies + Enemies）在接口上就有。
    /// 只有"摘除/加入 creature"那两个操作是 <c>CombatState</c> 独有的，
    /// 所以 <see cref="EnsureApplied" /> 仍然收具体类型。
    /// </remarks>
    public static bool IsDuelActive(ICombatState? state)
    {
        return WhiteAlbumSetting.DuelEnabled
               && state is { } combat
               && combat.Players.Count == 2;
    }

    /// <summary>
    /// 幂等地把战斗改造成决斗：没有怪物，<c>Players[1]</c> 在敌方侧。
    /// </summary>
    /// <remarks>
    /// 战斗初始化会分好几步（建遭遇、加玩家、开回合），我们没法保证只被调用一次，
    /// 所以这里做成"每次调用都保证结果正确"，而不是"只执行一次"。
    /// </remarks>
    public static void EnsureApplied(CombatState? state)
    {
        if (!WhiteAlbumSetting.DuelEnabled || state is null)
        {
            return;
        }

        if (state.Players.Count != 2)
        {
            return;
        }

        Current = state;

        if (!ReferenceEquals(_announced, state))
        {
            _announced = state;
            DuelRoundLimit.Reset();

            // 上一场如果留下了"等对手结束回合"的等待，这里放行掉，别让它卡住这一场。
            DuelTurnBridge.ReleaseAll();

            // 新的一场战斗：把"下一场是决斗"的标记消费掉（只对下一场有效）。
            DuelState.TakeForNewCombat();

            Log.Info(
                $"[STS2_WhiteAlbum2] 新的战斗：allies={state.Allies.Count} "
                + $"enemies={state.Enemies.Count} players={state.Players.Count}");
        }

        // 普通战斗：一点都不碰。
        if (!DuelState.InDuel)
        {
            return;
        }

        EnsureReplaySnapshot();

        RemoveMonsters(state);

        // 方案③（用户提的最小侵入做法）：对手**保持玩家身份**、不做任何"怪物化"处理 ——
        // 那五处 Monster 相关的坑（TakeTurn / AfterAddedToRoom / RollMove / PrepareForNextTurn / PerformIntent）
        // 一个都不会命中，敌方回合也完全不需要接管。
        // 只在两处"看法"上做文章：
        //   ① 立绘位置：把对手的节点挪到敌方容器（DuelOpponentVisualPatch）
        //   ② 敌我视角：按本机玩家重算可选目标（DuelHittableOpponentPatch）
        //
        // MoveOpponentToEnemySide(state);   // 不再需要：那会把对手变成"敌方单位"，代价是五处坑
        // SpawnDummy(state);                // 也不再需要：敌方侧空着由 IsCombatEnding 的补丁兜住

        DuelTurnOrder.Arm(state);
    }

    /// <summary>
    /// 补上战斗录像的初始快照（<c>CombatReplayWriter.RecordInitialState</c>）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本体只在两条路上记这个快照：<b>移动到地图点</b>（<c>RunManager.MoveToMapCoord</c> 那条）和
    /// <b>按房间类型直接进房</b>。而事件里"开打"是第三条路
    /// （<c>EventModel.EnterCombatWithoutExitingEvent</c>）—— 它不记。
    /// </para>
    /// <para>
    /// 快照为空时，<c>CombatReplayWriter</c> 的每个钩子都会抛
    /// <c>RecordInitialState must be called first</c>，而回合开始的第一个 checksum 正好踩中它 ——
    /// 整条回合链当场死掉，表现就是"战斗卡住不动"
    /// （log：<c>Combat #N turn loop died while its combat is in progress</c>）。
    /// </para>
    /// <para>
    /// 这个坑在"第一个问号房"那条路上看不见，是因为玩家<b>走地图点</b>进问号房时本体已经记过一次快照了；
    /// 三层最终 boss 之后是 <c>EnterNextAct → EnterRoom(事件房)</c>，不经过那条路，于是当场爆。
    /// </para>
    /// </remarks>
    private static void EnsureReplaySnapshot()
    {
        try
        {
            var run = RunManager.Instance;
            var writer = run?.CombatReplayWriter;

            if (run is null || writer is null || !writer.IsEnabled || writer.IsRecordingReplay)
            {
                return;
            }

            writer.RecordInitialState(run.ToSave(null));

            Capped.LogOnce("[STS2_WhiteAlbum2] 决斗：补上战斗录像的初始快照（本体只在走地图点/直接进房时才记）");
        }
        catch (Exception ex)
        {
            Log.Warn($"[STS2_WhiteAlbum2] 补战斗录像快照失败（不影响战斗）：{ex.Message}");
        }
    }

    /// <summary>纯玩家对决：把敌方侧的怪物全部摘掉（它们不会再被瞄准、也不再行动）。</summary>
    private static void RemoveMonsters(CombatState state)
    {
        foreach (var creature in state.Enemies.ToList())
        {
            if (creature.IsPlayer)
            {
                continue;
            }

            try
            {
                state.RemoveCreature(creature, unattach: true);
                Log.Info($"[STS2_WhiteAlbum2] 移除怪物：{creature.Monster?.Id.Entry ?? "?"}");
            }
            catch (Exception ex)
            {
                Log.Warn($"[STS2_WhiteAlbum2] 移除怪物失败：{ex.Message}");
            }
        }
    }

    /// <summary>
    /// 把对手挪到敌方侧（改 Side → 摘除 → 加回）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这一步<b>不能省</b>：卡牌的"能打谁"来自 <c>CombatState.Enemies</c>，
    /// 战斗 UI 也按 <c>Creature.Side</c> 决定把谁画在对面。两人都留在我方侧时，
    /// 结果是"打不到对方、立绘也不换位置"（实测）。
    /// </para>
    /// <para>
    /// 挪过去之后，对手依然是一个正常的 <c>Player</c>（<c>CombatState.Players</c>
    /// 是从 allies + enemies 两侧一起收集玩家的），所以<b>轮流行动那套完全不受影响</b>；
    /// 而它作为"敌方单位"会被 <c>ExecuteEnemyTurn</c> 遍历到 —— 那几条怪物专属路径
    /// （TakeTurn / AfterAddedToRoom / RollMove）由 DuelEnemyTurnGuardPatch 跳过。
    /// </para>
    /// <para>顺序必须是：改 Side → RemoveCreature → AddCreature（AddCreature 按 Side 分派）。</para>
    /// </remarks>
    private static void MoveOpponentToEnemySide(CombatState state)
    {
        if (state.Players.Count != 2)
        {
            return;
        }

        var opponent = state.Players[1].Creature;

        if (opponent.Side == CombatSide.Enemy)
        {
            return;
        }

        try
        {
            state.RemoveCreature(opponent, unattach: false);
            SideField(opponent) = CombatSide.Enemy;
            state.AddCreature(opponent);

            Log.Info($"[STS2_WhiteAlbum2] 对手 netId={state.Players[1].NetId} 已移到敌方侧（立绘与目标选择都跟着对）");
        }
        catch (Exception ex)
        {
            Log.Error($"[STS2_WhiteAlbum2] 移动对手到敌方侧失败：{ex}");
        }
    }

    /// <summary>
    /// 结束这场决斗（走本体的跑局结算）。
    /// </summary>
    /// <remarks>
    /// 决斗是"一局定胜负"，所以不论输赢都直接结算这一局：
    /// 胜负双方各走本体的 <c>OnEnded</c>，由它写入 run history、清掉存档、弹结算屏。
    /// </remarks>
    public static void EndRun(bool victory)
    {
        if (!RunManager.Instance.IsInProgress || RunManager.Instance.IsCleaningUp)
        {
            return;
        }

        Log.Info($"[STS2_WhiteAlbum2] 决斗结束：{(victory ? "我方胜利" : "我方失败")}");

        if (CombatManager.Instance.IsInProgress)
        {
            CombatManager.Instance.LoseCombat();
        }

        if (NRun.Instance is { } run)
        {
            run.RunMusicController.StopMusic();
            NAudioManager.Instance?.PlayMusic(
                victory ? "event:/temp/sfx/victory" : "event:/temp/sfx/game_over");
        }

        var serializableRun = RunManager.Instance.OnEnded(victory);
        NRun.Instance?.ShowGameOverScreen(serializableRun);
    }
}
