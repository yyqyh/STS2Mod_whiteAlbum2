using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_WhiteAlbum2.Core.Pvp;

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
