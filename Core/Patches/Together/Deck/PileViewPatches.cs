using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.TopBar;
using STS2_WhiteAlbum2.Core.Combat.Together;

using System.Reflection;

namespace STS2_WhiteAlbum2.Core.Patches.Together.Deck;

/// <summary>牌堆视图：CardModel.Pile 查询与牌堆计数同步</summary>
internal static class PileViewPatches
{
    /// <summary>让 <c>CardModel.Pile</c> 在配对局里也能找到"借住"在另一半堆里的自己。</summary>
    [HarmonyPatch(typeof(CardModel), "get_Pile")]
    [HarmonyPostfix]
    private static void CardPileLookupPostfix(CardModel __instance, ref CardPile? __result)
    {
        if (__result is not null || !TogetherPair.IsActive)
        {
            return;
        }

        foreach (var other in TogetherPair.OthersOf(OwnerOf(__instance)))
        {
            foreach (var pile in other.Piles)
            {
                if (pile.Cards.Contains(__instance))
                {
                    __result = pile;
                    return;
                }
            }
        }
    }

    /// <summary><c>Owner</c> 的 getter 会 AssertMutable，对 canonical 模型会抛，所以兜一层。</summary>
    private static Player? OwnerOf(CardModel card)
    {
        try
        {
            return card.Owner;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>入堆（含 <c>silent: true</c> 的静默入堆）之后，把计数写成真实张数。</summary>
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
    [HarmonyPostfix]
    private static void CountAddPostfix(CardPile __instance)
    {
        PileCountSync.SyncPile(__instance);
    }

    /// <summary>出堆（含静默出堆，抽牌走的就是这条）之后，把计数写成真实张数。</summary>
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.RemoveInternal))]
    [HarmonyPostfix]
    private static void CountRemovePostfix(CardPile __instance)
    {
        PileCountSync.SyncPile(__instance);
    }

    /// <summary>战斗牌堆按钮登记（顺手对齐一次初始张数）。</summary>
    [HarmonyPatch(typeof(NCombatCardPile), nameof(NCombatCardPile.Initialize))]
    [HarmonyPostfix]
    private static void CountBindPostfix(NCombatCardPile __instance)
    {
        PileCountSync.Bind(__instance, typeof(NCombatCardPile), PileCountSync.WriteCountLabel);
    }

    /// <summary>战斗牌堆按钮出场景树时注销。</summary>
    [HarmonyPatch(typeof(NCombatCardPile), nameof(NCombatCardPile._ExitTree))]
    [HarmonyPostfix]
    private static void CountUnbindPostfix(NCombatCardPile __instance)
    {
        PileCountSync.Unbind(__instance);
    }

    /// <summary>顶栏卡组按钮登记。</summary>
    [HarmonyPatch(typeof(NTopBarDeckButton), nameof(NTopBarDeckButton.Initialize))]
    [HarmonyPostfix]
    private static void DeckCountBindPostfix(NTopBarDeckButton __instance)
    {
        PileCountSync.Bind(__instance, typeof(NTopBarDeckButton), PileCountSync.RefreshDeckButton);
    }

    /// <summary>Godot 的 <c>NOTIFICATION_PREDELETE</c>。</summary>
    private const int PredeleteNotification = 1;

    /// <summary>顶栏卡组按钮销毁时注销（本体就是在这个通知里退订自己的事件的）。</summary>
    [HarmonyPatch(typeof(NTopBarDeckButton), nameof(NTopBarDeckButton._Notification))]
    [HarmonyPostfix]
    private static void DeckCountUnbindPostfix(NTopBarDeckButton __instance, int __0)
    {
        if (__0 == PredeleteNotification)
        {
            PileCountSync.Unbind(__instance);
        }
    }
}

/// <summary>牌堆计数 UI 的即时同步：把按钮上的数字按**真实张数**写死，不再靠事件累加。</summary>
internal static class PileCountSync
{
    private const string PileFieldName = "_pile";

    private const string LabelFieldName = "_countLabel";

    private const string CountFieldName = "_currentCount";

    /// <summary>已登记的 UI 节点 →（它盯着的牌堆，以及怎么刷新它）。</summary>
    private static readonly List<Entry> Entries = [];

    /// <summary>字段缓存。</summary>
    private static readonly Dictionary<(Type Type, string Name), FieldInfo?> FieldCache = [];

    private static readonly Dictionary<(Type Type, string Name), MethodInfo?> MethodCache = [];

    private static bool _warned;

    private sealed class Entry
    {
        public required object Node { get; init; }

        public required CardPile Pile { get; init; }

        public required Action<object, CardPile> Sync { get; init; }
    }

