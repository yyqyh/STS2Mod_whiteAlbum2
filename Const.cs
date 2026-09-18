using MegaCrit.Sts2.Core.Logging;

using STS2RitsuLib;

namespace STS2_WhiteAlbum2;

/// <summary>本 mod 的统一常量。</summary>
/// <remarks>内容 id 由 RitsuLib 按 <c>&lt;模组id&gt;_&lt;类别&gt;_&lt;类名&gt;</c> 自动生成，本地化键就是拿这些 id 拼的。</remarks>
internal static class Const
{
    /// <summary>必须与清单 STS2_WhiteAlbum2.json 的 id、DLL 名一致。</summary>
    public const string ModId = "STS2_WhiteAlbum2";

    /// <summary>显示用名称。</summary>
    public const string Name = "STS2_WhiteAlbum2";

    /// <summary>版本号，与清单 version 保持一致。</summary>
    public const string Version = "0.1.0";

    /// <summary>卡池用的能量颜色标识。</summary>
    public const string EnergyColorName = "Together";

    /// <summary>两个角色共用的资源来源：本体静默猎手（"猎人"）。</summary>
    public const string HunterSourceId = "silent";

    /// <summary>惰性创建的日志器，前缀就是模组 id。</summary>
    public static Logger Logger
    {
        get
        {
            _logger ??= RitsuLibFramework.CreateLogger(ModId);
            return _logger;
        }
    }

    private static Logger? _logger;
}