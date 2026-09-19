using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2_WhiteAlbum2.Core.Pvp;
using STS2_WhiteAlbum2.Core.Settings;
using System.Reflection;
using System.Threading.Tasks;

namespace STS2_WhiteAlbum2.Core.Patches.PvpEvent;

/// <summary>决斗战斗改造：布置、清怪、对手可击、目标定义</summary>
internal static class DuelCombatPatches
{
    /// <summary>
    /// 战斗初始化：每加进来一名玩家就对齐一次决斗布置。
    /// </summary>
    /// <remarks>
    /// 两个玩家是逐个加入战斗的，所以这里用 Postfix 在"第二位玩家进来之后"做改造。
    /// <see cref="DuelMode.EnsureApplied" /> 本身是幂等的，多调几次没有副作用。
    /// </remarks>
    [HarmonyPatch(typeof(CombatState), nameof(CombatState.AddPlayer))]
    [HarmonyPostfix]
    private static void ArmOnPlayerAddedPostfix(CombatState __instance)
    {
        DuelMode.EnsureApplied(__instance);

        // 场景这时肯定已经起来了：顺手把开关按键挂上（幂等）。

    }

    /// <summary>
    /// 纯玩家对决：怪物一律不进战斗。
    /// </summary>
    /// <remarks>
    /// 与其"先放进来再摘掉"（会牵动意图、血条、AI 一堆初始化），不如在这一步直接拦下 ——
    /// <c>AddCreature</c> 是怪物进入战斗的唯一入口，而 <c>Creature.Side</c> 决定它会被放到哪一侧，
    /// 未加入战斗的 creature 不会参与任何结算。
    /// 玩家自己的 creature 不受影响（决斗双方都还是正常的 Player）。
    /// </remarks>
    [HarmonyPatch(typeof(CombatState), nameof(CombatState.AddCreature))]
    [HarmonyPrefix]
    private static bool StripMonstersPrefix(CombatState __instance, Creature __0)
    {
        if (!WhiteAlbumSetting.DuelEnabled || __0 is null || __0.IsPlayer)
        {
            return true;
        }

        // 只有决斗那一场才拦怪物；普通战斗照旧生成。
        if (!DuelState.InDuel)
        {
            return true;
        }

        Log.Info($"[STS2_WhiteAlbum2] 决斗模式：拦下怪物 {__0.Monster?.Id.Entry ?? "?"} 不进战斗");
        return false;
    }

    /// <summary>
    /// 胜负：决斗里任意一方倒下就该结束。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 对手站在敌方侧，所以"对手倒下"本体会按"敌人全灭"判成<b>胜利</b>，不需要我们管。
    /// 需要接管的只有另一种情况：<b>我方侧（Players[0]）的玩家倒下</b>。
    /// 本体的失败判据是"所有玩家都死了"（<c>RunManager.Players.All(p =&gt; p.Creature.IsDead)</c>），
    /// 而对手此刻还活着、又在敌方侧，这个条件永远不成立 → 战斗会卡在那里，没人结束它。
    /// </para>
    /// <para>所以这里显式走一遍本体的失败收尾。</para>
    /// </remarks>
    [HarmonyPatch(
        typeof(CreatureCmd),
        nameof(CreatureCmd.Kill),
        new[] { typeof(IReadOnlyCollection<Creature>), typeof(bool) })]
    [HarmonyPrefix]
    private static void PlayerDownPrefix(bool force, out bool __state)
    {
        // 主动放弃这一局（force）不算决斗分出胜负，走本体原本的流程即可。
        __state = force;
    }

