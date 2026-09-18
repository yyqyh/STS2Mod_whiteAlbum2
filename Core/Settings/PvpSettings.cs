namespace STS2_WhiteAlbum2.Core.Pvp;

/// <summary>
/// 决斗 mod 的持久化设置。
/// </summary>
/// <remarks>
/// <b>加字段的约定</b>：给新字段一个合理的默认值即可，不用写迁移 ——
/// 老配置反序列化出来缺字段时会取这个默认值。
/// </remarks>
public sealed class PvpSettings
{
    /// <summary>是否开启决斗模式（开启后联机的战斗会变成与队友的决斗）。</summary>
    public bool Enabled { get; set; }

    /// <summary>调试总开关：关着时调试用途的项一律不生效。</summary>
    public bool Debug { get; set; } = true;

    /// <summary>是否生成「对决邀请」事件（关掉之后本 mod 的事件入口完全不出现）。</summary>
    public bool EventEnabled { get; set; } = true;

    /// <summary>
    /// 调试：把事件固定在<b>本局第一个问号房</b>。
    /// 关掉时走正式流程 —— 在三层的最终 boss 打完、本体要进结局事件那一刻生成。
    /// </summary>
    public bool EventDebugFirstQuestion { get; set; } = true;

    /// <summary>回合上限：打满这么多回合还没分出胜负，就按剩余血量比例判定。</summary>
    public int MaxRounds { get; set; } = 30;
}
