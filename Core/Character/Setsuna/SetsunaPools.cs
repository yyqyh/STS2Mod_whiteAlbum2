using Godot;

using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;

using STS2RitsuLib.Scaffolding.Content.Patches;
using STS2_WhiteAlbum2.Core.Potions;
using STS2_WhiteAlbum2.Core.Relics;

namespace STS2_WhiteAlbum2.Core.Character.Setsuna;

/// <summary>Setsuna 的卡池：并入本体静默猎手整套卡兜底，能量图标用自己的。</summary>
public sealed class SetsunaCardPool : CardPoolModel, IModBigEnergyIconPool, IModTextEnergyIconPool
{
    public override string Title => "setsuna";

    public override string EnergyColorName => Const.EnergyColorNameSetsuna;

    public string? BigEnergyIconPath => Const.Paths.SetsunaBigEnergyIcon;

    public string? TextEnergyIconPath => Const.Paths.SetsunaTextEnergyIcon;

    public override string CardFrameMaterialPath => "card_frame_green";

    public override Color DeckEntryCardColor => new("028080");

    public override Color EnergyOutlineColor => new("8A2E4A");

    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() =>
    [
        ..ModelDb.CardPool<SilentCardPool>().AllCards,
    ];
}

/// <summary>Setsuna 的遗物池。</summary>
public sealed class SetsunaRelicPool : RelicPoolModel, IModBigEnergyIconPool, IModTextEnergyIconPool
{
    public override string EnergyColorName => Const.EnergyColorNameSetsuna;

    public string? BigEnergyIconPath => Const.Paths.SetsunaBigEnergyIcon;

    public string? TextEnergyIconPath => Const.Paths.SetsunaTextEnergyIcon;

    protected override IEnumerable<RelicModel> GenerateAllRelics() =>
    [
        ModelDb.Relic<AlbumRelicOne>(),
        ModelDb.Relic<AlbumRelicTwo>(),
        ModelDb.Relic<AlbumRelicThree>(),
    ];
}

/// <summary>Setsuna 的药水池。</summary>
public sealed class SetsunaPotionPool : PotionPoolModel, IModBigEnergyIconPool, IModTextEnergyIconPool
{
    public override string EnergyColorName => Const.EnergyColorNameSetsuna;

    public string? BigEnergyIconPath => Const.Paths.SetsunaBigEnergyIcon;

    public string? TextEnergyIconPath => Const.Paths.SetsunaTextEnergyIcon;

    protected override IEnumerable<PotionModel> GenerateAllPotions() =>
        [ModelDb.Potion<AlbumPotionOne>()];
}