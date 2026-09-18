using System.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop;

using STS2_WhiteAlbum2.Core.Cards;
using STS2_WhiteAlbum2.Core.Character;
using STS2_WhiteAlbum2.Core.Settings;
using STS2_WhiteAlbum2.Core.Ancients;
using STS2_WhiteAlbum2.Core.Pvp;
using STS2_WhiteAlbum2.Core.Together.Config;
using STS2_WhiteAlbum2.Core.Together.Multiplayer;
using STS2_WhiteAlbum2.Core.Potions;
using STS2_WhiteAlbum2.Core.Relics;

namespace STS2_WhiteAlbum2;

/// <summary>
/// STS2_WHITE_ALBUM2 入口：逐类安装补丁。
/// </summary>
/// <remarks>
/// <para>
/// 内容（角色 / 卡牌 / 遗物 / 池子）全部走 RitsuLib 的自动注册注解，
/// 这里只负责装 Harmony 补丁 —— 现在还没有补丁，
/// 但骨架留着：后续把 together（本地多控）、PVP 决斗、先古替换并进来时，补丁类直接放进来就会被扫到。
/// </para>
/// </remarks>
[ModInitializer(nameof(Initialize))]
public static class Main
{
    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var harmony = new Harmony(Const.ModId);

        // —— 并入的 together（本地多控）逻辑 ——
        // 它的设置要在任何"读设置"的代码之前就位（共享角色目标就是从设置里读的），
        // 所以这几步放在装补丁之前，顺序和它原本的 Main 一致。
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, RitsuLibFramework.CreateLogger(Const.ModId));
        ModTypeDiscoveryHub.RegisterModAssembly(Const.ModId, assembly);
        TogetherSettingsStore.Initialize();
        TogetherSettingsSync.Initialize();
        AlbumGeneralSettingsStore.Initialize();
        AlbumSettingsPage.Register();
        SymbiosisMembers.Initialize();

        // —— 并入的 PVP 决斗逻辑 ——
        // 只初始化设置存储；它自带的设置页暂时不注册（本 mod 已经有一个设置页了，
        // 两个页面挂在同一个 mod 名下容易冲突，等确认 RitsuLib 支持多页再打开）。
        try { PvpSettingsStore.Initialize(); }
        catch (Exception ex) { Log.Error($"[{Const.ModId}] PVP 设置初始化失败（不影响装补丁）：{ex.Message}"); }

        var applied = 0;
        var failed = 0;

        foreach (var type in assembly.GetTypes())
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0)
            {
                continue;
            }

            try
            {
                harmony.CreateClassProcessor(type).Patch();
                applied++;
            }
            catch (Exception ex)
            {
                failed++;
                Log.Error($"[{Const.ModId}] 补丁类 {type.Name} 应用失败：{Describe(ex)}");
            }
        }

        Log.Info(
            $"[{Const.ModId}] initialized v0.1.0; patch classes applied={applied} failed={failed}; "
            + $"Harmony patched {harmony.GetPatchedMethods().Count()} method(s).");

        LogContentIds();

        // 并入的先古/boss 替换内容，id 也一起打出来。
        ReplacePlan.LogContentIds();
    }

    /// <summary>
    /// 把本 mod 内容的真实 id 打进日志。
    /// </summary>
    /// <remarks>
    /// RitsuLib 给模组模型的 id 是 <c>&lt;模组id&gt;_&lt;类别&gt;_&lt;类名&gt;</c>，
    /// 而且类名里的驼峰/数字还会再被拆一次
    /// （例如 <c>Setsuna</c> → <c>WHITE_ALBUM_TWO_HUNTER</c>）。
    /// 本地化键就是拿这些 id 拼的 —— 键显示不出来时看这几行最快，它是运行时真值，不是猜的。
    /// </remarks>
    private static void LogContentIds()
    {
        foreach (var type in ContentTypes)
        {
            try
            {
                Log.Info(
                    $"[{Const.ModId}] 内容 id：{type.Name} = "
                    + ModContentRegistry.GetFixedPublicEntry(Const.ModId, type));
            }
            catch (Exception ex)
            {
                Log.Warn($"[{Const.ModId}] 取内容 id 失败：{type.Name}（{ex.Message}）");
            }
        }
    }

    private static Type[] ContentTypes =>
    [
        typeof(Setsuna), typeof(Touma),
        typeof(SetsunaCardOne), typeof(SetsunaCardTwo), typeof(SetsunaCardThree),
        typeof(SetsunaCardFour), typeof(SetsunaCardFive),
        typeof(ToumaCardOne), typeof(ToumaCardTwo), typeof(ToumaCardThree),
        typeof(ToumaCardFour), typeof(ToumaCardFive),
        typeof(AlbumRelicOne), typeof(AlbumRelicTwo), typeof(AlbumRelicThree),
        typeof(AlbumPotionOne),
        typeof(SetsunaCardPool), typeof(SetsunaRelicPool), typeof(SetsunaPotionPool),
        typeof(ToumaCardPool), typeof(ToumaRelicPool), typeof(ToumaPotionPool),
    ];

    private static string Describe(Exception ex)
    {
        var parts = new List<string>();

        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            parts.Add($"{current.GetType().Name}: {current.Message}");
        }

        return string.Join("  ←  ", parts);
    }
}
