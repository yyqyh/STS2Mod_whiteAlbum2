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

        var symbiosisBinding = new ModSettingsValueBinding<WhiteAlbumSetting, bool>(
            Const.ModId,
            WhiteAlbumSettingStore.DataKey,
            SaveScope.Global,
            _ => TogetherSettingsSync.EffectiveSymbiosisEnabled,
            (settings, value) =>
            {
                settings.SymbiosisEnabled = value;

                if (!value)
                {
                    SymbiosisMembers.Reset(RunManager.Instance?.NetService, "symbiosis_disabled");
                }

                TogetherSettingsSync.PublishHostSettings("settings_changed");
            });

        var mergeBinding = new ModSettingsValueBinding<WhiteAlbumSetting, bool>(
            Const.ModId,
            WhiteAlbumSettingStore.DataKey,
            SaveScope.Global,
            _ => TogetherSettingsSync.EffectiveMergeStarterDecks,
            (settings, value) =>
            {
                settings.MergeStarterDecks = value;
                TogetherSettingsSync.PublishHostSettings("settings_changed");
            });

        var hpBinding = new ModSettingsValueBinding<WhiteAlbumSetting, string>(
            Const.ModId,
            WhiteAlbumSettingStore.DataKey,
            SaveScope.Global,
            _ => Math
                .Clamp(TogetherSettingsSync.EffectiveHpBonusPercent, 0, 100)
                .ToString(CultureInfo.InvariantCulture),
            (settings, value) =>
            {
                if (int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent))
                {
                    settings.HpBonusPercent = Math.Clamp(percent, 0, 100);
                }

                TogetherSettingsSync.PublishHostSettings("settings_changed");
            });

        var shareGoldBinding = new ModSettingsValueBinding<WhiteAlbumSetting, bool>(
            Const.ModId,
            WhiteAlbumSettingStore.DataKey,
            SaveScope.Global,
            _ => TogetherSettingsSync.EffectiveShareGold,
            (settings, value) =>
            {
                settings.ShareGold = value;
                TogetherSettingsSync.PublishHostSettings("settings_changed");
            });

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

            page.AddSection("together", section =>
            {
                section
                    .WithTitle(ModSettingsText.Literal("Together"))
                    .AddToggle(
                        "symbiosis_enabled",
                        ModSettingsText.Literal("开启Together"),
                        symbiosisBinding,
                        ModSettingsText.Dynamic(DescribeSymbiosis))
                    .AddToggle(
                        "merge_starter_decks",
                        ModSettingsText.Literal("开局合并双方初始卡组（共享卡组 = p1 + p2）"),
                        mergeBinding,
                        ModSettingsText.Literal(
                            "开启：开局时把p2的初始卡组【复制】进共享卡组，"
                            + "卡组就是两个人的牌合在一起。\n"
                            + "关闭：共享卡组只包含锚点p1的初始卡组，回声那副不参与。\n"
                            + "注意：因为是复制，两人选同一个角色时开启它会得到两份初始卡（两个静默猎手 = 24/+1 张）；"
                            + "想要「同角色只要一份」就把它关掉。"))
                    .AddString(
                        "hp_bonus_percent",
                        ModSettingsText.Literal("血量上限提升：p2 最大生命的百分比（0~100）"),
                        hpBinding,
                        placeholder: ModSettingsText.Literal("例如 50"),
                        maxLength: 3,
                        description: ModSettingsText.Literal(
                            "把p2的百分之几加进共享血池：0 = 不加，100 = 把 p2 那一整份也加上。\n"
                            + "只在新开一局时生效一次（上限会写进存档，读档/重连不会重复加）。\n"
                            + "只填 0~100 的整数；填别的会被忽略并保留原值。"),
                        valueValidationVisual: IsValidPercent)
                    .AddToggle(
                        "share_gold",
                        ModSettingsText.Literal("共享金币（组内一个钱包）"),
                        shareGoldBinding,
                        ModSettingsText.Literal(
                            "开启：成员共用一个金币余额 —— 谁捡到金币、谁在商店花掉，都是改同一份余额。\n"
                            + "开局取组内最大值作为共同余额（只在开新局时对齐一次，读档/重连不会重复加）；关闭时各花各的。\n"
                            + "联机时以主机设置为准。"))
                    .AddParagraph(
                        "together_how_it_works",
                        ModSettingsText.Literal(
                            "开启后，联机选人界面会在原版角色基础上【额外加入】本 mod 的两个角色"
                            + "（雪菜 / 冬马）：\n"
                            + "1. 不需要额外按确认键，也用本体原本的出发/确认进入游戏；\n"
                            + "2. 只有“正好 2 人、两个人分别选了不同的本 mod 角色”时才会开启 together；\n"
                            + "3. 只选 1 个本 mod 角色、人数不是 2、两人选了同一个本 mod 角色时，"
                            + "都会按普通联机局运行；\n"
                            + "4. 开启 together 后共用一个身体（血量 / 格挡 / 状态 / 卡组 / 抽牌堆 / 弃牌堆都是同一份）；\n"
                            + "5. 手牌与能量仍然各人各一份（你打你的、我打我的）；\n"
                            + "6. 共享卡组里放谁的初始卡由上面「开局合并双方初始卡组」决定；\n"
                            + "7. 关闭开关时，本 mod 不介入联机角色规则；单人默认不出现这两个角色"
                            + "（可在上面的「角色」小节打开单人可见）。\n"
                            + "联机时以主机设置为准。"));
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
                            + "关闭后，两个角色只在联机角色选择界面出现。"));
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

    private static string DescribeSymbiosis()
    {
        return TogetherSettingsSync.EffectiveSymbiosisEnabled
            ? "当前：开启 —— 联机选人时额外加入两个本 mod 角色；满足“2 人 + 两个不同 mod 角色”时自动开启 together。"
            : "当前：关闭 —— 本 mod 不介入任何对局。";
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

    private static bool IsValidPercent(string? value)
    {
        return int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent)
               && percent is >= 0 and <= 100;
    }

    private static bool IsValidRounds(string? value)
    {
        return int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rounds)
               && rounds is >= 1 and <= 999;
    }
}
