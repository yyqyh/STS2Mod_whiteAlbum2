using Godot;

using MegaCrit.Sts2.Core.Entities.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;

namespace STS2_WhiteAlbum2.Core.Character.Setsuna;

/// <summary>
/// 角色：<b>雪菜（Setsuna）</b>。
/// </summary>
/// <remarks>
/// <para>
/// 选人立绘 / 顶栏图标 / 地图标记先用本 mod 的静态占位 PNG；
/// 战斗立绘、能量计数器、商店与休息处场景继续回退到本体静默猎手。
/// </para>
/// <para>
/// <b>RequiresEpochAndTimeline = false</b>：本体的进度流程会去找 <c>*_EPOCH</c> 之类的数据，
/// 空白角色没有，所以直接退出那套假设（酒狐的第二个角色也是这么写的）。
/// </para>
/// <para>
/// 初始卡组与初始遗物走 RitsuLib 的 starter 注册（见 Cards / Relics）。
/// </para>
/// </remarks>
[RegisterCharacter]
public sealed class Setsuna : ModCharacterTemplate<SetsunaCardPool, SetsunaRelicPool, SetsunaPotionPool>
{
    public override Color NameColor => new("D96A8A");

    public override Color MapDrawingColor => new("8A2E4A");

    public override int StartingHp => 70;

    public override int StartingGold => 99;

    public override CharacterGender Gender => CharacterGender.Feminine;
    

    /// <summary>静态占位美术：选人立绘、顶栏图标、地图标记。</summary>
    public override CharacterAssetProfile AssetProfile => new(
        Scenes: new CharacterSceneAssetSet(
            EnergyCounterPath: "res://STS2_WhiteAlbum2/scenes/ui/energy_counters/setsuna_energy_counter.tscn"),
        Ui: new CharacterUiAssetSet(
            IconTexturePath: AlbumCharacterArt.SetsunaTopIcon,
            IconOutlineTexturePath: AlbumCharacterArt.SetsunaTopIconOutline,
            IconPath: AlbumCharacterArt.SetsunaTopIcon,
            CharacterSelectBgPath: AlbumCharacterArt.SetsunaSelectBgScene,
            CharacterSelectIconPath: AlbumCharacterArt.SetsunaSelectIcon,
            CharacterSelectLockedIconPath: AlbumCharacterArt.SetsunaSelectLockedIcon,
            MapMarkerPath: AlbumCharacterArt.SetsunaMapMarker),
        Multiplayer: new CharacterMultiplayerAssetSet(
            ArmPointingTexturePath: AlbumCharacterArt.SetsunaArmPoint,
            ArmRockTexturePath: AlbumCharacterArt.SetsunaArmRock,
            ArmPaperTexturePath: AlbumCharacterArt.SetsunaArmPaper,
            ArmScissorsTexturePath: AlbumCharacterArt.SetsunaArmScissors));

    /// <summary>默认用本体静默猎手补全缺失的场景 / 音效资源。</summary>
    public override string? PlaceholderCharacterId => Const.HunterSourceId;
  
    
    /// <summary>不参与本体 epoch / timeline 进度假设。</summary>
    public override bool RequiresEpochAndTimeline => false;

    /// <summary>不参与本体的随机角色抽选：随机抽到 mod 角色会绕开 together 的选人规则。</summary>
    public override bool AllowInVanillaRandomCharacterSelect => false;

    /// <summary>没有前置解锁角色。</summary>
    protected override Type? UnlocksAfterRunAsType => null;

    public override float AttackAnimDelay => 0.15f;

    public override float CastAnimDelay => 0.25f;

    public override List<string> GetArchitectAttackVfx() => ["vfx/vfx_attack_slash"];
}
