using System.Globalization;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;
using STS2_WhiteAlbum2.Core.Content;

namespace STS2_WhiteAlbum2.Core.Settings;

/// <remarks>独立注册一张页，不再往别人的页面构建器尾部追加控件。文案用 <c>ModSettingsText.Literal</c>，不依赖本地化表。</remarks>
internal static class AlbumSettingsPage
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        var debugBinding = new ModSettingsValueBinding<WhiteAlbumSetting, bool>(
            Const.ModId,
            WhiteAlbumSettingStore.DataKey,
            SaveScope.Global,
            settings => settings.Debug,
            (settings, value) => settings.Debug = value);

        var singleplayerCharacterBinding = new ModSettingsValueBinding<WhiteAlbumSetting, bool>(
            Const.ModId,
            WhiteAlbumSettingStore.DataKey,
            SaveScope.Global,
            settings => settings.CharactersVisibleInSingleplayer,
            (settings, value) => settings.CharactersVisibleInSingleplayer = value);

        var pvpEnabledBinding = new ModSettingsValueBinding<WhiteAlbumSetting, bool>(
            Const.ModId,
            WhiteAlbumSettingStore.DataKey,
            SaveScope.Global,
            settings => settings.Enabled,
            (settings, value) => settings.Enabled = value);

        var pvpEventBinding = new ModSettingsValueBinding<WhiteAlbumSetting, bool>(
            Const.ModId,
            WhiteAlbumSettingStore.DataKey,
            SaveScope.Global,
            settings => settings.EventEnabled,
            (settings, value) => settings.EventEnabled = value);

        var pvpEventDebugBinding = new ModSettingsValueBinding<WhiteAlbumSetting, bool>(
            Const.ModId,
            WhiteAlbumSettingStore.DataKey,
            SaveScope.Global,
            settings => settings.EventDebugFirstQuestion,
            (settings, value) => settings.EventDebugFirstQuestion = value);

        var pvpRoundsBinding = new ModSettingsValueBinding<WhiteAlbumSetting, string>(
            Const.ModId,
            WhiteAlbumSettingStore.DataKey,
            SaveScope.Global,
            settings => Math.Clamp(settings.MaxRounds, 1, 999).ToString(CultureInfo.InvariantCulture),
            (settings, value) =>
            {
                if (int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rounds))
                {
                    settings.MaxRounds = Math.Clamp(rounds, 1, 999);
                }
            });

        RitsuLibFramework.RegisterModSettings(Const.ModId, page =>
        {
            page
                .WithTitle(ModSettingsText.Literal("White Album 2"))
                .WithModDisplayName(ModSettingsText.Literal("White Album 2"))
                .WithReadOnlyOnHostSurfaces(ModSettingsHostSurface.RunPause | ModSettingsHostSurface.CombatPause);

            page.AddSection("debug", section =>
            {
                section
                    .WithTitle(ModSettingsText.Literal("调试"))
                    .AddToggle(
                        "debug_master",
                        ModSettingsText.Literal("调试总开关"),
                        debugBinding,
                        ModSettingsText.Dynamic(DescribeDebug))
                    .AddParagraph(
                        "debug_scope",
                        ModSettingsText.Literal(
                            "关闭后，所有带「调试：」前缀的选项都不生效；其余正式玩法不受影响。\n"
                            + "当前受它控制的是 PVP 的「对决事件放在第一个问号房」。"));
            });

            page.AddSection("character", section =>
            {
                section
                    .WithTitle(ModSettingsText.Literal("角色"))
                    .AddToggle(
                        "singleplayer_characters_visible",
                        ModSettingsText.Literal("单人可以看见 Setsuna / Touma"),
                        singleplayerCharacterBinding,
                        ModSettingsText.Literal(
                            "默认开启：单人角色选择界面也会显示 Setsuna / Touma，方便单人测试。\n"
                            + "关闭后，两个角色只在联机角色选择界面出现。"))
                    .AddParagraph(
                        "album_pair_rule",
                        ModSettingsText.Literal(
                            "联机时这两个角色会【额外加入】原版角色列表（不需要额外的确认键，用本体的出发/确认进游戏）。\n"
                            + "只有「正好 2 人、且两人分别选了不同的本 mod 角色」时才会开启共享："
                            + "共用一个身体（血量 / 格挡 / 状态 / 卡组 / 抽牌堆 / 弃牌堆），手牌与能量各人各一份。\n"
                            + "只选 1 个、人数不是 2、或两人选了同一个角色，都按普通联机局运行。\n"
                            + "共享本身与它的开关由 together mod 负责，相关设置在 together 的设置页里。"));
            });

            page.AddSection("pvp", section =>
            {
                section
                    .WithTitle(ModSettingsText.Literal("PVP 决斗"))
                    .AddToggle(
                        "pvp_enabled",
                        ModSettingsText.Literal("开启 PVP 决斗"),
                        pvpEnabledBinding,
                        ModSettingsText.Dynamic(DescribePvpEnabled))
                    .AddToggle(
                        "pvp_event_enabled",
                        ModSettingsText.Literal("生成对决事件"),
                        pvpEventBinding,
                        ModSettingsText.Dynamic(DescribePvpEvent))
                    .AddToggle(
                        "pvp_event_debug_first_question",
                        ModSettingsText.Literal("调试：对决事件放在第一个问号房"),
                        pvpEventDebugBinding,
                        ModSettingsText.Dynamic(DescribePvpEventDebug),
                        visibleWhen: () => WhiteAlbumSettingStore.Current.Debug)
                    .AddString(
                        "pvp_max_rounds",
                        ModSettingsText.Literal("回合上限（1~999）"),
                        pvpRoundsBinding,
                        placeholder: ModSettingsText.Literal("例如 30"),
                        maxLength: 3,
                        description: ModSettingsText.Literal(
                            "打满这么多回合还没分出胜负，就按双方的剩余血量百分比判定：血多的一方赢。\n"
                            + "用百分比而不是绝对血量，是因为两个人可以选不同角色、最大生命可能差很多。\n"
                            + "填 1~999 的整数；填别的会被忽略并保留原值。想快速验证可以填 3。"),
                        valueValidationVisual: IsValidRounds)
                    .AddParagraph(
                        "pvp_how_it_works",
                        ModSettingsText.Literal(
                            "启动条件：本局正好 2 人，且两个人分别选了不同的本 mod 角色；"
                            + "不满足时不会生成/启动决斗。\n"
                            + "入口：「对决邀请」事件。\n"
                            + "开始决斗时会先解除共生体：两个人恢复成独立的卡组与牌堆，"
                            + "共享卡组按当前顺序的奇数张给 P1、偶数张给 P2。\n"
                            + "接受之后，联机的两人这一场会进入决斗：\n"
                            + "1. 这一场不会出现怪物，对手被放到敌方一侧；\n"
                            + "2. 双方各自用自己的角色与卡组；\n"
                            + "3. 轮到谁由本体的回合结构决定（我方回合 → 对手回合交替）；\n"
                            + "4. 谁先倒下谁输；打满回合上限则按剩余血量比例判定；\n"
                            + "5. 决斗是一局定胜负，分出结果后直接结算这一局。"));
            });
        });
    }

    private static string DescribeDebug()
    {
        return WhiteAlbumSettingStore.Current.Debug
            ? "当前：开启 —— 调试选项会生效。"
            : "当前：关闭 —— 所有「调试：」选项都不生效。";
    }

    private static string DescribePvpEnabled()
    {
        return WhiteAlbumSettingStore.Current.Enabled
            ? "当前：设置开启 —— 仅在本局正好 2 人且分别选了不同 mod 角色时才会真正启动决斗。"
            : "当前：关闭 —— 本 mod 不介入任何对局。";
    }

    private static string DescribePvpEvent()
    {
        return WhiteAlbumSettingStore.Current.EventEnabled
            ? "当前：生成 —— 决斗事件会出现在选定的位置。"
            : "当前：不生成 —— PVP 事件入口完全不出现。";
    }

    private static string DescribePvpEventDebug()
    {
        return WhiteAlbumSettingStore.Current.EventDebugFirstQuestion
            ? "当前：固定第一个问号房（需要上面的调试总开关也开着）。"
            : "当前：走正式流程 —— 三层最终 boss 打完后、本体要进结局事件那一刻生成。";
    }

    private static bool IsValidRounds(string? value)
    {
        return int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rounds)
               && rounds is >= 1 and <= 999;
    }
}
