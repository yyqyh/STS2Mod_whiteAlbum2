using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Content;

namespace STS_WhiteAlbum2.Core.Ancients;

/// <summary>
/// 「哪一幕换成什么」的唯一清单。
/// </summary>
/// <remarks>
/// <para>
/// 按 <see cref="ActModel.Index" />（0/1/2）分，不认具体 Act 类型 ——
/// 第一幕本体有两个候选（<c>Overgrowth</c> / <c>Underdocks</c>），按 Index 分就不用管这一局用的是哪张图。
/// </para>
/// <para>
/// 4 个占位 boss 的分配：一幕一个，第三幕的<b>第二 boss</b>（双 boss 升华）用第四个。
/// </para>
/// </remarks>
internal static class ReplacePlan
{
    /// <summary>占位 boss 的血量。</summary>
    public const int PlaceholderHp = 100;

    /// <summary>占位 boss 每回合的攻击伤害 —— 也就是"意图：攻击 1"。</summary>
    public const int PlaceholderAttackDamage = 1;

    /// <summary>这一幕的先古之民。</summary>
    public static Type? AncientFor(int actIndex) => actIndex switch
    {
        0 => typeof(ActOneAncient),
        1 => typeof(ActTwoAncient),
        2 => typeof(ActThreeAncient),
        _ => null,
    };

    /// <summary>这一幕的 boss。</summary>
    public static Type? BossFor(int actIndex) => actIndex switch
    {
        0 => typeof(PlaceholderBossEncounterOne),
        1 => typeof(PlaceholderBossEncounterTwo),
        2 => typeof(PlaceholderBossEncounterThree),
        _ => null,
    };

    /// <summary>这一幕的"第二 boss"（只有第三幕配了）。</summary>
    public static Type? SecondBossFor(int actIndex) => actIndex switch
    {
        2 => typeof(PlaceholderBossEncounterFour),
        _ => null,
    };

    /// <summary>这套内容注册的全部类型（打 id 用，方便核对本地化键）。</summary>
    public static IReadOnlyList<Type> AllContentTypes { get; } =
    [
        typeof(ActOneAncient), typeof(ActTwoAncient), typeof(ActThreeAncient),
        typeof(PlaceholderBossOne), typeof(PlaceholderBossTwo),
        typeof(PlaceholderBossThree), typeof(PlaceholderBossFour),
        typeof(PlaceholderBossEncounterOne), typeof(PlaceholderBossEncounterTwo),
        typeof(PlaceholderBossEncounterThree), typeof(PlaceholderBossEncounterFour),
        typeof(AncientReplaceRelicPool),
        typeof(ActOneRelicAlpha), typeof(ActOneRelicBeta), typeof(ActOneRelicGamma),
        typeof(ActTwoRelicAlpha), typeof(ActTwoRelicBeta), typeof(ActTwoRelicGamma),
        typeof(ActThreeRelicAlpha), typeof(ActThreeRelicBeta), typeof(ActThreeRelicGamma),
    ];

    /// <summary>
    /// 把内容的真实 id 打进日志。
    /// </summary>
    /// <remarks>
    /// RitsuLib 给模组模型的 id 是 <c>&lt;模组id&gt;_&lt;类别&gt;_&lt;类名&gt;</c>
    /// （例如 <c>ANCIENT_REPLACE_EVENT_ACT_ONE_ANCIENT</c>），本地化键就是拿这些 id 拼的。
    /// 这里直接问 RitsuLib 要"它会给这个类型分配的条目"，
    /// 而不是读 <c>ModelDb.GetId()</c> —— 那是注册<b>之后</b>才会带前缀的，
    /// 而本入口跑在内容注册之前，读出来会少一截前缀。
    /// </remarks>
    public static void LogContentIds()
    {
        foreach (var type in AllContentTypes)
        {
            try
            {
                var entry = ModContentRegistry.GetFixedPublicEntry(Const.ModId, type);
                Log.Info($"[{Const.ModId}] 内容 id：{type.Name} = {entry}");
            }
            catch (Exception ex)
            {
                Log.Warn($"[{Const.ModId}] 取内容 id 失败：{type.Name}（{ex.Message}）");
            }
        }
    }
}
