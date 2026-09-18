using Godot;

using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace STS_WhiteAlbum2.Core.Pvp;

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
internal static class DuelOpponentVisualPatch
{
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

    [HarmonyPostfix]
    private static void Postfix(NCombatRoom __instance, Creature __0)
    {
        if (!DuelConfig.Enabled || !DuelState.InDuel || __0 is null || !__0.IsPlayer)
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
            Log.Warn($"[STS_WhiteAlbum2] 决斗站位失败（不影响战斗）：{ex.Message}");
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
            $"[STS_WhiteAlbum2] 决斗站位：P1(netId={p1.NetId}) 左 {room.GetCreatureNode(p1.Creature)?.Position}"
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
            Capped.LogOnce("[STS_WhiteAlbum2] 对手立绘的 Hitbox 原本不可交互，已恢复（否则鼠标点不到）");
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
