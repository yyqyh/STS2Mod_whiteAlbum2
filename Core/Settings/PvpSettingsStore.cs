using STS2RitsuLib;
using STS2RitsuLib.Utils.Persistence;

namespace STS_WhiteAlbum2.Core.Pvp;

/// <summary>
/// 设置的持久化入口（RitsuLib 数据存储）。
/// </summary>
/// <remarks>
/// 注册必须在 <c>BeginModDataRegistration</c> 作用域里做（RitsuLib 用它把存储归到本 mod 名下），
/// 而且只做一次；<see cref="Initialize" /> 是幂等的。
/// </remarks>
internal static class PvpSettingsStore
{
    internal const string DataKey = "pvp_settings";

    private const string FileName = "STS_WhiteAlbum2_pvp_settings.json";

    private static bool _initialized;

    /// <summary>当前设置（每次都从存储里取，保证「设置界面一改就生效」）。</summary>
    public static PvpSettings Current
    {
        get
        {
            Initialize();

            return RitsuLibFramework.GetDataStore(Const.ModId).Get<PvpSettings>(DataKey)
                   ?? new PvpSettings();
        }
    }

    /// <summary>改一个字段并立刻落盘。</summary>
    public static void Update(Action<PvpSettings> change)
    {
        Initialize();

        var store = RitsuLibFramework.GetDataStore(Const.ModId);
        store.Modify<PvpSettings>(DataKey, settings => change(settings));
        store.Save(DataKey);
    }

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        using (RitsuLibFramework.BeginModDataRegistration(Const.ModId, false))
        {
            RitsuLibFramework.GetDataStore(Const.ModId).Register(
                DataKey,
                FileName,
                SaveScope.Global,
                defaultFactory: () => new PvpSettings(),
                autoCreateIfMissing: true);
        }

        RitsuLibFramework.GetDataStore(Const.ModId).InitializeGlobal();
        _initialized = true;
    }
}
