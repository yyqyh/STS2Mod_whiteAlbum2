using STS2RitsuLib.Scaffolding.Content;

namespace STS_WhiteAlbum2.Core.Ancients;

/// <summary>
/// 表现资源一律"借本体捏奥的"。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么全借本体的资源</b>：自定义内容最容易翻车的地方不是逻辑，而是"资源不存在"——
/// 先古事件要加载背景场景、地图节点要加载图标、boss 节点要加载 <c>BossNodePath + ".png"</c>。
/// 这些路径一旦指向 mod 自己的图（或根本不存在）
/// 就会在预加载/进房间时抛异常，表现就是<b>卡住或黑屏</b>。
/// </para>
/// <para>本体资源一定在资源索引里，所以借用是最省事、最不容易出事的路子。</para>
/// </remarks>
internal static class NeowAssets
{
    /// <summary>先古事件的背景场景（本体 Neow 用的那个）。</summary>
    public const string BackgroundScene = "res://scenes/events/background_scenes/neow.tscn";

    /// <summary>地图节点图标（本体捏奥的两张 png）。</summary>
    public const string MapIcon = "res://images/packed/map/ancients/ancient_node_neow.png";

    public const string MapIconOutline = "res://images/packed/map/ancients/ancient_node_neow_outline.png";

    /// <summary>
    /// boss 地图节点用的"无扩展名 stem"。
    /// </summary>
    /// <remarks>
    /// <c>NBossMapPoint</c> 在 <c>BossNodeSpineResource == null</c> 时会去读
    /// <c>BossNodePath + ".png"</c> 和 <c>BossNodePath + "_outline.png"</c>，
    /// 所以这里给不带扩展名的 stem，正好落到捏奥那两张图上。
    /// </remarks>
    public const string MapNodeStem = "res://images/packed/map/ancients/ancient_node_neow";

    /// <summary>历史记录 / 房间图标。</summary>
    public const string RunHistoryIcon = "res://images/ui/run_history/neow.png";

    public const string RunHistoryIconOutline = "res://images/ui/run_history/neow_outline.png";

    /// <summary>先古事件的表现资源（只换背景场景，其余走本体默认）。</summary>
    public static EventAssetProfile EventProfile => new(BackgroundScenePath: BackgroundScene);

    /// <summary>先古在地图/历史记录上的图标。</summary>
    public static AncientEventPresentationAssetProfile PresentationProfile => new(
        MapIconPath: MapIcon,
        MapIconOutlinePath: MapIconOutline,
        RunHistoryIconPath: RunHistoryIcon,
        RunHistoryIconOutlinePath: RunHistoryIconOutline);
}
