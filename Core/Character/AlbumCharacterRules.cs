using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;

using STS2_WhiteAlbum2.Core.Settings;
using ToumaCharacter = STS2_WhiteAlbum2.Core.Character.Touma.Touma;
using SetsunaCharacter = STS2_WhiteAlbum2.Core.Character.Setsuna.Setsuna;

namespace STS2_WhiteAlbum2.Core.Character;

/// <summary>
/// 本 mod 两个角色（猎人·一号 / 猎人·二号）的使用规则。
/// </summary>
/// <remarks>
/// <para>
/// 这两个角色只服务于联机 together：单人角色选择界面里不出现；
/// together 开启时，一局只能有两个人，并且两个人必须各选其中一个、不能重复。
/// </para>
/// <para>
/// 这些规则只在“选人界面 + 开局构造”两条路上执行，不在角色注册层硬删内容，
/// 这样联机正常选人时两个角色仍然能正常加载立绘/卡池/遗物。
/// </para>
/// </remarks>
internal static class AlbumCharacterRules
{
    /// <summary>这个角色是不是本 mod 的两个人之一。</summary>
    public static bool IsAlbumCharacter(CharacterModel? character)
    {
        return character is SetsunaCharacter or ToumaCharacter;
    }

    /// <summary>当前大厅是不是“开启了 together 的联机局”。</summary>
    public static bool IsTogetherLobby(StartRunLobby? lobby)
    {
        return lobby is not null
               && lobby.NetService.Type.IsMultiplayer()
               && TogetherSettingsSync.EffectiveSymbiosisEnabled;
    }

    /// <summary>这局大厅里的两个人是不是正好各自选了不同的 mod 角色。</summary>
    public static bool HasValidTogetherPair(StartRunLobby? lobby)
    {
        if (lobby is null || lobby.Players.Count != 2)
        {
            return false;
        }

        var first = lobby.Players[0].character;
        var second = lobby.Players[1].character;

        return IsAlbumCharacter(first)
               && IsAlbumCharacter(second)
               && first.Id != second.Id;
    }

    /// <summary>当前跑局里至少有一个玩家选了本 mod 角色。</summary>
    public static bool CurrentRunHasAlbumCharacter()
    {
        var players = RunManager.Instance.DebugOnlyGetState()?.Players;
        return players is not null && players.Any(player => IsAlbumCharacter(player.Character));
    }

    /// <summary>当前跑局正好两个人，且两人分别选了不同的本 mod 角色。</summary>
    public static bool CurrentRunHasRequiredAlbumPair()
    {
        var players = RunManager.Instance.DebugOnlyGetState()?.Players;
        if (players is not { Count: 2 })
        {
            return false;
        }

        var first = players[0].Character;
        var second = players[1].Character;

        return IsAlbumCharacter(first)
               && IsAlbumCharacter(second)
               && first!.Id != second!.Id;
    }
}
