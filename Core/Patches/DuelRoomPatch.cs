using MegaCrit.Sts2.Core.Entities.Cards;
using HarmonyLib;

using System.Threading.Tasks;

using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS_WhiteAlbum2.Core.Pvp;

/// <summary>
/// 事件入口①（调试）：把<b>本局第一个问号房</b>变成决斗事件。
/// </summary>
/// <remarks>
/// <para>
/// 本体决定"问号房到底变成什么"只有一处：<c>RunManager.RollRoomTypeFor</c>，其中
/// <c>MapPointType.Unknown =&gt; State.Odds.UnknownMapPoint.Roll(blacklist, State)</c>。
/// 在这里把结果改掉，就能 100% 控制"第几个问号变成什么"。
/// </para>
/// <para>
/// 这条只在<b>设置里的调试开关打开</b>时生效（<c>DuelConfig.EventDebugFirstQuestion</c>）——
/// 它的用途是少跑图；正式流程见 <c>DuelFinalBossRoomPatch</c>。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(RunManager), "RollRoomTypeFor")]
internal static class DuelFirstUnknownRoomPatch
{
    [HarmonyPostfix]
    private static void Postfix(RunManager __instance, MapPointType __0, ref RoomType __result)
    {
        if (!DuelConfig.Enabled || !DuelConfig.EventEnabled || !DuelConfig.EventDebugFirstQuestion)
        {
            return;
        }

        if (__0 != MapPointType.Unknown)
        {
            return;
        }

        // RunManager.State 不是公开属性（本体在类内部用），外部只能走这个访问器。
        if (!DuelRoomPlan.TryTakeFirstUnknown(__instance.DebugOnlyGetState()))
        {
            return;
        }

        Log.Info("[STS_WhiteAlbum2] 本局第一个问号房 → 决斗事件");
        __result = RoomType.Event;
    }
}

/// <summary>
/// 让那个问号开出的是<b>我们的</b>事件，而不是从事件池里随机抽一个。
/// </summary>
/// <remarks>
/// <c>CreateRoom</c> 在 <c>RoomType.Event</c> 分支里走的是
/// <c>State.Act.PullNextEvent(State)</c>（随机抽），所以要在它返回之后把结果换掉。
/// </remarks>
[HarmonyPatch(typeof(RunManager), "CreateRoom")]
internal static class DuelEventRoomPatch
{
    [HarmonyPostfix]
    private static void Postfix(RoomType __0, ref AbstractRoom __result)
    {
        if (!DuelConfig.Enabled || __0 != RoomType.Event)
        {
            return;
        }

        if (!DuelRoomPlan.ConsumePendingDuel())
        {
            return;
        }

        Log.Info("[STS_WhiteAlbum2] 用「对决邀请」替换掉随机事件");

        // EventRoom 要的是 canonical（不可变）模型：本体自己也是直接传
        // State.Act.PullNextEvent(...) 的结果。给它 Mutable 副本会被 AssertCanonical 拦下。
        __result = new EventRoom(ModelDb.Event<DuelInvitationEvent>());
    }
}

/// <summary>记住"这一局的第一个问号已经用掉了"（换一局自动重置）。</summary>
internal static class DuelRoomPlan
{
    private static IRunState? _run;
    private static bool _used;
    private static bool _pending;

    public static bool TryTakeFirstUnknown(IRunState? runState)
    {
        if (!ReferenceEquals(_run, runState))
        {
            _run = runState;
            _used = false;
            _pending = false;
        }

        if (_used)
        {
            return false;
        }

        _used = true;
        _pending = true;
        return true;
    }

    /// <summary>取出"刚才那个问号是我们的"这个标记（只生效一次）。</summary>
    public static bool ConsumePendingDuel()
    {
        if (!_pending)
        {
            return false;
        }

        _pending = false;
        return true;
    }
}

/// <summary>
/// 事件入口②（正式）：<b>三层的最终 boss 打完</b>之后生成决斗事件。
/// </summary>
/// <remarks>
/// <para>
/// 本体在"打完最后一幕的 boss"时走的是 <c>RunManager.EnterNextAct</c> 的最后一幕分支：
/// 淡出 → 清屏 → 进入结局事件房（<c>TheArchitect</c>）→ 淡入。
/// </para>
/// <para>
/// 所以只要在 <c>EnterRoom</c> 这一步把"结局事件"换成我们的「对决邀请」，就正好落在
/// "三层最终 boss 之后"这个位置，而且淡出/清屏/淡入那些流程完全不用自己写
/// （<c>ClearScreens</c> 是 private，硬碰不如借道）。
/// </para>
/// <para>
/// <b>只认 <c>TheArchitect</c> 这一个事件</b>：<c>EnterRoom</c> 是通用入口，
/// 普通事件房、读档恢复也会走它，而只有"本体要进结局事件"这一种情况会被替换，
/// 别的房间一律不动。最后一幕有两个 boss（双 boss 升华）也照样命中 ——
/// 因为本体自己就是在这条分支里收尾的。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterRoom))]
internal static class DuelFinalBossRoomPatch
{
    /// <summary>
    /// 命中时<b>不改参数</b>，而是拿新房间再调一次本体、把它返回的 Task 交回调用方。
    /// 原因：<c>EnterRoom</c> 是 async 方法，前缀里写 <c>ref</c> 参数要落到状态机的字段上，
    /// 不如"重新调一次"这条来得稳（第二次进来时房间已经不是 TheArchitect，会直接放行）。
    /// </summary>
    [HarmonyPrefix]
    private static bool Prefix(RunManager __instance, AbstractRoom room, ref Task __result)
    {
        if (!DuelConfig.Enabled || !DuelConfig.EventEnabled || DuelConfig.EventDebugFirstQuestion)
        {
            return true;
        }

        if (room is not EventRoom { CanonicalEvent: TheArchitect })
        {
            return true;
        }

        Log.Info("[STS_WhiteAlbum2] 三层最终 boss 已打完 → 生成决斗事件（替换本体结局事件）");

        __result = __instance.EnterRoom(new EventRoom(ModelDb.Event<DuelInvitationEvent>()));
        return false;
    }
}
