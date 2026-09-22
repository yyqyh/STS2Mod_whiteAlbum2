using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Powers;
using System.Runtime.CompilerServices;
using STS2_WhiteAlbum2.Core.Combat.Together;
using STS2_WhiteAlbum2.Core.Utils;


using System.Reflection;

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
[HarmonyPatch]
internal static class MirroredPowerSingleFirePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(TemporaryStrengthPower), nameof(TemporaryStrengthPower.AfterSideTurnEnd));
        yield return AccessTools.Method(typeof(TemporaryDexterityPower), nameof(TemporaryDexterityPower.AfterSideTurnEnd));
        yield return AccessTools.Method(typeof(TemporaryFocusPower), nameof(TemporaryFocusPower.AfterSideTurnEnd));
        yield return AccessTools.Method(typeof(WeakPower), nameof(WeakPower.AfterSideTurnEnd));
        yield return AccessTools.Method(typeof(VulnerablePower), nameof(VulnerablePower.AfterSideTurnEnd));
        yield return AccessTools.Method(typeof(FrailPower), nameof(FrailPower.AfterSideTurnEnd));
    }

    [HarmonyPrefix]
    private static bool Prefix(PowerModel __instance, ref Task __result)
    {
        if (!TogetherPair.IsActive || !PowerMirror.IsMirrorCopy(__instance))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}