    /// <summary>登记一个"显示某个牌堆"的 UI 节点（战斗牌堆按钮 / 顶栏卡组按钮）。</summary>
    /// <param name="node">UI 节点。</param>
    /// <param name="declaringType">字段 <c>_pile</c> 的声明类型（战斗牌堆按钮是 <c>NCombatCardPile</c>）。</param>
    /// <param name="sync">拿到节点后怎么把它的显示刷新到真实张数。</param>
    public static void Bind(object node, Type declaringType, Action<object, CardPile> sync)
    {
        if (node is not GodotObject instance || !GodotObject.IsInstanceValid(instance))
        {
            return;
        }

        Unbind(node);

        if (FieldOf(declaringType, PileFieldName)?.GetValue(node) is not CardPile pile)
        {
            return;
        }

        Entries.Add(new Entry { Node = node, Pile = pile, Sync = sync });
        SyncPile(pile);
    }

    /// <summary>节点离开场景树 / 被销毁时注销，避免留下对已死 Godot 对象的引用。</summary>
    public static void Unbind(object node)
    {
        for (var i = Entries.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(Entries[i].Node, node))
            {
                Entries.RemoveAt(i);
            }
        }
    }

    /// <summary>某个牌堆的内容变了 → 把所有盯着它的 UI 刷成真实张数。</summary>
    public static void SyncPile(CardPile? pile)
    {
        if (pile is null || Entries.Count == 0)
        {
            return;
        }

        // 只关心界面上有计数的堆（Hand / Play 没有按钮）。
        if (pile.Type is not (PileType.Draw or PileType.Discard or PileType.Exhaust or PileType.Deck))
        {
            return;
        }

        // 快照：刷新过程中可能有节点被判定失效并从表里摘掉。
        foreach (var entry in Entries.ToArray())
        {
            if (!ReferenceEquals(entry.Pile, pile))
            {
                continue;
            }

            if (entry.Node is not GodotObject instance || !GodotObject.IsInstanceValid(instance))
            {
                Unbind(entry.Node);
                continue;
            }

            try
            {
                entry.Sync(entry.Node, entry.Pile);
            }
            catch (Exception ex)
            {
                WarnOnce($"刷新牌堆计数 UI 失败：{ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>战斗牌堆按钮：把 <c>_currentCount</c> 与标签一起写成真实张数。</summary>
    public static void WriteCountLabel(object node, CardPile pile)
    {
        var count = pile.Cards.Count;
        var countField = FieldOf(typeof(NCombatCardPile), CountFieldName);

        if (countField?.GetValue(node) is int current && current == count)
        {
            // 已经是真实值：不重复调 SetTextAutoSize（免得白白重建一次文本排版）。
            return;
        }

        countField?.SetValue(node, count);

        if (FieldOf(typeof(NCombatCardPile), LabelFieldName)?.GetValue(node) is not { } label)
        {
            return;
        }

        InvokeSetText(label, count.ToString());

        // 消耗堆按钮一开始是隐藏的，本体靠"牌落进堆"那次事件播进入动画才让它显形。
        // 共享堆里的牌大多属于锚点 → 回声那一侧永远等不到那个事件 → 整只按钮都不出现
        // （数字再准也没用）。这里补一次显形：只在"堆里有牌而按钮还藏着"时动手。
        if (count > 0 && node is NExhaustPileButton exhaust && !exhaust.Visible)
        {
            exhaust.AnimIn();
            exhaust.Enable();
        }
    }

    /// <summary>顶栏卡组按钮：触发它自己的重算（它内部会读真实张数）。</summary>
    public static void RefreshDeckButton(object node, CardPile pile)
    {
        var method = MethodOf(node.GetType(), "OnPileContentsChanged");
        if (method is null)
        {
            WarnOnce("顶栏卡组按钮上没有找到 OnPileContentsChanged，卡组张数只能靠本体自己刷新。");
            return;
        }

        method.Invoke(node, null);
    }

    private static void InvokeSetText(object label, string text)
    {
        var method = MethodOf(label.GetType(), "SetTextAutoSize", typeof(string));
        if (method is null)
        {
            WarnOnce("牌堆计数标签上没有找到 SetTextAutoSize，计数 UI 无法即时刷新。");
            return;
        }

        method.Invoke(label, [text]);
    }

    private static FieldInfo? FieldOf(Type type, string name)
    {
        var key = (type, name);
        if (FieldCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        FieldInfo? field;
        try
        {
            field = AccessTools.Field(type, name);
        }
        catch (Exception)
        {
            field = null;
        }

        if (field is null)
        {
            WarnOnce($"读不到 {type.Name}.{name}：本体改过这个字段，牌堆计数即时刷新会失效。");
        }

        FieldCache[key] = field;
        return field;
    }

    private static MethodInfo? MethodOf(Type type, string name, params Type[] parameters)
    {
        var key = (type, name);
        if (MethodCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        MethodInfo? method;
        try
        {
            method = parameters.Length == 0
                ? AccessTools.Method(type, name)
                : AccessTools.Method(type, name, parameters);
        }
        catch (Exception)
        {
            method = null;
        }

        MethodCache[key] = method;
        return method;
    }

    /// <summary>只警告一次。</summary>
    private static void WarnOnce(string message)
    {
        if (_warned)
        {
            return;
        }

        _warned = true;
        Log.Warn($"[together] {message}");
    }
}
