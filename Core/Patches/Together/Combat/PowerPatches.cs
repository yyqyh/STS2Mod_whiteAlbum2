using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_WhiteAlbum2.Core.Together.Multiplayer;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace STS2_WhiteAlbum2.Core.Patches.Together.Combat;

/// <summary>注能（<c>Imbued</c>）：共生体下"每场战斗开始时自动打出"只能发生一次。</summary>
[HarmonyPatch(typeof(Imbued), nameof(Imbued.AfterAutoPrePlayPhaseEntered))]
internal static class ImbuedOncePerCombatPatch
{
    /// <summary>当前记的是哪一场战斗（换战斗即清空）。</summary>
    private static ICombatState? _combat;

    /// <summary>本场战斗里已经自动打出过的注能牌（按对象引用）。</summary>
    private static readonly HashSet<CardModel> Played = new(ReferenceComparer.Instance);

    [HarmonyPrefix]
    private static bool Prefix(Imbued __instance, Player player, ref Task __result)
    {
        if (!TogetherPair.IsActive)
        {
            return true;
        }

        // 不是卡的归属者：原方法自己就会跳过，交给它，保持原版语义。
        if (!ReferenceEquals(player, __instance.Card.Owner))
        {
            return true;
        }

        if (__instance.Card.CombatState is not { } combat)
        {
            return true;
        }

        if (!ReferenceEquals(combat, _combat))
        {
            _combat = combat;
            Played.Clear();
        }

        if (Played.Add(__instance.Card))
        {
            return true;
        }

        CappedLog.Info(
            "imbued.dedupe",
            $"注能（{__instance.Card.Id.Entry}）本场战斗已经自动打出过，跳过重复派发"
            + $"（派发给={player.NetId}，其回合数={player.PlayerCombatState?.TurnNumber}）");

        __result = Task.CompletedTask;
        return false;
    }

    /// <summary>引用相等的比较器（同名牌是不同对象，不能按值去重）。</summary>
    private sealed class ReferenceComparer : IEqualityComparer<CardModel>
    {
        internal static readonly ReferenceComparer Instance = new();

        public bool Equals(CardModel? x, CardModel? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(CardModel obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}

/// <summary>"会改身体数值"的回合末能力：只由<b>原件</b>结算一次，镜像副本不重复结算。</summary>
internal static class MirroredTemporaryPowerGuard
{
    /// <summary>这份能力是不是"不该再自己跑一次"的镜像副本。</summary>
    public static bool ShouldSkip(PowerModel power)
    {
        return TogetherPair.IsActive && PowerMirror.IsMirrorCopy(power);
    }
}

/// <summary>临时力量（含药剂/卡牌派生的一堆子类）回合末只收回一次。</summary>
[HarmonyPatch(typeof(TemporaryStrengthPower), nameof(TemporaryStrengthPower.AfterSideTurnEnd))]
internal static class TemporaryStrengthSingleFirePatch
{
    [HarmonyPrefix]
    private static bool Prefix(TemporaryStrengthPower __instance, ref Task __result)
    {
        if (!MirroredTemporaryPowerGuard.ShouldSkip(__instance))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

/// <summary>临时敏捷（Fade / Anticipate / SpeedPotion 等）回合末只收回一次。</summary>
[HarmonyPatch(typeof(TemporaryDexterityPower), nameof(TemporaryDexterityPower.AfterSideTurnEnd))]
internal static class TemporaryDexteritySingleFirePatch
{
    [HarmonyPrefix]
    private static bool Prefix(TemporaryDexterityPower __instance, ref Task __result)
    {
        if (!MirroredTemporaryPowerGuard.ShouldSkip(__instance))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

/// <summary>临时集中（Hotfix 等）回合末只收回一次。</summary>
[HarmonyPatch(typeof(TemporaryFocusPower), nameof(TemporaryFocusPower.AfterSideTurnEnd))]
internal static class TemporaryFocusSingleFirePatch
{
    [HarmonyPrefix]
    private static bool Prefix(TemporaryFocusPower __instance, ref Task __result)
    {
        if (!MirroredTemporaryPowerGuard.ShouldSkip(__instance))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

/// <summary>虚弱：敌方回合结束时只减一层（镜像副本不重复减）。</summary>
[HarmonyPatch(typeof(WeakPower), nameof(WeakPower.AfterSideTurnEnd))]
internal static class WeakSingleFirePatch
{
    [HarmonyPrefix]
    private static bool Prefix(WeakPower __instance, ref Task __result)
    {
        if (!MirroredTemporaryPowerGuard.ShouldSkip(__instance))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

/// <summary>易伤：同上。</summary>
[HarmonyPatch(typeof(VulnerablePower), nameof(VulnerablePower.AfterSideTurnEnd))]
internal static class VulnerableSingleFirePatch
{
    [HarmonyPrefix]
    private static bool Prefix(VulnerablePower __instance, ref Task __result)
    {
        if (!MirroredTemporaryPowerGuard.ShouldSkip(__instance))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

/// <summary>脆弱：同上。</summary>
[HarmonyPatch(typeof(FrailPower), nameof(FrailPower.AfterSideTurnEnd))]
internal static class FrailSingleFirePatch
{
    [HarmonyPrefix]
    private static bool Prefix(FrailPower __instance, ref Task __result)
    {
        if (!MirroredTemporaryPowerGuard.ShouldSkip(__instance))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}
