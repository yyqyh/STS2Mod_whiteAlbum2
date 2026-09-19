using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2_WhiteAlbum2.Core.Pvp;
using STS2_WhiteAlbum2.Core.Settings;
using System.Threading.Tasks;

namespace STS2_WhiteAlbum2.Core.Patches.PvpEvent;

/// <summary>决斗事件入口与房间替换</summary>
internal static class DuelRoomPatches
{
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
    /// 这条只在<b>设置里的调试开关打开</b>时生效（<c>WhiteAlbumSetting.DuelEventDebugFirstQuestion</c>）——
    /// 它的用途是少跑图；正式流程见 <c>DuelFinalBossRoomPatch</c>。
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(RunManager), "RollRoomTypeFor")]
    [HarmonyPostfix]
    private static void FirstUnknownRoomPostfix(RunManager __instance, MapPointType __0, ref RoomType __result)
    {
        if (!WhiteAlbumSetting.DuelEnabled || !WhiteAlbumSetting.DuelEventEnabled || !WhiteAlbumSetting.DuelEventDebugFirstQuestion)
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

        Log.Info("[STS2_WhiteAlbum2] 本局第一个问号房 → 决斗事件");
        __result = RoomType.Event;
    }

    /// <summary>
    /// 让那个问号开出的是<b>我们的</b>事件，而不是从事件池里随机抽一个。
    /// </summary>
    /// <remarks>
    /// <c>CreateRoom</c> 在 <c>RoomType.Event</c> 分支里走的是
    /// <c>State.Act.PullNextEvent(State)</c>（随机抽），所以要在它返回之后把结果换掉。
    /// </remarks>
    [HarmonyPatch(typeof(RunManager), "CreateRoom")]
    [HarmonyPostfix]
    private static void EventRoomPostfix(RoomType __0, ref AbstractRoom __result)
    {
        if (!WhiteAlbumSetting.DuelEnabled || __0 != RoomType.Event)
        {
            return;
        }

        if (!DuelRoomPlan.ConsumePendingDuel())
        {
            return;
        }

        Log.Info("[STS2_WhiteAlbum2] 用「对决邀请」替换掉随机事件");

        // EventRoom 要的是 canonical（不可变）模型：本体自己也是直接传
        // State.Act.PullNextEvent(...) 的结果。给它 Mutable 副本会被 AssertCanonical 拦下。
        __result = new EventRoom(ModelDb.Event<DuelInvitationEvent>());
    }

    /// <summary>
    /// 命中时<b>不改参数</b>，而是拿新房间再调一次本体、把它返回的 Task 交回调用方。
    /// 原因：<c>EnterRoom</c> 是 async 方法，前缀里写 <c>ref</c> 参数要落到状态机的字段上，
    /// 不如"重新调一次"这条来得稳（第二次进来时房间已经不是 TheArchitect，会直接放行）。
    /// </summary>
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
    [HarmonyPrefix]
    private static bool FinalBossRoomPrefix(RunManager __instance, AbstractRoom room, ref Task __result)
    {
        if (!WhiteAlbumSetting.DuelEnabled || !WhiteAlbumSetting.DuelEventEnabled || WhiteAlbumSetting.DuelEventDebugFirstQuestion)
        {
            return true;
        }

        if (room is not EventRoom { CanonicalEvent: TheArchitect })
        {
            return true;
        }

        Log.Info("[STS2_WhiteAlbum2] 三层最终 boss 已打完 → 生成决斗事件（替换本体结局事件）");

        __result = __instance.EnterRoom(new EventRoom(ModelDb.Event<DuelInvitationEvent>()));
        return false;
    }

    /// <summary>
    /// 吞掉决斗事件选项在"角色变量注入"那一步抛出的异常。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本体 <c>EventOption</c> 构造时会调 <c>AddLocVars</c>：
    /// </para>
    /// <code>
    /// private void AddLocVars(EventModel eventModel)
    /// {
    ///     eventModel.Owner?.Character.AddDetailsTo(Description);   // ← 我们的路径上这句 NRE（Character 为 null）
    ///     Description.Add("IsMultiplayer", ...);
    /// }
    /// </code>
    /// <para>
    /// 关键点：<c>Description</c> 是在<b>传参时求值</b>的（属性拿到即初始化），所以异常发生时
    /// 选项的文案其实已经就绪；后面那句只是往里面塞一个 <c>{IsMultiplayer}</c> 变量，我们的文案不用它。
    /// 因此这里用 <see cref="HarmonyFinalizerAttribute" /> 把这次异常吞掉，让构造函数正常收尾。
    /// </para>
    /// <para>
    /// 之前试过"整个跳过 <c>AddLocVars</c>"（文案没初始化 → <c>ToString()</c> NRE → 事件进不去）
    /// 和"临时置空 <c>Owner</c>"（<c>Description</c> 仍未初始化，照样炸），都不如这一版干净。
    /// </para>
    /// <para>只吞"我方事件"的异常，其它事件、其它异常一律原样抛回。</para>
    /// </remarks>
    [HarmonyPatch(typeof(EventOption), "AddLocVars")]
    [HarmonyFinalizer]
    private static Exception? OptionLocVarsFinalizer(EventModel __0, Exception? __exception)
    {
        if (__exception is null || __0 is not DuelInvitationEvent)
        {
            return __exception;
        }

        Log.Warn(
            $"[STS2_WhiteAlbum2] 已吞掉决斗事件选项的角色变量注入异常（文案已就绪，不影响显示）："
            + $"{__exception.GetType().Name}: {__exception.Message}");

        return null;
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
