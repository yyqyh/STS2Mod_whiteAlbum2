using Godot;

using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace STS2_WhiteAlbum2.Core.Character;

/// <summary>Touma 的卡池 / 遗物池 / 药水池。</summary>
public sealed class ToumaCardPool : CardPoolModel
{
    public override string Title => "touma";

    public override string EnergyColorName => Const.HunterSourceId;

    public override string CardFrameMaterialPath => "card_frame_green";

    public override Color DeckEntryCardColor => new("6AA6D9");

    public override Color EnergyOutlineColor => new("2E4A8A");

    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() =>
    [
        ModelDb.Card<Cards.ToumaCardOne>(),
        ModelDb.Card<Cards.ToumaCardTwo>(),
        ModelDb.Card<Cards.ToumaCardThree>(),
        ModelDb.Card<Cards.ToumaCardFour>(),
        ModelDb.Card<Cards.ToumaCardFive>(),
        ..ModelDb.CardPool<SilentCardPool>().AllCards,
    ];
}

/// <summary>Touma 的遗物池。</summary>
public sealed class ToumaRelicPool : RelicPoolModel
{
    public override string EnergyColorName => Const.HunterSourceId;

    protected override IEnumerable<RelicModel> GenerateAllRelics() =>
    [
        ModelDb.Relic<Relics.AlbumRelicOne>(),
        ModelDb.Relic<Relics.AlbumRelicTwo>(),
        ModelDb.Relic<Relics.AlbumRelicThree>(),
    ];
}

/// <summary>Touma 的药水池。</summary>
public sealed class ToumaPotionPool : PotionPoolModel
{
    public override string EnergyColorName => Const.HunterSourceId;

    protected override IEnumerable<PotionModel> GenerateAllPotions() =>
        [ModelDb.Potion<Potions.AlbumPotionOne>()];
}