    [HarmonyPatch(
        typeof(CreatureCmd),
        nameof(CreatureCmd.Kill),
        new[] { typeof(IReadOnlyCollection<Creature>), typeof(bool) })]
    [HarmonyPostfix]
    private static void PlayerDownPostfix(IReadOnlyCollection<Creature> __0, bool __state)
    {
        if (__state || !WhiteAlbumSetting.DuelEnabled || !DuelState.InDuel || __0 is null)
        {
            return;
        }

        var deadPlayer = __0.FirstOrDefault(creature => creature.IsPlayer && creature.IsDead);
        if (deadPlayer?.CombatState is not { } state)
        {
            return;
        }

        if (!DuelMode.IsDuelActive(state))
        {
            return;
        }

        // F 方案下两个人都在我方侧，本体的"所有玩家都死了"只在真正全灭时成立；
        // 决斗要的是"谁先倒下谁输"，所以这里判断"倒下的是不是本机那位"。
        var me = LocalContext.GetMe(state)?.Creature;
        var iLost = ReferenceEquals(deadPlayer, me);

        DuelMode.EndRun(victory: !iLost);
    }

    /// <summary>本体的收尾方法（private）：发校验和之后要靠它推进到下一轮。</summary>
    private static readonly MethodInfo? EndEnemyTurnMethod =
        AccessTools.Method(typeof(CombatManager), "EndEnemyTurn");

