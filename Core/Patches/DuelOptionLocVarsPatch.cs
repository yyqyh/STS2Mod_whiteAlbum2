using HarmonyLib;

using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace STS_WhiteAlbum2.Core.Pvp;

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
internal static class DuelOptionLocVarsPatch
{
    [HarmonyFinalizer]
    private static Exception? Finalizer(EventModel __0, Exception? __exception)
    {
        if (__exception is null || __0 is not DuelInvitationEvent)
        {
            return __exception;
        }

        Log.Warn(
            $"[STS_WhiteAlbum2] 已吞掉决斗事件选项的角色变量注入异常（文案已就绪，不影响显示）："
            + $"{__exception.GetType().Name}: {__exception.Message}");

        return null;
    }
}
