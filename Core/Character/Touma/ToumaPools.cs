using Godot;

using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace STS_WhiteAlbum2.Core.Character;

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
        ModelDb.Card<Cards.ToumaTempBasicAttack1>(),
        ModelDb.Card<Cards.ToumaTempBasicAttack2>(),
        ModelDb.Card<Cards.ToumaTempBasicAttack3>(),
        ModelDb.Card<Cards.ToumaTempBasicSkill1>(),
        ModelDb.Card<Cards.ToumaTempBasicSkill2>(),
        ModelDb.Card<Cards.ToumaTempBasicSkill3>(),
        ModelDb.Card<Cards.ToumaTempCommonAttack1>(),
        ModelDb.Card<Cards.ToumaTempCommonAttack2>(),
        ModelDb.Card<Cards.ToumaTempCommonAttack3>(),
        ModelDb.Card<Cards.ToumaTempCommonSkill1>(),
        ModelDb.Card<Cards.ToumaTempCommonSkill2>(),
        ModelDb.Card<Cards.ToumaTempCommonSkill3>(),
        ModelDb.Card<Cards.ToumaTempCommonPower1>(),
        ModelDb.Card<Cards.ToumaTempCommonPower2>(),
        ModelDb.Card<Cards.ToumaTempCommonPower3>(),
        ModelDb.Card<Cards.ToumaTempUncommonAttack1>(),
        ModelDb.Card<Cards.ToumaTempUncommonAttack2>(),
        ModelDb.Card<Cards.ToumaTempUncommonAttack3>(),
        ModelDb.Card<Cards.ToumaTempUncommonSkill1>(),
        ModelDb.Card<Cards.ToumaTempUncommonSkill2>(),
        ModelDb.Card<Cards.ToumaTempUncommonSkill3>(),
        ModelDb.Card<Cards.ToumaTempUncommonPower1>(),
        ModelDb.Card<Cards.ToumaTempUncommonPower2>(),
        ModelDb.Card<Cards.ToumaTempUncommonPower3>(),
        ModelDb.Card<Cards.ToumaTempRareAttack1>(),
        ModelDb.Card<Cards.ToumaTempRareAttack2>(),
        ModelDb.Card<Cards.ToumaTempRareAttack3>(),
        ModelDb.Card<Cards.ToumaTempRareSkill1>(),
        ModelDb.Card<Cards.ToumaTempRareSkill2>(),
        ModelDb.Card<Cards.ToumaTempRareSkill3>(),
        ModelDb.Card<Cards.ToumaTempRarePower1>(),
        ModelDb.Card<Cards.ToumaTempRarePower2>(),
        ModelDb.Card<Cards.ToumaTempRarePower3>(),
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
