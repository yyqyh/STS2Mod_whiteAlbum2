using STS2RitsuLib;
using STS2RitsuLib.Utils.Persistence;

namespace STS2_WhiteAlbum2.Core.Settings;

/// <summary>本 mod 的通用设置。</summary>
public sealed class AlbumGeneralSettings
{
    /// <summary>单人角色选择界面是否显示 Setsuna / Touma。默认关闭，只在联机出现。</summary>
    public bool CharactersVisibleInSingleplayer { get; set; } = true;
}

/// <summary>通用设置的持久化入口。</summary>
internal static class AlbumGeneralSettingsStore
{
    internal const string DataKey = "general_settings";

    private const string FileName = "STS2_WhiteAlbum2_general_settings.json";

    private static bool _initialized;

    public static AlbumGeneralSettings Current
    {
        get
        {
            Initialize();

            return RitsuLibFramework.GetDataStore(Const.ModId).Get<AlbumGeneralSettings>(DataKey)
                   ?? new AlbumGeneralSettings();
        }
    }

    public static void Update(Action<AlbumGeneralSettings> change)
    {
        Initialize();

        var store = RitsuLibFramework.GetDataStore(Const.ModId);
        store.Modify<AlbumGeneralSettings>(DataKey, settings => change(settings));
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
                defaultFactory: () => new AlbumGeneralSettings(),
                autoCreateIfMissing: true);
        }

        RitsuLibFramework.GetDataStore(Const.ModId).InitializeGlobal();
        _initialized = true;
    }
}
