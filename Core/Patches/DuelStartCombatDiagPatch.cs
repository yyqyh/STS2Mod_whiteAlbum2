using System.Threading.Tasks;

using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Logging;

namespace STS_WhiteAlbum2.Core.Pvp;

/// <summary>
/// 诊断：把 <c>StartCombatInternal</c> 里被吞掉的异常完整打出来。
/// </summary>
/// <remarks>
/// <para>
/// 本体启动战斗的那条链路异常处理很"安静"：
/// <c>RunTurnLoopAfter</c> → <c>TaskHelper.LogTaskExceptions</c> 只打顶层那一层栈，
/// 于是 log 里只剩 <c>at CombatManager.StartCombatInternal(...)</c> 一行，
/// 根本看不出是哪一句炸的。
/// </para>
/// <para>
/// 这里把返回的 <c>Task</c> 包一层，用 <c>ex.ToString()</c> 打印类型、消息和<b>完整栈</b>，
/// 再用 <c>throw</c> 原样抛回去，不改变本体的行为。定位完可以整体删掉这个文件。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(CombatManager), "StartCombatInternal")]
internal static class DuelStartCombatDiagPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref Task __result)
    {
        if (!DuelConfig.Enabled || __result is null)
        {
            return;
        }

        __result = Wrap(__result);
    }

    private static async Task Wrap(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            // 战斗被取消（离房、退出、读档）是正常路径：本体的回合循环在收尾时一律以取消结束。
            if (ex is TaskCanceledException or OperationCanceledException)
            {
                throw;
            }

            Log.Error($"[STS_WhiteAlbum2] 战斗启动失败（完整异常）：{ex}");
            throw;
        }
    }
}
