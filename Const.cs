namespace STS_WhiteAlbum2;

/// <summary>
/// STS_WHITE_ALBUM2 的统一常量。
/// </summary>
/// <remarks>
/// 内容 id 由 RitsuLib 按 <c>&lt;模组id&gt;_&lt;类别&gt;_&lt;类名&gt;</c> 自动生成
/// （例如 <c>STS_WHITE_ALBUM2_CHARACTER_WHITE_ALBUM_TWO_HUNTER</c>），
/// 本地化键就是拿这些 id 拼的 —— 启动日志里会把真实 id 全部打出来，对不上时看日志。
/// </remarks>
internal static class Const
{
    /// <summary>必须与清单 STS_WhiteAlbum2.json 的 id、DLL 名一致。</summary>
    public const string ModId = "STS_WhiteAlbum2";

    // —— 两个空白角色"借"的本体角色 ——
    // 形象、能量计数器、商店/休息处立绘、选人背景与立绘、顶栏图标、地图标记、卡牌轨迹、
    // 多人手势、音效……全部走本体这一套（CharacterAssetProfiles.FromCharacterId）。
    // 这样零缺资源，不会因为裸角色没图而黑屏。
    //
    // 两个空白角色都用**同一个**本体角色（静默猎手）：它们是给后面 two-player 玩法占位的两个槽，
    // 不需要各自不同的形象，借用同一个也最省事、最不容易出资源问题。

    /// <summary>两个空白角色共用的资源来源：本体静默猎手（"猎人"）。</summary>
    public const string HunterSourceId = "silent";
}
