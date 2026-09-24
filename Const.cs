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

    /// <summary>
    /// 版本号：<b>必须与清单 <c>STS2_WhiteAlbum2.json</c> 的 <c>version</c> 一致</b>。
    /// </summary>
    /// <remarks>
    /// 清单是游戏真正读的那份，这里的副本只用于日志（<c>Main.Initialize</c> 里那行 "initialized v…"）
    /// 和别处要显示版本的地方 —— 启动日志和模组列表里的版本号对不上就是这两处漏改了一个。
    /// </remarks>
    public const string Version = "0.4.0";

    /// <summary>卡池用的能量颜色标识。</summary>
    public const string EnergyColorNameSetsuna = "SetsunaEnergyColor";
    public const string EnergyColorNameTouma = "ToumaEnergyColor";


    /// <summary>两个角色共用的资源来源：本体静默猎手（"猎人"）。</summary>
    public const string HunterSourceId = "silent";

    public static class Paths
    {
        public const string Root = "res://STS2_WhiteAlbum2";
        public const string ScenesRoot = Root + "/scenes";
        
        
        public const string SetsunaBigEnergyIcon = Root + "/images/character/setsuna/energy_icon_big.png";
        public const string SetsunaTextEnergyIcon = Root + "/images/character/setsuna/energy_icon.png";
        public const string ToumaBigEnergyIcon = Root + "/images/character/touma/energy_icon_big.png";
        public const string ToumaTextEnergyIcon = Root + "/images/character/touma/energy_icon.png";

        
    }



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
