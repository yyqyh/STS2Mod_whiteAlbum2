using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS_WhiteAlbum2.Core.Ancients;

/// <summary>
/// 占位 boss：<b>100 血，只会一招"攻击 1"</b>，形象借本体已有的怪物。
/// </summary>
/// <remarks>
/// <para>
/// 形象借本体是为了避开"自定义 Spine 资源缺失" ——那是最容易卡住/黑屏的一环。
/// 借来之后逻辑全部由我们自己给：一个 <see cref="MoveState" />，自己指向自己，
/// 所以意图永远只有一个"攻击 1"。
/// </para>
/// <para>
/// 血量不给任何升华加成，方便测试时一眼看出是不是这个占位 boss。
/// </para>
/// </remarks>
public abstract class PlaceholderBoss : ModMonsterTemplate
{
    /// <summary>借哪只本体怪的形象场景。</summary>
    protected abstract string BorrowedVisualsScene { get; }

    public override int MinInitialHp => ReplacePlan.PlaceholderHp;

    public override int MaxInitialHp => ReplacePlan.PlaceholderHp;

    /// <summary>直接覆盖本体那条"按 id 拼路径"的逻辑，保证一定用到借来的形象。</summary>
    protected override string VisualsPath => BorrowedVisualsScene;

    /// <summary>同一份路径也交给 RitsuLib 的资源替换/预加载用。</summary>
    public override MonsterAssetProfile AssetProfile => new(VisualsScenePath: BorrowedVisualsScene);

    /// <summary>没有技能、没有 buff，只循环这一招。</summary>
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var attack = new MoveState(
            "PLACEHOLDER_ATTACK",
            AttackMove,
            new SingleAttackIntent(ReplacePlan.PlaceholderAttackDamage));

        attack.FollowUpState = attack;

        return new MonsterMoveStateMachine([attack], attack);
    }

    private async Task AttackMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ReplacePlan.PlaceholderAttackDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.2f)
            .Execute(null);
    }
}

// —— 四只占位怪 ——
[RegisterMonster]
public sealed class PlaceholderBossOne : PlaceholderBoss
{
    protected override string BorrowedVisualsScene => "res://scenes/creature_visuals/battle_friend_v1.tscn";
}

[RegisterMonster]
public sealed class PlaceholderBossTwo : PlaceholderBoss
{
    protected override string BorrowedVisualsScene => "res://scenes/creature_visuals/corpse_slug.tscn";
}

[RegisterMonster]
public sealed class PlaceholderBossThree : PlaceholderBoss
{
    protected override string BorrowedVisualsScene => "res://scenes/creature_visuals/byrdonis.tscn";
}

[RegisterMonster]
public sealed class PlaceholderBossFour : PlaceholderBoss
{
    protected override string BorrowedVisualsScene => "res://scenes/creature_visuals/crusher.tscn";
}

/// <summary>
/// 占位 boss 的"遭遇"：一只怪、没有站位槽位、地图图标统一用捏奥的。
/// </summary>
/// <remarks>
/// <para>
/// <c>RoomType.Boss</c> 是必须的：只有 Boss 房才会走本体的 boss 结算/进入下一幕那条流程。
/// </para>
/// <para>
/// 地图图标：<c>BossNodeSpineResource</c> 返回 null 时，本体 <c>NBossMapPoint</c> 会去读
/// <c>BossNodePath + ".png"</c> 和 <c>+ "_outline.png"</c>，这里把 <c>BossNodePath</c>
/// 指向捏奥的先古节点图，于是 boss 节点也用捏奥的图标。
/// </para>
/// <para>
/// 不给遭遇单独的场景（<c>HasScene = false</c>），怪物就由本体按普通方式摆位，
/// 少一处"缺场景"的坑。
/// </para>
/// </remarks>
public abstract class PlaceholderBossEncounter : ModEncounterTemplate
{
    /// <summary>这场遭遇放哪只占位怪。</summary>
    protected abstract Type MonsterType { get; }

    public override RoomType RoomType => RoomType.Boss;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.GetById<MonsterModel>(ModelDb.GetId(MonsterType))];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [(ModelDb.GetById<MonsterModel>(ModelDb.GetId(MonsterType)).ToMutable(), null)];

    /// <summary>地图节点图标 → 捏奥（不带扩展名，本体自己会拼 .png / _outline.png）。</summary>
    public override string BossNodePath => NeowAssets.MapNodeStem;

    /// <summary>明确"没有 spine"，本体才会走上面那两张 png。</summary>
    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    /// <summary>历史记录里的图标也借捏奥的。</summary>
    public override EncounterAssetProfile AssetProfile => new(
        RunHistoryIconPath: NeowAssets.RunHistoryIcon,
        RunHistoryIconOutlinePath: NeowAssets.RunHistoryIconOutline);
}

// —— 四个占位 boss 遭遇 ——
[RegisterActEncounter(typeof(Overgrowth))]
[RegisterActEncounter(typeof(Underdocks))]
public sealed class PlaceholderBossEncounterOne : PlaceholderBossEncounter
{
    protected override Type MonsterType => typeof(PlaceholderBossOne);
}

[RegisterActEncounter(typeof(Hive))]
public sealed class PlaceholderBossEncounterTwo : PlaceholderBossEncounter
{
    protected override Type MonsterType => typeof(PlaceholderBossTwo);
}

[RegisterActEncounter(typeof(Glory))]
public sealed class PlaceholderBossEncounterThree : PlaceholderBossEncounter
{
    protected override Type MonsterType => typeof(PlaceholderBossThree);
}

[RegisterActEncounter(typeof(Glory))]
public sealed class PlaceholderBossEncounterFour : PlaceholderBossEncounter
{
    protected override Type MonsterType => typeof(PlaceholderBossFour);
}
