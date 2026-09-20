using System.Text.Json;

using HarmonyLib;

using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

using STS2RitsuLib.Networking.Sidecar;

using STS2_WhiteAlbum2.Core.Combat.Together;
using STS2_WhiteAlbum2.Core.Content;
namespace STS2_WhiteAlbum2.Core.Settings;

/// <summary>联机时的设置同步：<b>以主机为准</b>，客户端跟随。</summary>
/// <remarks>不这么做两边判定会分叉（一台配对、一台不配对），牌堆对不上就被校验和踢下线。
/// 取不到远端值（单人局 / 还没连上 / 主机没发过）时一律用本地设置。</remarks>
internal static class TogetherSettingsSync
{
    private const string Topic = "together.symbiosis_enabled";

    private static readonly Lock Gate = new();

    private static bool _initialized;

    private static Snapshot? _remote;

    /// <summary>本局实际生效的"共生体开关"。所有读设置的地方都应该走这里。</summary>
    public static bool EffectiveSymbiosisEnabled
    {
        get
        {
            Initialize();

            lock (Gate)
            {
                if (_remote is { } remote)
                {
                    return remote.SymbiosisEnabled;
                }
            }

            return WhiteAlbumSettingStore.SymbiosisEnabled;
        }
    }

    /// <summary>本局实际生效的"开局合并双方初始卡组"。</summary>
    public static bool EffectiveMergeStarterDecks
    {
        get
        {
            Initialize();

            lock (Gate)
            {
                if (_remote is { } remote)
                {
                    return remote.MergeStarterDecks;
                }
            }

            return WhiteAlbumSettingStore.MergeStarterDecks;
        }
    }

    /// <summary>本局实际生效的"血量上限提升百分比"（0~100）。</summary>
    public static int EffectiveHpBonusPercent
    {
        get
        {
            Initialize();

            lock (Gate)
            {
                if (_remote is { } remote)
                {
                    return Math.Clamp(remote.HpBonusPercent, 0, 100);
                }
            }

            return WhiteAlbumSettingStore.HpBonusPercent;
        }
    }

    /// <summary>本局实际生效的"共生体人数上限"（2~4）。</summary>
    public static int EffectiveGroupSize
    {
        get
        {
            Initialize();

            lock (Gate)
            {
                if (_remote is { } remote)
                {
                    return Math.Clamp(remote.GroupSize, TogetherPair.MinMembers, TogetherPair.MaxMembers);
                }
            }

            return WhiteAlbumSettingStore.GroupSize;
        }
    }

    /// <summary>本局实际生效的"是否共享金币"。</summary>
    public static bool EffectiveShareGold
    {
        get
        {
            Initialize();

            lock (Gate)
            {
                if (_remote is { } remote)
                {
                    return remote.ShareGold;
                }
            }

            return WhiteAlbumSettingStore.ShareGold;
        }
    }

