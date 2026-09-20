using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using STS2_WhiteAlbum2.Core.Combat.Together;
using System.Runtime.CompilerServices;

namespace STS2_WhiteAlbum2.Core.Patches.Together.Combat;

/// <summary>钩子监听表去重：共享牌堆里的牌会被当成<b>两个</b>监听者，于是"每回合一次"的卡牌/附魔效果触发两遍。</summary>
[HarmonyPatch(typeof(CombatState), nameof(CombatState.IterateHookListeners))]
internal static class HookListenerDedupePatch
{
    [HarmonyPostfix]
    private static void Postfix(ref IEnumerable<AbstractModel> __result)
    {
        if (!TogetherPair.IsActive || __result is null)
        {
            return;
        }

        __result = Filter(__result);
    }

    private static IEnumerable<AbstractModel> Filter(IEnumerable<AbstractModel> source)
    {
        var seen = new HashSet<AbstractModel>(ReferenceComparer.Instance);

        foreach (var model in source)
        {
            if (model is null || !seen.Add(model))
            {
                continue;
            }

            yield return model;
        }
    }

    /// <summary>引用相等的比较器（理由见类型注释：按 Id 去重会误伤同名牌）。</summary>
    private sealed class ReferenceComparer : IEqualityComparer<AbstractModel>
    {
        internal static readonly ReferenceComparer Instance = new();

        public bool Equals(AbstractModel? x, AbstractModel? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(AbstractModel obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
