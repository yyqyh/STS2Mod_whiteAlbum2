using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using STS2_WhiteAlbum2.Core.Settings;

namespace STS2_WhiteAlbum2.Core.Combat.Duel;

/// <summary>决斗运行时状态：状态、回合顺序、回合桥接、回合上限</summary>

/// <summary>
/// 「这场战斗是不是决斗」——把决斗限制在事件触发的那一场，普通战斗完全不受影响。
/// </summary>
/// <remarks>
/// 之前是"开关一开，所有战斗都变决斗"，那会把正常爬塔一起改掉。现在用两个标记：
/// Armed = 事件里点了「接受对决」，下一场战斗是决斗；
/// InDuel = 当前这场确实是决斗（进战斗时从 Armed 转过来，用完即清）。
/// 没经过事件的战斗里 InDuel 永远是 false，清怪、拦怪物、轮流行动都不会插手。
/// </remarks>
internal static class DuelState
{
    /// <summary>下一场战斗要是决斗（由事件设置）。</summary>
    public static bool Armed { get; private set; }

    /// <summary>当前这场战斗是决斗。</summary>
    public static bool InDuel { get; private set; }

    /// <summary>事件里点了「接受对决」。</summary>
    public static void ArmForNextCombat()
    {
        Armed = true;
        Log.Info("[STS2_WhiteAlbum2] 已登记：下一场战斗为决斗");
    }

    /// <summary>新的一场战斗开始：消费标记（只对下一场有效）。</summary>
    public static void TakeForNewCombat()
    {
        InDuel = Armed;
        Armed = false;

        Log.Info(InDuel
            ? "[STS2_WhiteAlbum2] 这场战斗是决斗"
            : "[STS2_WhiteAlbum2] 这场是普通战斗（不做任何改造）");
    }

    public static void Clear()
    {
        InDuel = false;
        Armed = false;
    }
}

/// <summary>
/// 决斗的回合顺序：把本体的"所有玩家同时行动"改成"一人一回合，轮流来"。
/// </summary>
/// <remarks>
/// <para>
/// 本体联机的玩家回合是<b>并行</b>的（大家都动完才推进），决斗要的是交替。
/// 实现方式见 <c>DuelActorTurnPatch</c>：把"这一个回合的参与者"限定成当前行动者
/// （本体 <c>Play/额外回合</c> 那套机制，佩尔之眼用的就是它）。
/// </para>
/// <para>
/// 这里只负责"轮到谁"这本账，另外记下每个人是否已经行动过 ——
/// 后者是给<b>回合数</b>用的：<c>PlayerCombatState.TurnNumber</c> 在本体是"进入我方侧就全体 +1"，
/// 而决斗里没出手的那位不该被算作过了一回合（否则他的首次回合会变成"第 2 回合"，
/// 固有牌 <c>Innate</c> 置顶那段只在 <c>TurnNumber == 1</c> 时跑，他就永远享受不到）。
/// </para>
/// </remarks>
internal static class DuelTurnOrder
{
    private static int _actorIndex;
    private static readonly HashSet<Player> Acted = [];
    private static ICombatState? _combat;

    /// <summary>当前该谁行动。</summary>
    public static Player? Actor { get; private set; }

    public static bool IsActor(Player? player)
    {
        return player is not null && ReferenceEquals(player, Actor);
    }

    /// <summary>下一轮该谁行动（自增之前问它）。</summary>
    public static bool IsNextActor(Player? player)
    {
        if (player is null || _combat is not { } combat || combat.Players.Count == 0)
        {
            return false;
        }

        return ReferenceEquals(combat.Players[_actorIndex % combat.Players.Count], player);
    }

    /// <summary>这位玩家在本场战斗里是否已经行动过。</summary>
    public static bool HasActedBefore(Player? player)
    {
        return player is not null && Acted.Contains(player);
    }

    /// <summary>新的一场战斗：从头开始。</summary>
    public static void Arm(ICombatState state)
    {
        if (ReferenceEquals(_combat, state))
        {
            return;
        }

        _combat = state;
        _actorIndex = 0;
        Acted.Clear();
        Actor = null;
    }

    /// <summary>一轮开始（我方回合开始）时指定行动者。</summary>
    public static void BeginRound(ICombatState state)
    {
        var players = state.Players;
        if (players.Count == 0)
        {
            return;
        }

        Actor = players[_actorIndex % players.Count];
        _actorIndex++;
        Acted.Add(Actor);

        Log.Info($"[STS2_WhiteAlbum2] 本回合行动者：netId={Actor.NetId}");
    }
}

