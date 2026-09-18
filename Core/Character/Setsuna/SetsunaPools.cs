using Godot;

using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace STS_WhiteAlbum2.Core.Character;

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
        ModelDb.Card<Cards.SetsunaTempBasicAttack1>(),
        ModelDb.Card<Cards.SetsunaTempBasicAttack2>(),
        ModelDb.Card<Cards.SetsunaTempBasicAttack3>(),
        ModelDb.Card<Cards.SetsunaTempBasicSkill1>(),
        ModelDb.Card<Cards.SetsunaTempBasicSkill2>(),
        ModelDb.Card<Cards.SetsunaTempBasicSkill3>(),
        ModelDb.Card<Cards.SetsunaTempCommonAttack1>(),
        ModelDb.Card<Cards.SetsunaTempCommonAttack2>(),
        ModelDb.Card<Cards.SetsunaTempCommonAttack3>(),
        ModelDb.Card<Cards.SetsunaTempCommonSkill1>(),
        ModelDb.Card<Cards.SetsunaTempCommonSkill2>(),
        ModelDb.Card<Cards.SetsunaTempCommonSkill3>(),
        ModelDb.Card<Cards.SetsunaTempCommonPower1>(),
        ModelDb.Card<Cards.SetsunaTempCommonPower2>(),
        ModelDb.Card<Cards.SetsunaTempCommonPower3>(),
        ModelDb.Card<Cards.SetsunaTempUncommonAttack1>(),
        ModelDb.Card<Cards.SetsunaTempUncommonAttack2>(),
        ModelDb.Card<Cards.SetsunaTempUncommonAttack3>(),
        ModelDb.Card<Cards.SetsunaTempUncommonSkill1>(),
        ModelDb.Card<Cards.SetsunaTempUncommonSkill2>(),
        ModelDb.Card<Cards.SetsunaTempUncommonSkill3>(),
        ModelDb.Card<Cards.SetsunaTempUncommonPower1>(),
        ModelDb.Card<Cards.SetsunaTempUncommonPower2>(),
        ModelDb.Card<Cards.SetsunaTempUncommonPower3>(),
        ModelDb.Card<Cards.SetsunaTempRareAttack1>(),
        ModelDb.Card<Cards.SetsunaTempRareAttack2>(),
        ModelDb.Card<Cards.SetsunaTempRareAttack3>(),
        ModelDb.Card<Cards.SetsunaTempRareSkill1>(),
        ModelDb.Card<Cards.SetsunaTempRareSkill2>(),
        ModelDb.Card<Cards.SetsunaTempRareSkill3>(),
        ModelDb.Card<Cards.SetsunaTempRarePower1>(),
        ModelDb.Card<Cards.SetsunaTempRarePower2>(),
        ModelDb.Card<Cards.SetsunaTempRarePower3>(),
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
