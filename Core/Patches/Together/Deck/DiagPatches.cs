using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using STS2_WhiteAlbum2.Core.Combat.Together;
using STS2_WhiteAlbum2.Core.Utils;



namespace STS2_WhiteAlbum2.Core.Patches.Together.Deck;

/// <summary>临时诊断（定位完可删）</summary>
internal static class DiagPatches
{
    /// <summary>查"牌到底有没有进弃牌堆、进的是哪一份"。</summary>
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
    [HarmonyPostfix]
    private static void DiscardEntryPostfix(CardPile __instance, CardModel __0)
    {
        if (!SelfCheck.Enabled || __instance.Type != PileType.Discard || !Diag.TryLogDiscard())
        {
            return;
        }

        var owner = __0.Owner;
        Log.Info(
            $"[together][diag] 弃牌入堆：card={__0.Id.Entry} owner={owner?.NetId} "
            + $"isAnchor={TogetherPair.IsAnchor(owner)} isEcho={TogetherPair.IsEcho(owner)} "
            + $"弃牌堆现在={__instance.Cards.Count} 张");
    }

    /// <summary>查"回合结束清手牌到底清了几张、清了谁的手牌"。</summary>
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterFlush))]
    [HarmonyPrefix]
    private static void FlushPrefix(Player __1, object __3)
    {
        if (!SelfCheck.Enabled || !TogetherPair.IsActive || !Diag.TryLogFlush())
        {
            return;
        }

        var flushed = __3 as IReadOnlyCollection<CardModel>;
        var pile = __1.PlayerCombatState?.DiscardPile;
        Log.Info(
            $"[together][diag] 回合清手牌：player={__1.NetId} "
            + $"isAnchor={TogetherPair.IsAnchor(__1)} isEcho={TogetherPair.IsEcho(__1)} "
            + $"清掉={flushed?.Count} 张 弃牌堆={pile?.Cards.Count} 张");

        if (flushed is null)
        {
            return;
        }

        // 逐张打印本体批量 Add 那条"静默失败"分支的每个判定项，
        // 看它到底是被哪一项挡下的（挡下时不抛异常，只把 success 置 false）。
        foreach (var card in flushed)
        {
            var owner = card.Owner;
            var creature = owner?.Creature;
            Log.Info(
                $"[together][diag]   清手牌明细：card={card.Id.Entry} owner={owner?.NetId} "
                + $"removedFromState={card.HasBeenRemovedFromState} "
                + $"isInCombat={card.IsInCombat} pile={card.Pile?.Type.ToString() ?? "null"} "
                + $"ownerIsDead={creature?.IsDead} ownerCombatStateNull={creature?.CombatState is null} "
                + $"ownerSide={creature?.Side.ToString() ?? "null"} "
                + $"combatEnding={CombatManager.Instance.IsEnding} "
                + $"combatOverOrEnding={CombatManager.Instance.IsOverOrEnding}");
        }
    }

    /// <summary>查"谁在读回声的卡组、读到的是哪份"。</summary>
    [HarmonyPatch(typeof(Player), "get_Deck")]
    [HarmonyPostfix]
    private static void DeckReadPostfix(Player __instance, ref CardPile __result)
    {
        if (!SelfCheck.Enabled || !TogetherPair.IsEcho(__instance) || !Diag.TryLogDeckRead())
        {
            return;
        }

        var anchorDeck = TogetherPair.Anchor?.Deck;
        Log.Info(
            $"[together][diag] 读回声卡组：netId={__instance.NetId} "
            + $"返回={__result.Cards.Count} 张 锚点卡组={anchorDeck?.Cards.Count} "
            + $"同一实例={ReferenceEquals(__result, anchorDeck)}");
    }

    /// <summary>查"抽牌到底有没有发生、从哪个堆抽、落到谁手上"。</summary>
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardDrawn))]
    [HarmonyPrefix]
    private static void DrawPrefix(CardModel __2)
    {
        if (!SelfCheck.Enabled || !TogetherPair.IsActive || !Diag.TryLogDraw() || __2.Owner is not { } owner)
        {
            return;
        }

        var combat = owner.PlayerCombatState;
        Log.Info(
            $"[together][diag] 抽牌：card={__2.Id.Entry} owner={owner.NetId} "
            + $"isAnchor={TogetherPair.IsAnchor(owner)} isEcho={TogetherPair.IsEcho(owner)} "
            + $"hand={combat?.Hand.Cards.Count} draw={combat?.DrawPile.Cards.Count}");
    }

    /// <summary>查"卡牌奖励加到了哪个卡组"。</summary>
    [HarmonyPatch(
        typeof(CardPileCmd),
        nameof(CardPileCmd.Add),
        new[] { typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool), typeof(bool) })]
    [HarmonyPrefix]
    private static void DeckAddPrefix(CardPile __1)
    {
        if (!SelfCheck.Enabled || __1.Type != PileType.Deck || !TogetherPair.IsActive || !Diag.TryLogDeckAdd())
        {
            return;
        }

        Log.Info($"[together][diag] 往卡组加牌：目标已有 {__1.Cards.Count} 张（锚点卡组={TogetherPair.Anchor?.Deck.Cards.Count}）");
    }

    /// <summary>变牌（<c>FromDeckForTransformation</c>）。</summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForTransformation))]
    [HarmonyPrefix]
    private static void TransformSelectPrefix(Player __0)
    {
        CardSelectDiag.Log("变牌选牌", __0);
    }

    /// <summary>通用的"从卡组选一张"（<c>FromDeckGeneric</c>：先古克隆等不少事件走它）。</summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckGeneric))]
    [HarmonyPrefix]
    private static void DeckSelectPrefix(Player __0)
    {
        CardSelectDiag.Log("卡组选牌", __0);
    }

    /// <summary>休息处强化（<c>FromDeckForUpgrade</c>）。</summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForUpgrade))]
    [HarmonyPrefix]
    private static void UpgradeSelectPrefix(Player __0)
    {
        CardSelectDiag.Log("强化选牌", __0);
    }

    /// <summary>附魔（<c>FromDeckForEnchantment</c>）。</summary>
    [HarmonyPatch(
        typeof(CardSelectCmd),
        nameof(CardSelectCmd.FromDeckForEnchantment),
        new[] { typeof(Player), typeof(EnchantmentModel), typeof(int), typeof(CardSelectorPrefs) })]
    [HarmonyPrefix]
    private static void EnchantSelectPrefix(Player __0)
    {
        CardSelectDiag.Log("附魔选牌", __0);
    }
}

