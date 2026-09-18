using MegaCrit.Sts2.Core.Logging;

namespace STS_WhiteAlbum2.Core.Pvp;

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
    private static int MaxRounds => DuelConfig.MaxRounds;

    /// <summary>新的一场战斗开始时重置。</summary>
    public static void Reset()
    {
        _finishedThisCombat = false;
    }

    public static void Tick()
    {
        if (_finishedThisCombat || !DuelConfig.Enabled)
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
            $"[STS_WhiteAlbum2] 达到回合上限 {MaxRounds}：我方 {myRatio:P0} vs 对手 {theirRatio:P0} → "
            + (victory ? "判定胜利" : "判定失败"));

        DuelMode.EndRun(victory);
    }

    private static double Ratio(int current, int max)
    {
        return max <= 0 ? 0d : (double)current / max;
    }
}
