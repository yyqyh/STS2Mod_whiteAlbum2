using System.Globalization;

using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace STS_WhiteAlbum2.Core.Pvp;

/// <summary>
/// 设置界面：一个开关 + 一个回合上限。
/// </summary>
/// <remarks>
/// <para>
/// 用 RitsuLib 的 ModSettings 框架注册（和 together 同一套）。
/// 文案一律 <c>ModSettingsText.Literal</c>，不依赖本地化表。
/// </para>
/// <para>
/// 局内改设置没有意义（决斗的布置在战斗初始化时就定下来了），所以标成"局内只读"，
/// 避免出现「改了没生效」的困惑。游戏内还留了 F9 快捷键用于快速切换开关。
/// </para>
/// </remarks>
internal static class PvpModSettingsPage
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        var enabledBinding = new ModSettingsValueBinding<PvpSettings, bool>(
            Const.ModId,
            PvpSettingsStore.DataKey,
            SaveScope.Global,
            settings => settings.Enabled,
            (settings, value) => settings.Enabled = value);

        var roundsBinding = new ModSettingsValueBinding<PvpSettings, string>(
            Const.ModId,
            PvpSettingsStore.DataKey,
            SaveScope.Global,
            settings => Math.Clamp(settings.MaxRounds, 1, 999).ToString(CultureInfo.InvariantCulture),
            (settings, value) =>
            {
                if (int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rounds))
                {
                    settings.MaxRounds = Math.Clamp(rounds, 1, 999);
                }
            });

        var eventEnabledBinding = new ModSettingsValueBinding<PvpSettings, bool>(
            Const.ModId,
            PvpSettingsStore.DataKey,
            SaveScope.Global,
            settings => settings.EventEnabled,
            (settings, value) => settings.EventEnabled = value);

        var eventDebugBinding = new ModSettingsValueBinding<PvpSettings, bool>(
            Const.ModId,
            PvpSettingsStore.DataKey,
            SaveScope.Global,
            settings => settings.EventDebugFirstQuestion,
            (settings, value) => settings.EventDebugFirstQuestion = value);

        RitsuLibFramework.RegisterModSettings(Const.ModId, page => page
            .WithTitle(ModSettingsText.Literal("PVP 决斗"))
            .WithModDisplayName(ModSettingsText.Literal("PVP 决斗"))
            .WithReadOnlyOnHostSurfaces(ModSettingsHostSurface.RunPause | ModSettingsHostSurface.CombatPause)
            .AddSection("event_entry", section => section
                .WithTitle(ModSettingsText.Literal("事件入口"))
                .AddToggle(
                    "event_enabled",
                    ModSettingsText.Literal("生成「对决邀请」事件"),
                    eventEnabledBinding,
                    ModSettingsText.Dynamic(DescribeEventEnabled))
                .AddToggle(
                    "event_debug_first_question",
                    ModSettingsText.Literal("调试：固定在第一个问号房"),
                    eventDebugBinding,
                    ModSettingsText.Dynamic(DescribeEventDebug),
                    visibleWhen: () => PvpSettingsStore.Current.EventEnabled)
                .AddParagraph(
                    "event_where",
                    ModSettingsText.Literal(
                        "事件生成位置由上面那个调试开关决定：\n"
                        + "· 开：本局第一个问号房 100% 是对决邀请（跑图最快，适合测试）；\n"
                        + "· 关：走正式流程 —— 三层的最终 boss 打完、本体本来要进结局事件的那一刻，"
                        + "换成对决邀请。\n"
                        + "两个位置都只在「开启决斗模式」时才会出现。")))
            .AddSection("duel", section => section
                .WithTitle(ModSettingsText.Literal("决斗"))
                .AddToggle(
                    "enabled",
                    ModSettingsText.Literal("开启决斗模式"),
                    enabledBinding,
                    ModSettingsText.Dynamic(DescribeEnabled))
                .AddString(
                    "max_rounds",
                    ModSettingsText.Literal("回合上限（1~999）"),
                    roundsBinding,
                    placeholder: ModSettingsText.Literal("例如 30"),
                    maxLength: 3,
                    description: ModSettingsText.Literal(
                        "打满这么多回合还没分出胜负，就按双方的剩余血量百分比判定：血多的一方赢。\n"
                        + "用百分比而不是绝对血量，是因为两个人可以选不同角色、最大生命可能差很多。\n"
                        + "填 1~999 的整数；填别的会被忽略并保留原值。想快速验证可以填 3。"),
                    valueValidationVisual: IsValidRounds)
                .AddParagraph(
                    "how_it_works",
                    ModSettingsText.Literal(
                        "入口：「对决邀请」事件，位置见上面的调试开关。\n"
                        + "接受之后，联机的两人这一场会进入决斗：\n"
                        + "1. 这一场不会出现怪物，对手被放到敌方一侧；\n"
                        + "2. 双方各自用自己的角色与卡组；\n"
                        + "3. 轮到谁由本体的回合结构决定（我方回合 → 对手回合交替）；\n"
                        + "4. 谁先倒下谁输；打满回合上限则按剩余血量比例判定；\n"
                        + "5. 决斗是一局定胜负，分出结果后直接结算这一局。\n"
                        + "游戏内也可以按 F9 快速开关（结果同样会写进设置）。"))));
    }

    private static string DescribeEventEnabled()
    {
        return PvpSettingsStore.Current.EventEnabled
            ? "当前：生成 —— 事件会出现在下面选定的位置，进去之后可以接受对决。"
            : "当前：不生成 —— 本 mod 的事件入口完全不会出现（相当于关掉整个 mod 的入口）。";
    }

    private static string DescribeEventDebug()
    {
        return PvpSettingsStore.Current.EventDebugFirstQuestion
            ? "当前：调试模式 —— 本局第一个问号房一定是对决邀请，跑图最快。"
            : "当前：正式流程 —— 事件在三层最终 boss 打完之后、本体要进结局事件那一刻生成。";
    }

    private static string DescribeEnabled()
    {
        return PvpSettingsStore.Current.Enabled
            ? "当前：开启 —— 联机的战斗会变成与队友的决斗。"
            : "当前：关闭 —— 本 mod 不介入任何对局。";
    }

    private static bool IsValidRounds(string? value)
    {
        return int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rounds)
               && rounds is >= 1 and <= 999;
    }
}