/// <summary>临时诊断（定位完就删）。</summary>
internal static class Diag
{
    private static int _drawsLogged;
    private static int _deckReadsLogged;
    private static int _deckAddsLogged;
    private static int _discardLogged;
    private static int _flushLogged;

    internal static bool TryLogDraw() => System.Threading.Interlocked.Increment(ref _drawsLogged) <= 30;

    internal static bool TryLogDeckRead() => System.Threading.Interlocked.Increment(ref _deckReadsLogged) <= 30;

    internal static bool TryLogDeckAdd() => System.Threading.Interlocked.Increment(ref _deckAddsLogged) <= 30;

    internal static bool TryLogDiscard() => System.Threading.Interlocked.Increment(ref _discardLogged) <= 25;

    internal static bool TryLogFlush() => System.Threading.Interlocked.Increment(ref _flushLogged) <= 10;
}

/// <summary>诊断："从卡组里选一张牌"的界面到底有没有弹、候选有几张。</summary>
internal static class CardSelectDiag
{
    public static void Log(string entry, Player? player)
    {
        if (player is null)
        {
            return;
        }

        CardPile? pile;
        try
        {
            pile = PileType.Deck.GetPile(player);
        }
        catch (Exception ex)
        {
            CappedLog.Info("select.entry", $"{entry}：取卡组抛异常 {ex.GetType().Name}: {ex.Message}");
            return;
        }

        var pileCount = pile?.Cards.Count ?? -1;
        var fieldCount = player.Deck.Cards.Count;
        var upgradable = pile?.Cards.Count(IsUpgradable) ?? -1;
        var transformable = pile?.Cards.Count(IsTransformable) ?? -1;

        CappedLog.Info(
            "select.entry",
            $"{entry}：player={player.NetId} isAnchor={TogetherPair.IsAnchor(player)} "
            + $"isEcho={TogetherPair.IsEcho(player)} 共生局={TogetherPair.IsActive} 本机={LocalContext.NetId} "
            + $"| GetPile={pileCount} 张、player.Deck={fieldCount} 张、可升级={upgradable}、可变形={transformable}"
            + $"（两处是同一口堆={ReferenceEquals(pile, player.Deck)}）");
    }

    private static bool IsUpgradable(CardModel card)
    {
        try
        {
            return card.IsUpgradable;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsTransformable(CardModel card)
    {
        try
        {
            return card.IsTransformable;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
