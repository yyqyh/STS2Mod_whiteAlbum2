using Godot;

using HarmonyLib;

using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

using STS2_WhiteAlbum2.Core.Character;
using STS2_WhiteAlbum2.Core.Interop;
using STS2_WhiteAlbum2.Core.Settings;



using System.Reflection;

namespace STS2_WhiteAlbum2.Core.Patches;

/// <summary>
/// 选人界面的 mod 角色可见性：单人隐藏，联机作为额外角色出现。
/// </summary>
/// <remarks>
/// <para>
/// together 开启时不再把原版角色换掉，也不再加额外的确认按钮：
/// 联机选人界面就是“原版角色 + 本 mod 两个角色”，玩家用本体原本的出发/确认进入游戏。
/// </para>
/// <para>
/// 是否真的开启共享由开局时的实际组合决定（规则见 <c>Main.SelectSymbiosisMembers</c>，注册给 together 的
/// <c>RegisterPairRule</c>）：
/// 只有正好 2 人、两个人都选了本 mod 角色、且两个角色不同，才会开启；
/// 只选 1 个 mod 角色、人数不为 2、两人选同一个 mod 角色，都按普通联机局跑。
/// </para>
/// </remarks>
internal static class CharacterSelectGateImpl
{
    /// <summary>当前打开的角色选择界面。</summary>
    internal static NCharacterSelectScreen? CachedScreen;

    internal static readonly AccessTools.FieldRef<NCharacterSelectScreen, Control> CharButtonContainer =
        AccessTools.FieldRefAccess<NCharacterSelectScreen, Control>("_charButtonContainer");

    /// <summary>打开选人界面：单人隐藏两个 mod 角色；联机全部显示。</summary>
    internal static void OnOpened(NCharacterSelectScreen screen)
    {
        try
        {
            CachedScreen = screen;
            Apply(screen);
        }
        catch (Exception ex)
        {
            Log.Warn($"[STS2_WhiteAlbum2] 选人界面角色可见性刷新失败（不影响原版选人）：{ex.Message}");
        }
    }

    /// <summary>关闭选人界面时清理缓存。</summary>
    internal static void OnClosed(NCharacterSelectScreen screen)
    {
        if (ReferenceEquals(CachedScreen, screen))
        {
            CachedScreen = null;
        }
    }

    private static void Apply(NCharacterSelectScreen screen)
    {
        var lobby = screen.Lobby;
        if (lobby is null)
        {
            return;
        }

        var container = CharButtonContainer(screen);
        if (container is null)
        {
            return;
        }

        var buttons = container.GetChildren().OfType<NCharacterSelectButton>().ToList();
        if (buttons.Count == 0)
        {
            return;
        }

        var multiplayer = lobby.NetService.Type.IsMultiplayer();
        var singleplayerVisible = WhiteAlbumSettingStore.Current.CharactersVisibleInSingleplayer;
        var albumCount = 0;

        foreach (var button in buttons)
        {
            if (button.IsRandom)
            {
                // 随机角色按钮的可见性由本体按解锁进度决定，这里不要覆盖。
                continue;
            }

            var isAlbum = AlbumCharacterRules.IsAlbumCharacter(button.Character);
            if (isAlbum)
            {
                albumCount++;
            }

            // 单人：mod 角色不出现。联机：作为额外角色加入原版选人列表。
            button.Visible = multiplayer || singleplayerVisible || !isAlbum;
        }

        Log.Info(
            $"[{Const.ModId}] 选人可见性：联机={multiplayer} 单人可见开关={singleplayerVisible} "
            + $"大厅人数={lobby.Players.Count} mod角色按钮={albumCount}");

        RebuildFocusNeighbors(container);
    }

    private static void RebuildFocusNeighbors(Control container)
    {
        var visible = container.GetChildren()
            .OfType<NCharacterSelectButton>()
            .Where(button => button.Visible)
            .ToList();

        if (visible.Count == 0)
        {
            return;
        }

        for (var i = 0; i < visible.Count; i++)
        {
            var current = visible[i];
            current.FocusNeighborTop = current.GetPath();
            current.FocusNeighborBottom = current.GetPath();
            current.FocusNeighborLeft = visible[(i - 1 + visible.Count) % visible.Count].GetPath();
            current.FocusNeighborRight = visible[(i + 1) % visible.Count].GetPath();
        }
    }
}

/// <summary>选人界面：打开时应用 mod 角色可见性，关闭时回收缓存。</summary>
[HarmonyPatch]
internal static class CharacterSelectPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NCharacterSelectScreen), "OnSubmenuOpened");
        yield return AccessTools.Method(typeof(NCharacterSelectScreen), "OnSubmenuClosed");
    }

    [HarmonyPostfix]
    private static void Postfix(NCharacterSelectScreen __instance, MethodBase __originalMethod)
    {
        if (__originalMethod.Name == "OnSubmenuOpened")
        {
            CharacterSelectGateImpl.OnOpened(__instance);
            return;
        }

        CharacterSelectGateImpl.OnClosed(__instance);
    }
}
