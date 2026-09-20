using MegaCrit.Sts2.Core.Entities.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_WhiteAlbum2.Core.Character;
using STS2_WhiteAlbum2.Core.Character.Setsuna;
using STS2_WhiteAlbum2.Core.Character.Touma;

namespace STS2_WhiteAlbum2.Core.Relics;

/// <summary>
/// 空白遗物：只占位，不带任何效果。
/// </summary>
/// <remarks>
/// <para>
/// 三张分别占住 Starter / Common / Rare：初始遗物用第一张，
/// 另外两张存在的意义是让遗物奖励与商店"有货可给"（按稀有度抽，池子空着容易开出空奖励）。
/// </para>
/// <para>图标借本体已有的遗物图集（一定在资源索引里），不需要 mod 自己的美术。</para>
/// </remarks>
public abstract class BlankAlbumRelic : ModPlaceholderRelicTemplate
{
    protected BlankAlbumRelic(RelicRarity rarity) : base(rarity)
    {
    }

    protected static RelicAssetProfile BorrowedIcon(string stem) => new(
        IconPath: $"res://images/atlases/relic_atlas.sprites/{stem}.tres",
        IconOutlinePath: $"res://images/atlases/relic_outline_atlas.sprites/{stem}.tres");
}

/// <summary>初始遗物（两个角色共用，starter 注册写两遍）。</summary>
[RegisterRelic(typeof(SetsunaRelicPool))]
[RegisterCharacterStarterRelic(typeof(Setsuna), Order = 0)]
[RegisterCharacterStarterRelic(typeof(Touma), Order = 0)]
public sealed class AlbumRelicOne : BlankAlbumRelic
{
    public AlbumRelicOne() : base(RelicRarity.Starter)
    {
    }

    public override RelicAssetProfile AssetProfile => BorrowedIcon("neows_talisman");
}

/// <summary>普通稀有度占位遗物（补遗物奖励/商店）。</summary>
[RegisterRelic(typeof(SetsunaRelicPool))]
public sealed class AlbumRelicTwo : BlankAlbumRelic
{
    public AlbumRelicTwo() : base(RelicRarity.Common)
    {
    }

    public override RelicAssetProfile AssetProfile => BorrowedIcon("neows_bones");
}

/// <summary>稀有稀有度占位遗物（补遗物奖励/商店）。</summary>
[RegisterRelic(typeof(SetsunaRelicPool))]
public sealed class AlbumRelicThree : BlankAlbumRelic
{
    public AlbumRelicThree() : base(RelicRarity.Rare)
    {
    }

    public override RelicAssetProfile AssetProfile => BorrowedIcon("neows_lament");
}
