using System.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop;

using STS2_WhiteAlbum2.Core.Character.Setsuna;
using STS2_WhiteAlbum2.Core.Character.Touma;
using STS2_WhiteAlbum2.Core.Potions;
using STS2_WhiteAlbum2.Core.Relics;
using STS2_WhiteAlbum2.Core.Character;
using STS2_WhiteAlbum2.Core.Interop;
using STS2_WhiteAlbum2.Core.Settings;
using STS2_WhiteAlbum2.Core.Content;

using SetsunaCharacter = STS2_WhiteAlbum2.Core.Character.Setsuna.Setsuna;
using ToumaCharacter = STS2_WhiteAlbum2.Core.Character.Touma.Touma;

namespace STS2_WhiteAlbum2;

/// <summary>STS2_WHITE_ALBUM2 入口：先初始化设置，再逐类安装 Harmony 补丁。</summary>
/// <remarks>内容（角色 / 卡牌 / 遗物 / 池子）全部走 RitsuLib 的自动注册注解，这里只管设置与补丁。</remarks>
[ModInitializer(nameof(Initialize))]
public static class Main
{
    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var harmony = new Harmony(Const.ModId);

        // 设置必须在任何"读设置"的代码之前就位，所以放在装补丁之前。
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, RitsuLibFramework.CreateLogger(Const.ModId));
        ModTypeDiscoveryHub.RegisterModAssembly(Const.ModId, assembly);

        try
        {
            WhiteAlbumSettingStore.Initialize();
        }
        catch (Exception ex)
        {
            Log.Error($"[{Const.ModId}] 设置初始化失败（不影响装补丁）：{ex.Message}");
        }

        AlbumSettingsPage.Register();

        // 共生体什么时候成组由本 mod 决定（共享本身的实现归 together mod）：
        // 正好 2 人、且两人分别选了本 mod 的两个不同角色。
        TogetherInterop.RegisterPairRule("STS2_WhiteAlbum2:two-distinct", SelectSymbiosisMembers);

        var applied = 0;
        var failed = 0;

        foreach (var type in assembly.GetTypes())
        {
            if (!HasHarmonyPatches(type))
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
            $"[{Const.ModId}] initialized v{Const.Version}; patch classes applied={applied} failed={failed}; "
            + $"Harmony patched {harmony.GetPatchedMethods().Count()} method(s).");

        LogContentIds();

        // 并入的先古/boss 替换内容，id 也一起打出来。
        ReplacePlan.LogContentIds();
    }

    /// <summary>
    /// 共生体的配对规则：正好 2 人，且两人分别选了本 mod 的两个不同角色。
    /// </summary>
    /// <remarks>
    /// 只在 <c>Arm()</c>（新开一局 / 读档 / 重连）被问，必须只读、幂等。
    /// 只看 <c>RunState.Players</c> 的顺序与角色 id —— 两端必须算出同一个名单，否则锚点会分叉。
    /// 返回 <c>null</c> 表示"这局不该成组"，会回落到 together 自己的选人按钮名单。
    /// </remarks>
    private static IReadOnlyList<Player>? SelectSymbiosisMembers(IRunState runState)
    {
        var players = runState.Players;
        if (players.Count != 2)
        {
            return null;
        }

        var first = players[0].Character;
        var second = players[1].Character;

        if (!AlbumCharacterRules.IsAlbumCharacter(first)
            || !AlbumCharacterRules.IsAlbumCharacter(second)
            || first.Id == second.Id)
        {
            return null;
        }

        return [.. players];
    }

    /// <summary>把本 mod 内容的真实 id 打进日志（本地化键对不上时看这几行最快）。</summary>
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

    /// <summary>类级或方法级带 [HarmonyPatch] 的类型都要装（合并后的补丁类只有方法级）。</summary>
    private static bool HasHarmonyPatches(Type type)
    {
        if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
        {
            return true;
        }

        foreach (var method in type.GetMethods(
                     BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (method.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static Type[] ContentTypes =>
    [
        typeof(SetsunaCharacter), typeof(ToumaCharacter),
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
