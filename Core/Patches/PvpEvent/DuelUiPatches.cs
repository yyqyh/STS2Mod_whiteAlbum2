using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2_WhiteAlbum2.Core.Pvp;
using STS2_WhiteAlbum2.Core.Settings;

namespace STS2_WhiteAlbum2.Core.Patches.PvpEvent;

/// <summary>决斗界面：回合横幅</summary>
internal static class DuelUiPatches
{
    private static readonly AccessTools.FieldRef<NPlayerTurnBanner, MegaLabel> LabelField =
        AccessTools.FieldRefAccess<NPlayerTurnBanner, MegaLabel>("_label");

    /// <summary>把"额外回合"改回"玩家回合"。</summary>
    [HarmonyPatch(typeof(NPlayerTurnBanner), "_Ready")]
    [HarmonyPostfix]
    private static void PlayerTurnBannerPostfix(NPlayerTurnBanner __instance)
    {
        if (!DuelBanner.ShouldOverride())
        {
            return;
        }

        LabelField(__instance)?.SetTextAutoSize(
            new LocString("gameplay_ui", "PLAYER_TURN").GetFormattedText());
    }

    /// <summary>敌方侧一个单位都没有时，不要弹"敌方回合"。</summary>
    [HarmonyPatch(typeof(NEnemyTurnBanner), "Create")]
    [HarmonyPrefix]
    private static bool EnemyTurnBannerPrefix(ref NEnemyTurnBanner? __result)
    {
        if (!DuelBanner.ShouldOverride())
        {
            return true;
        }

        // 返回 null 是安全的：加节点的地方用的是 AddChildSafely，它自带 null 判断。
        __result = null;
        Capped.LogOnce("[STS2_WhiteAlbum2] 决斗里不再弹「敌方回合」横幅（那一回合没有敌人）");
        return false;
    }
}

/// <summary>
/// 决斗里那两个"名不副实"的回合横幅：一个是多出来的"敌方回合"，一个是被叫成"额外回合"的普通回合。
/// </summary>
/// <remarks>
/// <para>
/// 为什么会有：决斗的轮流是按本体的<b>额外回合</b>机制实现的（见 <c>DuelActorTurnPatch</c>），
/// 而本体的 UI 是按"这一回合是不是额外回合"来选文案的 ——
/// <c>NPlayerTurnBanner</c> 看到参与者名单非空就写 "额外回合"（<c>PLAYER_TURN_EXTRA</c>）。
/// </para>
/// <para>
/// 另外每个玩家回合之后都会走一遍敌方回合（决斗里敌方侧空着，那一回合只是空跑），
/// 于是每回合都会闪一次"敌方回合"。两个都只是文案，改掉即可，不碰任何战斗逻辑。
/// </para>
/// </remarks>
internal static class DuelBanner
{
    /// <summary>决斗里要不要接管横幅。</summary>
    public static bool ShouldOverride()
    {
        return WhiteAlbumSetting.DuelEnabled && DuelState.InDuel;
    }
}
