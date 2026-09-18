using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace STS_WhiteAlbum2.Core.Pvp;

/// <summary>
/// 决斗里「敌人」的定义：<b>敌人 = 另一位玩家</b>，而不是「站在敌方阵营的单位」。
/// </summary>
/// <remarks>
/// <para>
/// 为什么光有 <c>CombatState.HittableEnemies</c> 的重算还不够：那条只负责
/// 「规则的候选列表」，而"鼠标能不能选中对面"要走另外两道闸门，它们看的是
/// <c>Creature.Side</c>：
/// </para>
/// <list type="number">
/// <item><description>
/// <b>卡牌校验</b>：<c>CardModel.IsValidTarget</c> 里
/// <c>TargetType.AnyEnemy =&gt; target.Side != Owner.Creature.Side</c>。
/// 决斗中两人都在我方侧，于是"对面"被判为不合法 → <c>TryManualPlay</c> 直接取消，
/// 连 <c>PlayCardAction</c> 里也会再拦一次。
/// </description></item>
/// <item><description>
/// <b>选中 UI</b>：<c>NTargetManager.AllowedToTargetCreature</c> 里
/// <c>TargetType.AnyEnemy =&gt; creature.Side == CombatSide.Enemy</c>。
/// 立绘虽然被挪到右边，但数据上仍是我方侧 → 悬停判定直接 return，
/// <c>HoveredNode</c> 永远是 null → 松手时确认不了（这就是"选不中"的根因）。
/// </description></item>
/// </list>
/// <para>
/// 所以这里把两道闸门都按"决斗语义"重写：<b>AnyEnemy 可以指向另一位玩家</b>；
/// 反过来 <b>AnyAlly 不再能指向另一位玩家</b>（1v1 里没有队友，否则"给友方上 buff"
/// 的牌会变成给对方加 buff）。
/// </para>
/// <para>
/// 判定用的是「卡主 vs 目标」，<b>不依赖本机视角</b>，所以两端算出来的结果一定一致
/// （ActionQueueSynchronizer 会把同一个 <c>PlayCardAction</c> 发给双方重新校验）。
/// 唯一带本机视角的是悬停 UI —— 那本来就该是各看各的。
/// </para>
/// </remarks>
internal static class DuelTargeting
{
    /// <summary>这场战斗是不是「2 人决斗」（普通战斗一律不插手）。</summary>
    public static bool InTwoPlayerDuel(ICombatState? state)
    {
        return DuelConfig.Enabled
               && DuelState.InDuel
               && state is { } combat
               && combat.Players.Count == 2;
    }

    /// <summary>这个单位就是本机玩家在决斗里的对手（活着，且不是我自己）。</summary>
    /// <remarks>归属判定统一放 <c>TogetherPlayers</c>，两边只有一份实现，免得对不上。</remarks>
    public static bool IsOpponentOfLocal(Creature? creature)
    {
        return creature is { IsDead: false } && TogetherPlayers.IsOpponentOfLocal(creature);
    }

    /// <summary>目标是不是"另一位玩家"（相对卡牌主人而言）。</summary>
    public static bool IsOtherPlayer(Creature? target, Creature? owner)
    {
        return target is { IsPlayer: true, IsDead: false }
               && owner is not null
               && !ReferenceEquals(target, owner);
    }

    /// <summary>
    /// 「对手到底算不算合法目标」的唯一判定入口（卡牌与药水共用）。
    /// </summary>
    /// <returns>需要改写时给出新结果；<c>null</c> 表示不插手，维持原版判断。</returns>
    public static bool? OpponentTargetOverride(
        TargetType targetType,
        Creature? target,
        Creature? owner,
        ICombatState? state)
    {
        if (target is null
            || targetType is not (TargetType.AnyEnemy or TargetType.AnyAlly)
            || !InTwoPlayerDuel(state)
            || !IsOtherPlayer(target, owner))
        {
            return null;
        }

        // AnyEnemy：放行；AnyAlly：否掉（1v1 里对面不是队友）。
        return targetType == TargetType.AnyEnemy;
    }
}

