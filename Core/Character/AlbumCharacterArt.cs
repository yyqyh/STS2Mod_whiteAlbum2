namespace STS2_WhiteAlbum2.Core.Character;

/// <summary>
/// 两个角色的静态占位美术路径。
/// </summary>
/// <remarks>
/// 这些 PNG 先在工程里放最小占位图，后面直接覆盖同名文件即可；
/// 战斗立绘 / 能量计数器 / 商店与休息处场景仍然回退到本体静默猎手，避免缺场景黑屏。
/// </remarks>
internal static class AlbumCharacterArt
{
    public const string Root = "res://STS2_WhiteAlbum2/images/character";

    public const string SetsunaRoot = Root + "/setsuna";
    public const string ToumaRoot = Root + "/touma";

    public const string SetsunaSelectIcon = SetsunaRoot + "/character_select_setsuna.png";
    public const string SetsunaSelectLockedIcon = SetsunaRoot + "/character_select_setsuna_locked.png";
    public const string SetsunaTopIcon = SetsunaRoot + "/character_icon_setsuna.png";
    public const string SetsunaTopIconOutline = SetsunaRoot + "/character_icon_setsuna_outline.png";
    public const string SetsunaMapMarker = SetsunaRoot + "/map_marker_setsuna.png";
    public const string SetsunaSelectBg = SetsunaRoot + "/select_bg.png";
    public const string SetsunaSelectBgScene = "res://STS2_WhiteAlbum2/scenes/char_select/select_bg_setsuna.tscn";
    public const string SetsunaCombatPortrait = SetsunaRoot + "/character.png";
    public const string SetsunaEnergyIcon = SetsunaRoot + "/energy_icon.png";
    public const string SetsunaShop = SetsunaRoot + "/shop.png";
    public const string SetsunaRest = SetsunaRoot + "/rest.png";
    public const string SetsunaTrail = SetsunaRoot + "/card_trail.png";
    public const string SetsunaArmPoint = SetsunaRoot + "/arm/point.png";
    public const string SetsunaArmRock = SetsunaRoot + "/arm/rock.png";
    public const string SetsunaArmPaper = SetsunaRoot + "/arm/paper.png";
    public const string SetsunaArmScissors = SetsunaRoot + "/arm/scissors.png";

    public const string ToumaSelectIcon = ToumaRoot + "/character_select_touma.png";
    public const string ToumaSelectLockedIcon = ToumaRoot + "/character_select_touma_locked.png";
    public const string ToumaTopIcon = ToumaRoot + "/character_icon_touma.png";
    public const string ToumaTopIconOutline = ToumaRoot + "/character_icon_touma_outline.png";
    public const string ToumaMapMarker = ToumaRoot + "/map_marker_touma.png";
    public const string ToumaSelectBg = ToumaRoot + "/select_bg.png";
    public const string ToumaSelectBgScene = "res://STS2_WhiteAlbum2/scenes/char_select/select_bg_touma.tscn";
    public const string ToumaCombatPortrait = ToumaRoot + "/character.png";
    public const string ToumaEnergyIcon = ToumaRoot + "/energy_icon.png";
    public const string ToumaShop = ToumaRoot + "/shop.png";
    public const string ToumaRest = ToumaRoot + "/rest.png";
    public const string ToumaTrail = ToumaRoot + "/card_trail.png";
    public const string ToumaArmPoint = ToumaRoot + "/arm/point.png";
    public const string ToumaArmRock = ToumaRoot + "/arm/rock.png";
    public const string ToumaArmPaper = ToumaRoot + "/arm/paper.png";
    public const string ToumaArmScissors = ToumaRoot + "/arm/scissors.png";
}
