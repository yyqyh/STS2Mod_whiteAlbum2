using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace STS2_WhiteAlbum2.Core.Cards.Setsuna;

public abstract class SetsunaCard( int baseCost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true) : ModCardTemplate(baseCost, type, rarity, target, showInCardLibrary)
{
    protected static CardAssetProfile Art(string portraitPath)
    {
        return new(portraitPath, portraitPath);
    }
}