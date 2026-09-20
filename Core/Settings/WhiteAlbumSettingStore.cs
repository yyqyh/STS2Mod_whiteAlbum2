using STS2RitsuLib;
using STS2RitsuLib.Utils.Persistence;

using STS2_WhiteAlbum2.Core.Combat.Together;

namespace STS2_WhiteAlbum2.Core.Settings;

/// <summary>设置的持久化入口（RitsuLib 数据存储）。</summary>
/// <remarks>注册必须在 <c>BeginModDataRegistration</c> 作用域里做且只做一次；<see cref="Initialize" /> 幂等。</remarks>
internal static class WhiteAlbumSettingStore
{
    internal const string DataKey = "settings";

    /// <summary>共生体存档登记最多保留多少条（按插入顺序淘汰最旧）。</summary>
    public const int SymbioticRunHistory = 10;

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

    // ---- 单字段快捷读取：联机时功能代码应改读 TogetherSettingsSync 的 Effective* ----

    /// <summary>本机设置的"共生体开关"。</summary>
    public static bool SymbiosisEnabled => Current.SymbiosisEnabled;

    /// <summary>本机设置的"开局合并初始卡组"。</summary>
    public static bool MergeStarterDecks => Current.MergeStarterDecks;

    /// <summary>本机设置的"血量上限提升百分比"（夹到 0~100）。</summary>
    public static int HpBonusPercent => Math.Clamp(Current.HpBonusPercent, 0, 100);

    /// <summary>本机设置的"共生体人数上限"（夹到 2~4）。</summary>
    public static int GroupSize => Math.Clamp(Current.GroupSize, TogetherPair.MinMembers, TogetherPair.MaxMembers);

    /// <summary>本机设置的"是否共享金币"。</summary>
    public static bool ShareGold => Current.ShareGold;

    /// <summary>改一个字段并立刻落盘。</summary>
    public static void Update(Action<WhiteAlbumSetting> change)
    {
        Initialize();

        var store = RitsuLibFramework.GetDataStore(Const.ModId);
        store.Modify<WhiteAlbumSetting>(DataKey, settings => change(settings));
        store.Save(DataKey);
    }

    /// <summary>登记"这个种子是共生体局，成员是这些 netId"。</summary>
    public static void RememberSymbioticRun(string? seed, IEnumerable<ulong> memberIds)
    {
        if (string.IsNullOrWhiteSpace(seed))
        {
            return;
        }

        Initialize();

        var value = string.Join(",", memberIds);
        var store = RitsuLibFramework.GetDataStore(Const.ModId);

        store.Modify<WhiteAlbumSetting>(DataKey, settings =>
        {
            settings.SymbioticRuns ??= [];
            settings.SymbioticRuns[seed] = value;

            while (settings.SymbioticRuns.Count > SymbioticRunHistory)
            {
                settings.SymbioticRuns.Remove(settings.SymbioticRuns.Keys.First());
            }
        });

        store.Save(DataKey);
    }

    /// <summary>读档时按种子找回共生体成员；没登记过返回 <c>null</c>。</summary>
    public static ulong[]? FindSymbioticRun(string? seed)
    {
        if (string.IsNullOrWhiteSpace(seed))
        {
            return null;
        }

        Initialize();

        var settings = RitsuLibFramework.GetDataStore(Const.ModId).Get<WhiteAlbumSetting>(DataKey);
        if (settings?.SymbioticRuns is not { } map
            || !map.TryGetValue(seed, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var ids = new List<ulong>();
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (ulong.TryParse(part.Trim(), out var id) && id != 0UL)
            {
                ids.Add(id);
            }
        }

        return ids.Count > 0 ? ids.ToArray() : null;
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
