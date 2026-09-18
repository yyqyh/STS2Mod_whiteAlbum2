using STS2RitsuLib.Scaffolding.Content;

namespace STS2_WhiteAlbum2.Core.Cards;

/// <summary>占位卡面：先借本体静默猎手的 <c>card_atlas</c>。</summary>
internal static class AlbumCardArt
{
    public static CardAssetProfile Silent(string stem) => new(
        PortraitPath: $"res://images/atlases/card_atlas.sprites/silent/{stem}.tres");
}
