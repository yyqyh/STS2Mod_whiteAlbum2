using Godot;

using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace STS2_WhiteAlbum2.Core.Character;

/// <summary>Setsuna 的卡池 / 遗物池 / 药水池。</summary>
public sealed class SetsunaCardPool : CardPoolModel
{
    public override string Title => "setsuna";

    public override string EnergyColorName => Const.HunterSourceId;

    public override string CardFrameMaterialPath => "card_frame_green";

    public override Color DeckEntryCardColor => new("D96A8A");

    public override Color EnergyOutlineColor => new("8A2E4A");

    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() =>
    [
        ModelDb.Card<Cards.SetsunaCardOne>(),
        ModelDb.Card<Cards.SetsunaCardTwo>(),
        ModelDb.Card<Cards.SetsunaCardThree>(),
        ModelDb.Card<Cards.SetsunaCardFour>(),
        ModelDb.Card<Cards.SetsunaCardFive>(),
        ..ModelDb.CardPool<SilentCardPool>().AllCards,
    ];
}

/// <summary>Setsuna 的遗物池。</summary>
public sealed class SetsunaRelicPool : RelicPoolModel
{
    public override string EnergyColorName => Const.HunterSourceId;

    protected override IEnumerable<RelicModel> GenerateAllRelics() =>
    [
        ModelDb.Relic<Relics.AlbumRelicOne>(),
        ModelDb.Relic<Relics.AlbumRelicTwo>(),
        ModelDb.Relic<Relics.AlbumRelicThree>(),
    ];
}

/// <summary>Setsuna 的药水池。</summary>
public sealed class SetsunaPotionPool : PotionPoolModel
{
    public override string EnergyColorName => Const.HunterSourceId;

    protected override IEnumerable<PotionModel> GenerateAllPotions() =>
        [ModelDb.Potion<Potions.AlbumPotionOne>()];
}
