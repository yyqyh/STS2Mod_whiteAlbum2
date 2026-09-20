using Godot;

using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;

using STS2RitsuLib.Scaffolding.Content.Patches;
using STS2_WhiteAlbum2.Core.Potions;
using STS2_WhiteAlbum2.Core.Relics;

namespace STS2_WhiteAlbum2.Core.Character.Touma;

/// <summary>Touma 的卡池：并入本体静默猎手整套卡兜底，能量图标用自己的。</summary>
public sealed class ToumaCardPool : CardPoolModel, IModBigEnergyIconPool, IModTextEnergyIconPool
{
    public override string Title => "touma";

    public override string EnergyColorName => Const.EnergyColorNameTouma;

    public string? BigEnergyIconPath => Const.Paths.ToumaBigEnergyIcon;

    public string? TextEnergyIconPath => Const.Paths.ToumaTextEnergyIcon;

    public override string CardFrameMaterialPath => "card_frame_green";

    public override Color DeckEntryCardColor => new("6AA6D9");

    public override Color EnergyOutlineColor => new("2E4A8A");

    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() =>
    [
        ..ModelDb.CardPool<SilentCardPool>().AllCards,
    ];
}

/// <summary>Touma 的遗物池。</summary>
public sealed class ToumaRelicPool : RelicPoolModel, IModBigEnergyIconPool, IModTextEnergyIconPool
{
    public override string EnergyColorName => Const.EnergyColorNameTouma;

    public string? BigEnergyIconPath => Const.Paths.ToumaBigEnergyIcon;

    public string? TextEnergyIconPath => Const.Paths.ToumaTextEnergyIcon;

    protected override IEnumerable<RelicModel> GenerateAllRelics() =>
    [
        ModelDb.Relic<AlbumRelicOne>(),
        ModelDb.Relic<AlbumRelicTwo>(),
        ModelDb.Relic<AlbumRelicThree>(),
    ];
}

/// <summary>Touma 的药水池。</summary>
public sealed class ToumaPotionPool : PotionPoolModel, IModBigEnergyIconPool, IModTextEnergyIconPool
{
    public override string EnergyColorName => Const.EnergyColorNameTouma;

    public string? BigEnergyIconPath => Const.Paths.ToumaBigEnergyIcon;

    public string? TextEnergyIconPath => Const.Paths.ToumaTextEnergyIcon;

    protected override IEnumerable<PotionModel> GenerateAllPotions() =>
        [ModelDb.Potion<AlbumPotionOne>()];
}