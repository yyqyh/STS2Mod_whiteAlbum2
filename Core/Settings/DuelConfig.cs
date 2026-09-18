using MegaCrit.Sts2.Core.Logging;

using STS2_WhiteAlbum2.Core.Character;

namespace STS2_WhiteAlbum2.Core.Pvp;

/// <summary>
/// 决斗的开关与参数：统一从 RitsuLib 的设置存储读。
/// </summary>
/// <remarks>
/// 设置界面与游戏内 F9 改的是<b>同一份</b>数据（<see cref="PvpSettingsStore" />），
/// 所以不存在"界面上开着、实际却没生效"这种两套状态。
/// </remarks>
internal static class DuelConfig
{
    /// <summary>本局角色组合是否满足决斗要求：正好两人，且分别选了不同的本 mod 角色。</summary>
    public static bool CharacterConditionMet => AlbumCharacterRules.CurrentRunHasRequiredAlbumPair();

    /// <summary>是否开启决斗模式（设置开启 + 本局角色条件满足）。</summary>
    public static bool Enabled => PvpSettingsStore.Current.Enabled && CharacterConditionMet;

    /// <summary>是否生成决斗事件（关掉 = 哪个入口都不出现）。</summary>
    public static bool EventEnabled => Enabled && PvpSettingsStore.Current.EventEnabled;

    /// <summary>事件生成位置：true = 固定第一个问号房（调试）；false = 三层最终 boss 之后（正式）。</summary>
    public static bool EventDebugFirstQuestion => PvpSettingsStore.Current.Debug && PvpSettingsStore.Current.EventDebugFirstQuestion;

    /// <summary>回合上限（1~999，默认 30）。</summary>
    public static int MaxRounds => Math.Clamp(PvpSettingsStore.Current.MaxRounds, 1, 999);

    /// <summary>游戏内切换开关（快捷键用）。</summary>
    public static void SetEnabled(bool value)
    {
        PvpSettingsStore.Update(settings => settings.Enabled = value);

        Log.Info($"[STS2_WhiteAlbum2] 决斗模式已{(value ? "开启" : "关闭")}");
    }
}
