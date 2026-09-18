using MegaCrit.Sts2.Core.Entities.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_WhiteAlbum2.Core.Ancients;

/// <summary>
/// 空白先古遗物：只占位，不带任何效果。
/// </summary>
/// <remarks>
/// <para>
/// 先古之民的选项是"遗物选项"，所以先古要能正常显示就必须有遗物可给。
/// 这里刻意用<b>没有任何效果</b>的占位遗物：先把流程跑通，避免因为真实遗物的复杂逻辑卡住/黑屏。
/// </para>
/// <para>
/// 图标借本体已有的先古遗物（捏奥那几个），避免 mod 自己的图进不了资源索引时报错。
/// </para>
/// <para>
/// <b>稀有度是刻意分开的</b>：先古之民的遗物本来就不是清一色一个稀有度
/// （本体 <c>EventRelicPool</c> 里既有 <c>Ancient</c> 也有 <c>Event</c>），
/// 所以每个先古给的三件分别是 先古 / 事件 / 稀有，方便在界面上区分是哪一件。
/// 要换组合改子类构造函数里的那一个参数即可。
/// </para>
/// </remarks>
public abstract class BlankAncientRelic : ModPlaceholderRelicTemplate
{
    protected BlankAncientRelic(RelicRarity rarity) : base(rarity)
    {
    }

    /// <summary>用本体 <c>relic_atlas</c> 里已有的遗物图标（传 stem，不带扩展名）。</summary>
    protected static RelicAssetProfile BorrowedIcon(string stem) => new(
        IconPath: $"res://images/atlases/relic_atlas.sprites/{stem}.tres",
        IconOutlinePath: $"res://images/atlases/relic_outline_atlas.sprites/{stem}.tres");
}

// —— 第一幕的三个 ——
[RegisterRelic(typeof(AncientReplaceRelicPool))]
public sealed class ActOneRelicAlpha : BlankAncientRelic
{
    public ActOneRelicAlpha() : base(RelicRarity.Ancient)
    {
    }

    public override RelicAssetProfile AssetProfile => BorrowedIcon("neows_talisman");
}

[RegisterRelic(typeof(AncientReplaceRelicPool))]
public sealed class ActOneRelicBeta : BlankAncientRelic
{
    public ActOneRelicBeta() : base(RelicRarity.Event)
    {
    }

    public override RelicAssetProfile AssetProfile => BorrowedIcon("neows_lament");
}

[RegisterRelic(typeof(AncientReplaceRelicPool))]
public sealed class ActOneRelicGamma : BlankAncientRelic
{
    public ActOneRelicGamma() : base(RelicRarity.Rare)
    {
    }

    public override RelicAssetProfile AssetProfile => BorrowedIcon("neows_bones");
}

// —— 第二幕的三个 ——
[RegisterRelic(typeof(AncientReplaceRelicPool))]
public sealed class ActTwoRelicAlpha : BlankAncientRelic
{
    public ActTwoRelicAlpha() : base(RelicRarity.Ancient)
    {
    }

    public override RelicAssetProfile AssetProfile => BorrowedIcon("neows_sacrifice");
}

[RegisterRelic(typeof(AncientReplaceRelicPool))]
public sealed class ActTwoRelicBeta : BlankAncientRelic
{
    public ActTwoRelicBeta() : base(RelicRarity.Event)
    {
    }

    public override RelicAssetProfile AssetProfile => BorrowedIcon("neows_torment");
}

[RegisterRelic(typeof(AncientReplaceRelicPool))]
public sealed class ActTwoRelicGamma : BlankAncientRelic
{
    public ActTwoRelicGamma() : base(RelicRarity.Rare)
    {
    }

    public override RelicAssetProfile AssetProfile => BorrowedIcon("neows_talisman");
}

// —— 第三幕的三个 ——
[RegisterRelic(typeof(AncientReplaceRelicPool))]
public sealed class ActThreeRelicAlpha : BlankAncientRelic
{
    public ActThreeRelicAlpha() : base(RelicRarity.Ancient)
    {
    }

    public override RelicAssetProfile AssetProfile => BorrowedIcon("neows_lament");
}

[RegisterRelic(typeof(AncientReplaceRelicPool))]
public sealed class ActThreeRelicBeta : BlankAncientRelic
{
    public ActThreeRelicBeta() : base(RelicRarity.Event)
    {
    }

    public override RelicAssetProfile AssetProfile => BorrowedIcon("neows_bones");
}

[RegisterRelic(typeof(AncientReplaceRelicPool))]
public sealed class ActThreeRelicGamma : BlankAncientRelic
{
    public ActThreeRelicGamma() : base(RelicRarity.Rare)
    {
    }

    public override RelicAssetProfile AssetProfile => BorrowedIcon("neows_sacrifice");
}

/// <summary>本 mod 的遗物池：空白先古遗物都挂在这里（不混进本体池，免得污染正常掉落）。</summary>
public sealed class AncientReplaceRelicPool : TypeListRelicPoolModel
{
    /// <summary>无色（先古遗物天然是无色的），图标直接用本体已有的那套。</summary>
    public override string EnergyColorName => "colorless";
}