/// <summary>
/// 「等对手结束回合」的信号桥。
/// </summary>
/// <remarks>
/// <para>
/// 接管的敌方回合里，轮到"对手（站在敌方侧的玩家）"时我们要阻塞住，等他真的点结束回合。
/// 本体的玩家回合结束入口是 <c>CombatManager.SetReadyToEndTurn(player, canBackOut, ...)</c>
/// （<c>EndPlayerTurnAction</c> 最终会走到它），所以这里就是：
/// </para>
/// <list type="number">
/// <item><description>开回合前 <see cref="Expect" /> 登记一个等待；</description></item>
/// <item><description><c>SetReadyToEndTurn</c> 的 Postfix 调 <see cref="Signal" /> 放行。</description></item>
/// </list>
/// <para>
/// 按 netId 登记而不是按对象，是因为存档/重连之后 <c>Player</c> 实例会换新的。
/// 同一个玩家重复登记时，旧的等待者会被直接放行（否则那场战斗会永远卡住）。
/// </para>
/// </remarks>
internal static class DuelTurnBridge
{
    private static readonly Dictionary<ulong, TaskCompletionSource> Waiting = [];

    public static TaskCompletionSource Expect(Player player)
    {
        var tcs = new TaskCompletionSource();

        lock (Waiting)
        {
            if (Waiting.Remove(player.NetId, out var stale))
            {
                stale.TrySetResult();
            }

            Waiting[player.NetId] = tcs;
        }

        return tcs;
    }

    public static void Signal(ulong netId)
    {
        TaskCompletionSource? tcs;

        lock (Waiting)
        {
            if (!Waiting.Remove(netId, out tcs))
            {
                tcs = null;
            }
        }

        tcs?.TrySetResult();
    }

    /// <summary>战斗结束/清理时把所有等待者放行，避免残留的 Task 把下一场战斗卡住。</summary>
    public static void ReleaseAll()
    {
        List<TaskCompletionSource> all;

        lock (Waiting)
        {
            all = Waiting.Values.ToList();
            Waiting.Clear();
        }

        foreach (var tcs in all)
        {
            tcs.TrySetResult();
        }
    }
}

/// <summary>
/// 回合上限：打满 N 个回合还没分出胜负，就按双方剩余血量比例判定。
/// </summary>
/// <remarks>
/// <para>
/// 判据用"血量百分比"而不是"绝对血量"：两个人的最大生命可能差很多
/// （各自选的角色不同、或者带了加生命的遗物），比绝对值会偏向血厚的那个。
/// </para>
/// <para>
/// 少一个挂点就少一处和本体回合流程纠缠的机会。
/// </para>
/// </remarks>
internal static class DuelRoundLimit
{
    private static bool _finishedThisCombat;

    /// <summary>回合上限（来自配置文件，默认 30）。</summary>
    private static int MaxRounds => WhiteAlbumSetting.DuelMaxRounds;

    /// <summary>新的一场战斗开始时重置。</summary>
    public static void Reset()
    {
        _finishedThisCombat = false;
    }

    public static void Tick()
    {
        if (_finishedThisCombat || !WhiteAlbumSetting.DuelEnabled)
        {
            return;
        }

        if (DuelMode.Current is not { } state || state.Players.Count != 2)
        {
            return;
        }

        if (state.RoundNumber < MaxRounds)
        {
            return;
        }

        _finishedThisCombat = true;

        var mine = state.Players[0].Creature;
        var theirs = state.Players[1].Creature;

        var myRatio = Ratio(mine.CurrentHp, mine.MaxHp);
        var theirRatio = Ratio(theirs.CurrentHp, theirs.MaxHp);

        // 回合数用尽：血多的一方赢；完全打平算我方失败（决斗里"没赢就是输"）。
        var victory = myRatio > theirRatio;

        Log.Info(
            $"[STS2_WhiteAlbum2] 达到回合上限 {MaxRounds}：我方 {myRatio:P0} vs 对手 {theirRatio:P0} → "
            + (victory ? "判定胜利" : "判定失败"));

        DuelMode.EndRun(victory);
    }

    private static double Ratio(int current, int max)
    {
        return max <= 0 ? 0d : (double)current / max;
    }
}