    public static void Initialize()
    {
        lock (Gate)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            RegisterTopicFromLocalSettings();
            RitsuLibSidecarConfigSyncService.TopicChanged += OnTopicChanged;
        }
    }

    /// <summary>主机把当前设置广播出去（开服 / 有对端就绪 / 改设置时调用）。</summary>
    public static void PublishHostSettings(string reason)
    {
        PublishHostSettings(RunManager.Instance?.NetService, reason);
    }

    public static void PublishHostSettings(INetGameService? netService, string reason)
    {
        if (netService is not NetHostGameService)
        {
            // 客户端 / 单人局：没有"广播"这回事。
            return;
        }

        try
        {
            lock (Gate)
            {
                RegisterTopicFromLocalSettings();
                _remote = null;
            }

            RitsuLibSidecarConfigSyncService.PublishHostState(netService, Topic, 0, reason);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[together] 广播设置失败（{reason}）：{ex.Message}");
        }
    }

    /// <summary>清掉缓存的远端设置（客户端连接 / 断开时调用）。</summary>
    public static void ClearRemote()
    {
        lock (Gate)
        {
            _remote = null;
        }
    }

    private static void RegisterTopicFromLocalSettings()
    {
        RitsuLibSidecarConfigSyncService.RegisterTopic<Snapshot, Snapshot>(
            Topic,
            new Snapshot(
                WhiteAlbumSettingStore.SymbiosisEnabled,
                WhiteAlbumSettingStore.MergeStarterDecks,
                WhiteAlbumSettingStore.HpBonusPercent,
                WhiteAlbumSettingStore.GroupSize,
                WhiteAlbumSettingStore.ShareGold),
            (_, _) => false,
            (state, _) => state);
    }

    private static void OnTopicChanged(SidecarConfigTopicChangedEvent ev)
    {
        if (ev.Topic != Topic || RunManager.Instance?.NetService is not NetClientGameService)
        {
            // 只有客户端听主机的；主机和单人局永远用自己的设置。
            return;
        }

        Snapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<Snapshot>(ev.StateJson);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[together] 解析主机设置失败：{ex.Message}");
            return;
        }

        if (snapshot is null)
        {
            return;
        }

        lock (Gate)
        {
            _remote = snapshot;
        }

        Const.Logger.Info(
            $"[together] 跟随主机设置：共生体={snapshot.SymbiosisEnabled}"
            + $" 合并初始卡组={snapshot.MergeStarterDecks} 血量提升={snapshot.HpBonusPercent}%"
            + $" 人数上限={snapshot.GroupSize} 共享金币={snapshot.ShareGold}");
    }

    private sealed record Snapshot(
        bool SymbiosisEnabled,
        bool MergeStarterDecks,
        int HpBonusPercent,
        int GroupSize,
        bool ShareGold);
}

/// <summary>主机开 ENet 服（直连）时广播一次。</summary>
[HarmonyPatch(typeof(NetHostGameService), nameof(NetHostGameService.StartENetHost))]
internal static class HostStartENetSettingsSyncPatch
{
    [HarmonyPrefix]
    private static void Prefix(NetHostGameService __instance)
    {
        TogetherSettingsSync.PublishHostSettings(__instance, "start_enet_host");

        // 新大厅开始：清掉上一局的共生体成员，并把空名单广播出去。
        SymbiosisMembers.Reset(__instance, "start_enet_host");
    }
}

/// <summary>主机开 Steam 服时广播一次。</summary>
[HarmonyPatch(typeof(NetHostGameService), nameof(NetHostGameService.StartSteamHost))]
internal static class HostStartSteamSettingsSyncPatch
{
    [HarmonyPrefix]
    private static void Prefix(NetHostGameService __instance)
    {
        TogetherSettingsSync.PublishHostSettings(__instance, "start_steam_host");
        SymbiosisMembers.Reset(__instance, "start_steam_host");
    }
}

/// <summary>有对端进入可广播状态时补发一次（后进的人也能拿到）。</summary>
[HarmonyPatch(typeof(NetHostGameService), nameof(NetHostGameService.SetPeerReadyForBroadcasting))]
internal static class HostPeerReadySettingsSyncPatch
{
    [HarmonyPostfix]
    private static void Postfix(NetHostGameService __instance)
    {
        TogetherSettingsSync.PublishHostSettings(__instance, "peer_ready");
        SymbiosisMembers.PublishHostState(__instance, "peer_ready");
    }
}

/// <summary>客户端开始连接前，先清掉上一局缓存的主机设置。</summary>
[HarmonyPatch(typeof(NetClientGameService), nameof(NetClientGameService.Initialize))]
internal static class ClientInitializeSettingsResetPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        TogetherSettingsSync.ClearRemote();
        SymbiosisMembers.ClearRemote();
    }
}

/// <summary>客户端与主机断开后同样清掉。</summary>
[HarmonyPatch(typeof(NetClientGameService), nameof(NetClientGameService.OnDisconnectedFromHost))]
internal static class ClientDisconnectedSettingsResetPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        TogetherSettingsSync.ClearRemote();
        SymbiosisMembers.ClearRemote();
    }
}