    private static readonly PropertyInfo? StateProperty =
        AccessTools.Property(AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatTurnState"), "State");

    private static readonly AccessTools.FieldRef<CombatManager, bool> ActionsDisabledField =
        AccessTools.FieldRefAccess<CombatManager, bool>("_playerActionsDisabled");

    /// <summary>
    /// 路线 A：接管敌方回合，让"站在敌方侧的玩家"真的能操作。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本体敌方回合（<c>CombatManager.ExecuteEnemyTurn</c>）是给怪物用的：对每个敌方单位
    /// 演意图 + 跑 AI 回合。对手站在敌方侧之后,那里必须换一套流程 ——
    /// 否则 <c>Creature.TakeTurn()</c> 会直接抛
    /// 「Only enemy monsters can take automated turns」把敌方回合打死。
    /// </para>
    /// <para>
    /// 这里把整个敌方回合接管下来，对"玩家单位"改成跑一遍<b>玩家回合的开场</b>
    /// （重置能量 → 抽牌 → 回合开始钩子），把 phase 设成可出牌，然后<b>等他点结束回合</b>。
    /// 开场那几步是照着本体 <c>CombatManager.SetupPlayerTurn</c> 复刻的 —— 它本身是 private，
    /// 而且需要一个由 <c>StartTurn</c> 内部创建的 <c>HookPlayerChoiceContext</c>，外面拿不到；
    /// 但那个 context 的构造函数是公开的（<c>new HookPlayerChoiceContext(player, netId, type)</c>），
    /// 所以复刻是可行的：用到的 <c>Hook.*</c> 与 <c>CardPileCmd.Draw</c> 都是公开 API。
    /// </para>
    /// <para>
    /// 阻塞等待靠 <see cref="DuelTurnBridge" />（登记等待 + <c>SetReadyToEndTurn</c> 放行）。
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(CombatManager), "ExecuteEnemyTurn")]
    [HarmonyPrefix]
    private static bool EnemyTurnPrefix(CombatManager __instance, object __0, object __1, ref Task __result)
    {
        if (!WhiteAlbumSetting.DuelEnabled || !DuelState.InDuel || EndEnemyTurnMethod is null || StateProperty is null)
        {
            return true;
        }

        __result = RunEnemyTurnAsync(__instance, __0, __1);
        return false;
    }

    private static async Task RunEnemyTurnAsync(CombatManager manager, object turnState, object actionDuringEnemyTurn)
    {
        if (StateProperty?.GetValue(turnState) is not ICombatState state)
        {
            return;
        }

        // 本体原本的第一步：等外部注入的动作（测试用；正常流程里是 null）。
        if (actionDuringEnemyTurn is Func<Task> action)
        {
            await action();
        }

        foreach (var enemy in state.Enemies.ToList())
        {
            if (!state.ContainsCreature(enemy))
            {
                continue;
            }

            if (enemy.IsPlayer && enemy.Player is { } opponent)
            {
                await RunOpponentTurnAsync(state, opponent);
                continue;
            }

            // 决斗里不该出现怪物（AddCreature 已经拦下了）；真出现也不驱动它，避免误伤玩家。
            Capped.LogOnce($"[STS2_WhiteAlbum2] 敌方侧出现非玩家单位，已跳过：{enemy.Monster?.Id.Entry ?? "?"}");
        }

        RunManager.Instance.ChecksumTracker.GenerateChecksum("After enemy turn end", null);

        try
        {
            if (EndEnemyTurnMethod?.Invoke(manager, [turnState]) is Task endTask)
            {
                await endTask;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[STS2_WhiteAlbum2] 收尾敌方回合失败：{ex}");
        }
    }

    /// <summary>对手的"回合"：复刻本体的玩家回合开场，然后等他自己结束回合。</summary>
    private static async Task RunOpponentTurnAsync(ICombatState state, Player opponent)
    {
        if (opponent.Creature.IsDead || opponent.PlayerCombatState is null)
        {
            return;
        }

        Log.Info($"[STS2_WhiteAlbum2] 对手 netId={opponent.NetId} 的回合开始");

        // 这个 context 就是本体 StartTurn 里造的那个东西；构造函数是公开的，所以能自己造。
        var choiceContext = new HookPlayerChoiceContext(
            opponent,
            LocalContext.NetId ?? opponent.NetId,
            GameActionType.CombatPlayPhaseOnly);

        // —— 以下照抄 CombatManager.SetupPlayerTurn（本体 880-927 行）——
        if (Hook.ShouldPlayerResetEnergy(state, opponent))
        {
            opponent.PlayerCombatState.ResetEnergy();
        }
        else
        {
            opponent.PlayerCombatState.AddMaxEnergyToCurrent();
        }

        await Hook.AfterEnergyReset(state, opponent);

        await Hook.BeforeHandDraw(state, opponent, choiceContext);
        var handDraw = Hook.ModifyHandDraw(state, opponent, 5m, out var modifiers);
        await Hook.AfterModifyingHandDraw(state, modifiers);

        await CardPileCmd.Draw(choiceContext, handDraw, opponent, fromHandDraw: true);
        await Hook.AfterPlayerTurnStart(state, choiceContext, opponent);
        // —— 照抄结束 ——

        // 让他能出牌（本体 RunAutoPrePlayPhase 最后做的就是这一步）。
        opponent.PlayerCombatState.Phase = PlayerTurnPhase.Play;

        // 敌方回合里本体把"玩家操作"禁用了（那回合本来是给怪物的）；决斗里这回合是玩家的，要放开，
        // 否则对手看得到手牌却点不动、也就永远点不了结束回合。
        ActionsDisabledField(CombatManager.Instance) = false;
        Capped.LogOnce("[STS2_WhiteAlbum2] 敌方回合里放开了玩家操作（这个回合其实是玩家的）");

        // 等他自己点"结束回合"。
        var completion = DuelTurnBridge.Expect(opponent);
        await completion.Task;

        Log.Info($"[STS2_WhiteAlbum2] 对手 netId={opponent.NetId} 结束了回合");
    }

    /// <summary>玩家点"结束回合"时放行对应的等待。</summary>
    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetReadyToEndTurn))]
    [HarmonyPostfix]
    private static void EndTurnSignalPostfix(Player player)
    {
        if (!WhiteAlbumSetting.DuelEnabled || player is null)
        {
            return;
        }

        DuelTurnBridge.Signal(player.NetId);
    }

    /// <summary>
    /// 第三层：把「敌人」从"共享一份列表"改成"每个玩家各有自己的视角"。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本体里"谁是敌人"只有一个来源：<c>CombatState.HittableEnemies</c>
    /// （<c>Enemies.Where(e =&gt; e.IsHittable)</c>）。而它同时被两处使用：
    /// </para>
    /// <list type="bullet">
    /// <item><description>规则层：几乎所有攻击牌、AOE、随机目标、能力触发都读它；</description></item>
    /// <item><description>UI 层：<c>NCardPlay</c> 拖牌时拿它当可选目标（这就是"能指向谁"的来源）。</description></item>
    /// </list>
    /// <para>
    /// 决斗时对手被我挪到了敌方侧，于是那份共享列表是 <c>[对手, 假人]</c>：
    /// 对"我方侧那位"完全正确，但对"站在敌方侧的那位"错了 —— 他自己也在列表里，
    /// 而真正的对手（我方侧那位）却不在，导致他既打不到人、候选里还多了个自己。
    /// </para>
    /// <para>
    /// 这里按<b>本机玩家</b>（<c>LocalContext.NetId</c>）重算一次：
    /// <c>候选 = (Enemies - 自己) + 另一个玩家</c>。于是两边各看各的：
    /// 我方侧看到 <c>[对手, 假人]</c>，敌方侧看到 <c>[我方那位, 假人]</c>。
    /// </para>
    /// <para>
    /// 这是<b>本地视角</b>，不涉及跨端一致性 —— 目标选择本来就是本地 UI 行为，
    /// 真正的伤害由同步的动作结算，所以两端视角不同不会分叉。
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(CombatState), "get_HittableEnemies")]
    [HarmonyPostfix]
    private static void HittableOpponentPostfix(CombatState __instance, ref IReadOnlyList<Creature> __result)
    {
        if (!WhiteAlbumSetting.DuelEnabled || !DuelState.InDuel || __result is null)
        {
            return;
        }

        var players = __instance.Players;
        if (players.Count != 2)
        {
            return;
        }

        var (mine, theirs) = SplitPerspective(players);

        if (mine is null || theirs is null)
        {
            return;
        }

        var merged = new List<Creature>(__result.Count + 1);

        foreach (var creature in __result)
        {
            // 自己绝不能出现在自己的攻击候选里（敌方侧的那位正好是这种情况）。
            if (!ReferenceEquals(creature, mine))
            {
                merged.Add(creature);
            }
        }

        // 把"真正的对手"补进来：我方侧那位本来就在 Enemies 里，
        // 敌方侧那位则需要补（对他而言对手是我方侧的人）。
        if (theirs.IsAlive && !merged.Any(creature => ReferenceEquals(creature, theirs)))
        {
            merged.Add(theirs);
        }

        Capped.LogOnce(
            $"[STS2_WhiteAlbum2] 按本机视角重算可选敌人：本机={mine.Player?.NetId ?? 0} "
            + $"对手={theirs.Player?.NetId ?? 0} 候选={merged.Count} 个");

        __result = merged;
    }

    /// <summary>按本机玩家把两位分成「我」与「对手」；认不出本机时退回 Players 顺序。</summary>
    private static (Creature? Mine, Creature? Theirs) SplitPerspective(IReadOnlyList<Player> players)
    {
        var localId = LocalContext.NetId;

        if (localId is { } id)
        {
            Creature? mine = null;
            Creature? theirs = null;

            foreach (var player in players)
            {
                if (player.NetId == id)
                {
                    mine = player.Creature;
                }
                else
                {
                    theirs = player.Creature;
                }
            }

            if (mine is not null && theirs is not null)
            {
                return (mine, theirs);
            }
        }

        return (players[0].Creature, players[1].Creature);
    }

    /// <summary>
    /// 决斗里的战斗不能因为「敌方侧没人」就结束。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本体 CombatManager.IsCombatEnding 的判据是：敌方侧只要有活的 PrimaryEnemy 就继续，
    /// 否则就认为敌人全灭、战斗结束（胜利）。
    /// </para>
    /// <para>
    /// 决斗是清掉所有怪物、两个玩家互相打，所以一开打敌方侧本来就是空的 ——
    /// 于是战斗刚开始就被判成「敌人全灭、胜利」，玩家什么都没做就弹结算
    /// （实测表现就是「直接跳过战斗」）。
    /// </para>
    /// <para>
    /// 这里在决斗模式下把这个结果压成 false，让战斗继续；胜负交给
    /// DuelPlayerDownPatch（谁先倒下）和 DuelRoundLimit（回合上限）。
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(CombatManager), "IsCombatEnding")]
    [HarmonyPostfix]
    private static void KeepCombatAlivePostfix(ref bool __result)
    {
        if (!__result || !WhiteAlbumSetting.DuelEnabled || !DuelState.InDuel)
        {
            return;
        }

        Capped.LogOnce("[STS2_WhiteAlbum2] 决斗：敌方侧没有怪物，但不结束战斗（避免开局即判定胜利）");
        __result = false;
    }

    private static readonly AccessTools.FieldRef<NCombatRoom, Control> EnemyContainerField =
        AccessTools.FieldRefAccess<NCombatRoom, Control>("_enemyContainer");

    /// <summary>
    /// 两个决斗位离画面中线的水平距离。
    /// 480 正好是本体摆"单个敌人"时的槽位（<c>PositionEnemies</c>：<c>(960 - 宽度) / 2 + 宽度 / 2</c>），
    /// 比本体给玩家算出来的 ~320 更开，两人之间留出空档。
    /// </summary>
    private const float SlotX = 480f;

    /// <summary>本体摆位用的高度（玩家、敌人用的都是这个 y）。</summary>
    private const float SlotY = 200f;

    /// <summary>
    /// 决斗站位：<b>P1 固定站左边、P2 固定站右边，两人拉开、面对面</b>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本体 <c>NCombatRoom.AddCreature</c> 按 <c>Creature.Side</c> 决定节点进 <c>%AllyContainer</c>
    /// 还是 <c>%EnemyContainer</c>；决斗里两个玩家都保持我方身份（方案③），所以两个立绘都会被塞进
    /// 己方容器，而且位置是按"本机玩家排第一"算的 —— 结果是<b>不同机器上谁在左谁在右还不一样</b>。
    /// 这里干脆全部钉死：位置自己摆，朝向自己翻。
    /// </para>
    /// <para>
    /// <b>光搬容器是没用的</b>：<c>%AllyContainer</c> 与 <c>%EnemyContainer</c> 在场景里都锚在正中央
    /// （<c>anchors_preset = 8</c>），位置完全一样，reparent 之后算出的全局坐标一模一样，
    /// 画面上一像素都不会动 —— 之前日志显示"已挪到 EnemyContainer"却看不出变化就是这个原因。
    /// 所以位置必须自己写。
    /// </para>
    /// <para>
    /// <b>朝向</b>用本体的做法（<c>SurroundedPower</c> 让帝王蟹左右转头就是这一招）：
    /// 镜像 <c>NCreature.Body</c> 的 <c>Scale.X</c>，另外连带镜像 <c>FormVfxHolder</c>（变身特效容器）。
    /// 镜像只动视觉，判定用的 <c>Hitbox</c> 来自 <c>%Bounds</c>，不受影响。
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature))]
    [HarmonyPostfix]
    private static void OpponentVisualPostfix(NCombatRoom __instance, Creature __0)
    {
        if (!WhiteAlbumSetting.DuelEnabled || !DuelState.InDuel || __0 is null || !__0.IsPlayer)
        {
            return;
        }

        if (__0.CombatState is not { } state || state.Players.Count != 2)
        {
            return;
        }

        // 只在"第二位玩家（P2）进场"时摆一次：那时两个人的节点都已经建好了。
        if (!ReferenceEquals(state.Players[1].Creature, __0))
        {
            return;
        }

        try
        {
            // 延后一帧：AddCreature 是在建节点的过程中调用的，本体紧接着还会
            // PositionPlayersAndPets 统一摆位，等它摆完我们再覆盖。
            Callable.From(() => ApplyDuelLayout(__instance, state)).CallDeferred();
        }
        catch (Exception ex)
        {
            Log.Warn($"[STS2_WhiteAlbum2] 决斗站位失败（不影响战斗）：{ex.Message}");
        }
    }

    private static void ApplyDuelLayout(NCombatRoom room, ICombatState state)
    {
        if (!GodotObject.IsInstanceValid(room) || state.Players.Count != 2)
        {
            return;
        }

        var p1 = state.Players[0];
        var p2 = state.Players[1];

        Place(room, p1.Creature, onLeft: true);
        Place(room, p2.Creature, onLeft: false);

        Capped.LogOnce(
            $"[STS2_WhiteAlbum2] 决斗站位：P1(netId={p1.NetId}) 左 {room.GetCreatureNode(p1.Creature)?.Position}"
            + $"，P2(netId={p2.NetId}) 右 {room.GetCreatureNode(p2.Creature)?.Position}"
            + $"（右侧立绘已反向）");
    }

    private static void Place(NCombatRoom room, Creature creature, bool onLeft)
    {
        if (room.GetCreatureNode(creature) is not { } node)
        {
            return;
        }

        // 右边那位搬到敌方容器：两个容器位置相同，所以这只影响绘制层级（敌方容器画在后面=更靠前）。
        if (!onLeft)
        {
            var enemyContainer = EnemyContainerField(room);

            if (enemyContainer is not null && node.GetParent() != enemyContainer)
            {
                node.GetParent()?.RemoveChild(node);
                enemyContainer.AddChildSafely(node);
            }
        }

        node.Position = new Vector2(onLeft ? -SlotX : SlotX, SlotY);

        // 右边的立绘反个向，两人面对面。
        SetFacing(node, faceLeft: !onLeft);

        // 保险：鼠标能不能"悬停到"这个立绘取决于 Hitbox 的 MouseFilter；
        // 原版里队友的立绘本来就能悬停（移上去会显示血条），万一被关成 Ignore 就补回来。
        if (node.Hitbox is { } hitbox && hitbox.MouseFilter == Control.MouseFilterEnum.Ignore)
        {
            hitbox.MouseFilter = Control.MouseFilterEnum.Stop;
            Capped.LogOnce("[STS2_WhiteAlbum2] 对手立绘的 Hitbox 原本不可交互，已恢复（否则鼠标点不到）");
        }
    }

    /// <summary>照本体 <c>SurroundedPower.FaceDirection</c> 的做法翻转朝向（幂等：方向对了就不动）。</summary>
    private static void SetFacing(NCreature node, bool faceLeft)
    {
        FlipX(node.Body, faceLeft);
        FlipX(node.Visuals?.FormVfxHolder, faceLeft);
    }

    private static void FlipX(Node2D? body, bool faceLeft)
    {
        if (body is null)
        {
            return;
        }

        var x = body.Scale.X;

        if ((faceLeft && x > 0f) || (!faceLeft && x < 0f))
        {
            body.Scale *= new Vector2(-1f, 1f);
        }
    }

    private static void FlipX(Control? body, bool faceLeft)
    {
        if (body is null)
        {
            return;
        }

        var x = body.Scale.X;

        if ((faceLeft && x > 0f) || (!faceLeft && x < 0f))
        {
            body.Scale *= new Vector2(-1f, 1f);
        }
    }

    /// <summary>
    /// 闸门①：卡牌层——决斗里 AnyEnemy 的"对手"是另一位玩家，AnyAlly 则不再成立。
    /// </summary>
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.IsValidTarget))]
    [HarmonyPostfix]
    private static void CardTargetValidityPostfix(CardModel __instance, Creature? target, ref bool __result)
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
            ? "[STS2_WhiteAlbum2] 目标校验：对手被当作「敌人」放行（AnyEnemy）"
            : "[STS2_WhiteAlbum2] 目标校验：对手不再被当作「队友」（AnyAlly）");
    }

    /// <summary>
    /// 闸门①的孪生兄弟：药水走的是另一套校验（<c>PotionModel.IsValidTarget</c>），
    /// 不一起改的话会出现"选中 UI 让点，点完药水用不出去"的怪现象。
    /// </summary>
    [HarmonyPatch(typeof(PotionModel), nameof(PotionModel.IsValidTarget))]
    [HarmonyPostfix]
    private static void PotionTargetValidityPostfix(PotionModel __instance, Creature? target, ref bool __result)
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

        Capped.LogOnce("[STS2_WhiteAlbum2] 目标校验：药水也可以指向对手了");
    }

    /// <summary>当前正在选的目标类型（private 字段，取不到就整体不生效，不影响原版）。</summary>
    private static readonly AccessTools.FieldRef<NTargetManager, TargetType>? ValidTargetsField =
        AccessTools.Field(typeof(NTargetManager), "_validTargetsType") is null
            ? null
            : AccessTools.FieldRefAccess<NTargetManager, TargetType>("_validTargetsType");

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
    [HarmonyPostfix]
    private static void TargetManagerOpponentPostfix(NTargetManager __instance, Creature creature, ref bool __result)
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
            Capped.LogOnce("[STS2_WhiteAlbum2] 选中 UI：对手现在可以悬停/点击选中（当作敌人）");
        }
        else if (targetType == TargetType.AnyAlly)
        {
            // 1v1：对面不是队友，"指向队友"的牌在决斗里没有合法目标。
            __result = false;
        }
    }

    /// <summary>
    /// 诊断：把 <c>StartCombatInternal</c> 里被吞掉的异常完整打出来。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本体启动战斗的那条链路异常处理很"安静"：
    /// <c>RunTurnLoopAfter</c> → <c>TaskHelper.LogTaskExceptions</c> 只打顶层那一层栈，
    /// 于是 log 里只剩 <c>at CombatManager.StartCombatInternal(...)</c> 一行，
    /// 根本看不出是哪一句炸的。
    /// </para>
    /// <para>
    /// 这里把返回的 <c>Task</c> 包一层，用 <c>ex.ToString()</c> 打印类型、消息和<b>完整栈</b>，
    /// 再用 <c>throw</c> 原样抛回去，不改变本体的行为。定位完可以整体删掉这个文件。
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(CombatManager), "StartCombatInternal")]
    [HarmonyPostfix]
    private static void StartCombatDiagPostfix(ref Task __result)
    {
        if (!WhiteAlbumSetting.DuelEnabled || __result is null)
        {
            return;
        }

        __result = Wrap(__result);
    }

    private static async Task Wrap(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            // 战斗被取消（离房、退出、读档）是正常路径：本体的回合循环在收尾时一律以取消结束。
            if (ex is TaskCanceledException or OperationCanceledException)
            {
                throw;
            }

            Log.Error($"[STS2_WhiteAlbum2] 战斗启动失败（完整异常）：{ex}");
            throw;
        }
    }
}

/// <summary>决斗里"谁和谁"的判定（纯本机表现层的判断，不动战斗数据）。</summary>
internal static class TogetherPlayers
{
    /// <summary>本机视角下的"对手"：本机玩家以外的那位（认不出本机时退回 Players[1]）。</summary>
    public static bool IsOpponentOfLocal(Creature? creature)
    {
        if (creature is not { IsPlayer: true })
        {
            return false;
        }

        var state = creature.CombatState;
        if (state is null || state.Players.Count != 2)
        {
            return false;
        }

        if (LocalContext.NetId is { } id)
        {
            foreach (var player in state.Players)
            {
                if (player.NetId == id)
                {
                    return !ReferenceEquals(player.Creature, creature);
                }
            }
        }

        return ReferenceEquals(state.Players[1].Creature, creature);
    }
}

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
        return WhiteAlbumSetting.DuelEnabled
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