/// <summary>
/// 闸门①：卡牌层——决斗里 AnyEnemy 的"对手"是另一位玩家，AnyAlly 则不再成立。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsValidTarget))]
internal static class DuelCardTargetValidityPatch
{
    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, Creature? target, ref bool __result)
    {
        if (__instance is null)
        {
            return;
        }

        var allowed = DuelTargeting.OpponentTargetOverride(
            __instance.TargetType,
            target,
            __instance.Owner?.Creature,
            __instance.CombatState);

        if (allowed is null || allowed.Value == __result)
        {
            return;
        }

        __result = allowed.Value;

        Capped.LogOnce(allowed.Value
            ? "[STS_WhiteAlbum2] 目标校验：对手被当作「敌人」放行（AnyEnemy）"
            : "[STS_WhiteAlbum2] 目标校验：对手不再被当作「队友」（AnyAlly）");
    }
}

/// <summary>
/// 闸门①的孪生兄弟：药水走的是另一套校验（<c>PotionModel.IsValidTarget</c>），
/// 不一起改的话会出现"选中 UI 让点，点完药水用不出去"的怪现象。
/// </summary>
[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.IsValidTarget))]
internal static class DuelPotionTargetValidityPatch
{
    [HarmonyPostfix]
    private static void Postfix(PotionModel __instance, Creature? target, ref bool __result)
    {
        if (__instance is null)
        {
            return;
        }

        var owner = __instance.Owner?.Creature;

        var allowed = DuelTargeting.OpponentTargetOverride(
            __instance.TargetType,
            target,
            owner,
            owner?.CombatState);

        if (allowed is null || allowed.Value == __result)
        {
            return;
        }

        __result = allowed.Value;

        Capped.LogOnce("[STS_WhiteAlbum2] 目标校验：药水也可以指向对手了");
    }
}

/// <summary>
/// 闸门②：选中 UI 层——鼠标悬停/点击对手时，让 <c>NTargetManager</c> 认这个目标。
/// </summary>
/// <remarks>
/// 本体把"谁是合法目标"也写死在阵营上（<c>AnyEnemy</c> 要求 <c>Side == Enemy</c>），
/// 而决斗里两人都保持我方侧（方案③，见 DuelMode 的说明），所以必须这里放行。
/// 不放行的话鼠标悬停上去 <c>OnNodeHovered</c> 会立刻 return，
/// <c>HoveredNode</c> 保持 null，松手只会取消 —— 表现就是"点不到人"。
/// </remarks>
[HarmonyPatch(typeof(NTargetManager), "AllowedToTargetCreature")]
internal static class DuelTargetManagerOpponentPatch
{
    /// <summary>当前正在选的目标类型（private 字段，取不到就整体不生效，不影响原版）。</summary>
    private static readonly AccessTools.FieldRef<NTargetManager, TargetType>? ValidTargetsField =
        AccessTools.Field(typeof(NTargetManager), "_validTargetsType") is null
            ? null
            : AccessTools.FieldRefAccess<NTargetManager, TargetType>("_validTargetsType");

    [HarmonyPostfix]
    private static void Postfix(NTargetManager __instance, Creature creature, ref bool __result)
    {
        if (ValidTargetsField is null || creature is null)
        {
            return;
        }

        if (!DuelTargeting.InTwoPlayerDuel(creature.CombatState))
        {
            return;
        }

        if (!DuelTargeting.IsOpponentOfLocal(creature))
        {
            return;
        }

        var targetType = ValidTargetsField(__instance);

        if (targetType == TargetType.AnyEnemy)
        {
            __result = true;
            Capped.LogOnce("[STS_WhiteAlbum2] 选中 UI：对手现在可以悬停/点击选中（当作敌人）");
        }
        else if (targetType == TargetType.AnyAlly)
        {
            // 1v1：对面不是队友，"指向队友"的牌在决斗里没有合法目标。
            __result = false;
        }
    }
}
