using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Potions;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS_WhiteAlbum2.Core.Character;

namespace STS_WhiteAlbum2.Core.Potions;

/// <summary>
/// 空白药水：占位，没有任何效果（<see cref="ModPlaceholderPotionTemplate" /> 的默认行为就是无操作）。
/// </summary>
/// <remarks>
/// 存在的意义是让药水奖励与商店有东西可给（药水池空着可能开出空奖励）。
/// 图标借本体已有的药水图集，不需要 mod 自己的美术。
/// </remarks>
[RegisterPotion(typeof(SetsunaPotionPool))]
[RegisterPotion(typeof(ToumaPotionPool))]
public sealed class AlbumPotionOne : ModPlaceholderPotionTemplate
{
    public AlbumPotionOne() : base(PotionRarity.Common, PotionUsage.AnyTime, TargetType.Self)
    {
    }

    public override PotionAssetProfile AssetProfile => new(
        ImagePath: "res://images/atlases/potion_atlas.sprites/block_potion.tres",
        OutlinePath: "res://images/atlases/potion_outline_atlas.sprites/block_potion.tres");
}
