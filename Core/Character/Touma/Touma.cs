using Godot;

using MegaCrit.Sts2.Core.Entities.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;

namespace STS_WhiteAlbum2.Core.Character;

/// <summary>
/// 角色：<b>冬马（Touma）</b>。
/// </summary>
/// <remarks>
/// <para>
/// 选人立绘 / 顶栏图标 / 地图标记先用本 mod 的静态占位 PNG；
/// 战斗立绘、能量计数器、商店与休息处场景继续回退到本体静默猎手。
/// </para>
/// </remarks>
[RegisterCharacter]
public sealed class Touma : ModCharacterTemplate<ToumaCardPool, ToumaRelicPool, ToumaPotionPool>
{
    public override Color NameColor => new("6AA6D9");

    public override Color MapDrawingColor => new("2E4A8A");

    public override int StartingHp => 70;

    public override int StartingGold => 99;

    public override CharacterGender Gender => CharacterGender.Feminine;

    /// <summary>默认用本体静默猎手补全缺失的场景 / 音效资源。</summary>
    public override string? PlaceholderCharacterId => Const.HunterSourceId;

    /// <summary>静态占位美术：选人立绘、顶栏图标、地图标记。</summary>
    public override CharacterAssetProfile AssetProfile => new(
        Ui: new CharacterUiAssetSet(
            IconTexturePath: AlbumCharacterArt.ToumaTopIcon,
            IconOutlineTexturePath: AlbumCharacterArt.ToumaTopIconOutline,
            CharacterSelectIconPath: AlbumCharacterArt.ToumaSelectIcon,
            CharacterSelectLockedIconPath: AlbumCharacterArt.ToumaSelectLockedIcon,
            MapMarkerPath: AlbumCharacterArt.ToumaMapMarker),
        Multiplayer: new CharacterMultiplayerAssetSet(
            ArmPointingTexturePath: AlbumCharacterArt.ToumaArmPoint,
            ArmRockTexturePath: AlbumCharacterArt.ToumaArmRock,
            ArmPaperTexturePath: AlbumCharacterArt.ToumaArmPaper,
            ArmScissorsTexturePath: AlbumCharacterArt.ToumaArmScissors));

    /// <summary>不参与本体的随机角色抽选：随机抽到 mod 角色会绕开 together 的选人规则。</summary>
    public override bool AllowInVanillaRandomCharacterSelect => false;

    public override bool RequiresEpochAndTimeline => false;

    protected override Type? UnlocksAfterRunAsType => null;

    public override float AttackAnimDelay => 0.15f;

    public override float CastAnimDelay => 0.25f;

    public override List<string> GetArchitectAttackVfx() => ["vfx/vfx_attack_slash"];
}
