using MegaCrit.Sts2.Core.Logging;

namespace STS_WhiteAlbum2.Core.Pvp;

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
        Log.Info("[STS_WhiteAlbum2] 已登记：下一场战斗为决斗");
    }

    /// <summary>新的一场战斗开始：消费标记（只对下一场有效）。</summary>
    public static void TakeForNewCombat()
    {
        InDuel = Armed;
        Armed = false;

        Log.Info(InDuel
            ? "[STS_WhiteAlbum2] 这场战斗是决斗"
            : "[STS_WhiteAlbum2] 这场是普通战斗（不做任何改造）");
    }

    public static void Clear()
    {
        InDuel = false;
        Armed = false;
    }
}
