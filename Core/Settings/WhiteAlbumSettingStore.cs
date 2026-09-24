using STS2RitsuLib;
using STS2RitsuLib.Utils.Persistence;

namespace STS2_WhiteAlbum2.Core.Settings;

/// <summary>设置的持久化入口（RitsuLib 数据存储）。</summary>
/// <remarks>注册必须在 <c>BeginModDataRegistration</c> 作用域里做且只做一次；<see cref="Initialize" /> 幂等。</remarks>
internal static class WhiteAlbumSettingStore
{
    internal const string DataKey = "settings";

    private const string FileName = "STS2_WhiteAlbum2_settings.json";

    private static bool _initialized;

    /// <summary>当前设置（每次现取，所以设置界面一改就立刻生效）。</summary>
    public static WhiteAlbumSetting Current
    {
        get
        {
            Initialize();

            return RitsuLibFramework.GetDataStore(Const.ModId).Get<WhiteAlbumSetting>(DataKey)
                   ?? new WhiteAlbumSetting();
        }
    }

    /// <summary>改一个字段并立刻落盘。</summary>
    public static void Update(Action<WhiteAlbumSetting> change)
    {
        Initialize();

        var store = RitsuLibFramework.GetDataStore(Const.ModId);
        store.Modify<WhiteAlbumSetting>(DataKey, settings => change(settings));
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
                defaultFactory: () => new WhiteAlbumSetting(),
                autoCreateIfMissing: true);
        }

        RitsuLibFramework.GetDataStore(Const.ModId).InitializeGlobal();
        _initialized = true;
    }
}
