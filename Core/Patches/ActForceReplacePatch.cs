using HarmonyLib;

using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

using STS2_WhiteAlbum2.Core.Character;
using STS2_WhiteAlbum2.Core.Ancients;

namespace STS2_WhiteAlbum2.Core.Patches;

/// <summary>
/// 强制替换的"写房间"那一步：把每一幕的 <c>RoomSet.Ancient</c> / <c>Boss</c> / <c>SecondBoss</c> 换成我们的。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么要改 <c>RoomSet</c> 而不是只改"拿的时候"</b>：地图上的图标是从
/// <c>ActModel.AssetPaths → RoomSet.Ancient/Boss.MapNodeAssetPaths</c> 取的
/// （<c>NBossMapPoint</c> 直接读 <c>EncounterModel.BossNodePath + ".png"</c>），
/// 所以只有把房间本身换掉，地图图标才会跟着换成捏奥的。
/// </para>
/// <para>
/// 挂两个点：<see cref="ActModel.GenerateRooms" />（正常开一幕时）和
/// <see cref="ActModel.ValidateRoomsAfterLoad" />（读档时，存档里可能还留着本体的 boss/先古）。
/// 另外 <see cref="ForceBossEncounterPatch" /> / <see cref="ForceAncientPatch" /> 在"取房间"那一步兜底，
/// 保证真正进战斗/进事件的绝对是我们的内容。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
internal static class ForceActRoomsPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance)
    {
        ActForcer.Apply(__instance);
    }
}

/// <summary>读档之后也强制一次（存档里的 boss / 先古可能是本体的）。</summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.ValidateRoomsAfterLoad))]
internal static class ForceActRoomsAfterLoadPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance)
    {
        ActForcer.Apply(__instance);
    }
}

/// <summary>兜底①：真正"取 boss 遭遇"时再确认一次（包括双 boss 的第二只）。</summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEncounter))]
internal static class ForceBossEncounterPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance, RoomType __0, ref EncounterModel __result)
    {
        if (__0 != RoomType.Boss || __result is null)
        {
            return;
        }

        if (!AlbumCharacterRules.CurrentRunHasAlbumCharacter())
        {
            Capped.LogOnce($"[{Const.ModId}] 未替换 boss 遭遇：本局没有 mod 角色");
            return;
        }

        var second = __instance.SecondBossEncounter;
        var isSecondBoss = second is not null && ReferenceEquals(second, __result);

        var type = isSecondBoss
            ? ReplacePlan.SecondBossFor(__instance.Index)
            : ReplacePlan.BossFor(__instance.Index);

        if (type is null)
        {
            return;
        }

        try
        {
            __result = ModelDb.GetById<EncounterModel>(ModelDb.GetId(type));

            Capped.LogOnce(
                $"[{Const.ModId}] 取 boss 遭遇：act{__instance.Index}"
                + $"{(isSecondBoss ? " 第二 boss" : string.Empty)} → {__result.Id.Entry}");
        }
        catch (Exception ex)
        {
            Log.Error($"[{Const.ModId}] 替换 boss 遭遇失败：{ex.Message}");
        }
    }
}

/// <summary>兜底②：真正"取先古"时再确认一次。</summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullAncient))]
internal static class ForceAncientPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance, ref EventModel __result)
    {
        if (!AlbumCharacterRules.CurrentRunHasAlbumCharacter())
        {
            Capped.LogOnce($"[{Const.ModId}] 未替换先古：本局没有 mod 角色");
            return;
        }

        if (ReplacePlan.AncientFor(__instance.Index) is not { } type)
        {
            return;
        }

        try
        {
            __result = ModelDb.GetById<AncientEventModel>(ModelDb.GetId(type));

            Capped.LogOnce($"[{Const.ModId}] 取先古：act{__instance.Index} → {__result.Id.Entry}");
        }
        catch (Exception ex)
        {
            Log.Error($"[{Const.ModId}] 替换先古失败：{ex.Message}");
        }
    }
}

/// <summary>把一幕的三个房间字段一次换掉。</summary>
internal static class ActForcer
{
    /// <summary>ActModel 里 <c>_rooms</c> 是 protected 字段，从补丁里只能用反射取。</summary>
    private static readonly AccessTools.FieldRef<ActModel, RoomSet> RoomsField =
        AccessTools.FieldRefAccess<ActModel, RoomSet>("_rooms");

    public static void Apply(ActModel act)
    {
        if (!AlbumCharacterRules.CurrentRunHasAlbumCharacter())
        {
            Capped.LogOnce($"[{Const.ModId}] 未替换先古/boss：本局没有 mod 角色");
            return;
        }

        try
        {
            var rooms = RoomsField(act);
            var replaced = new List<string>();

            if (ReplacePlan.AncientFor(act.Index) is { } ancientType)
            {
                rooms.Ancient = ModelDb.GetById<AncientEventModel>(ModelDb.GetId(ancientType));
                replaced.Add($"先古={rooms.Ancient.Id.Entry}");
            }

            if (ReplacePlan.BossFor(act.Index) is { } bossType)
            {
                rooms.Boss = ModelDb.GetById<EncounterModel>(ModelDb.GetId(bossType));
                replaced.Add($"boss={rooms.Boss.Id.Entry}");
            }

            if (rooms.HasSecondBoss && ReplacePlan.SecondBossFor(act.Index) is { } secondType)
            {
                rooms.SecondBoss = ModelDb.GetById<EncounterModel>(ModelDb.GetId(secondType));
                replaced.Add($"第二boss={rooms.SecondBoss.Id.Entry}");
            }

            Capped.LogOnce($"[{Const.ModId}] 强制替换 act{act.Index}：{string.Join("，", replaced)}");
        }
        catch (Exception ex)
        {
            Log.Error($"[{Const.ModId}] 强制替换 act{act.Index} 失败：{ex}");
        }
    }
}
