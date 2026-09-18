using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;

namespace STS2_WhiteAlbum2.Core.Pvp;

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
