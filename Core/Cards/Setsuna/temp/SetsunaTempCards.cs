using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_WhiteAlbum2.Core.Character;

namespace STS2_WhiteAlbum2.Core.Cards;
/// <summary>临时占位牌：基础攻击 1。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempBasicAttack1 : ModCardTemplate
{
    public SetsunaTempBasicAttack1() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("strike_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

/// <summary>临时占位牌：基础攻击 2。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempBasicAttack2 : ModCardTemplate
{
    public SetsunaTempBasicAttack2() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("strike_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

/// <summary>临时占位牌：基础攻击 3。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempBasicAttack3 : ModCardTemplate
{
    public SetsunaTempBasicAttack3() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("strike_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

/// <summary>临时占位牌：基础技能 1。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempBasicSkill1 : ModCardTemplate
{
    public SetsunaTempBasicSkill1() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("defend_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

/// <summary>临时占位牌：基础技能 2。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempBasicSkill2 : ModCardTemplate
{
    public SetsunaTempBasicSkill2() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("defend_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

/// <summary>临时占位牌：基础技能 3。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempBasicSkill3 : ModCardTemplate
{
    public SetsunaTempBasicSkill3() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("defend_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

/// <summary>临时占位牌：普通攻击 1。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempCommonAttack1 : ModCardTemplate
{
    public SetsunaTempCommonAttack1() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("strike_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(9m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

/// <summary>临时占位牌：普通攻击 2。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempCommonAttack2 : ModCardTemplate
{
    public SetsunaTempCommonAttack2() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("strike_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(9m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

/// <summary>临时占位牌：普通攻击 3。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempCommonAttack3 : ModCardTemplate
{
    public SetsunaTempCommonAttack3() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("strike_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(9m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

/// <summary>临时占位牌：普通技能 1。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempCommonSkill1 : ModCardTemplate
{
    public SetsunaTempCommonSkill1() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("defend_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

/// <summary>临时占位牌：普通技能 2。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempCommonSkill2 : ModCardTemplate
{
    public SetsunaTempCommonSkill2() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("defend_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

/// <summary>临时占位牌：普通技能 3。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempCommonSkill3 : ModCardTemplate
{
    public SetsunaTempCommonSkill3() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("defend_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

/// <summary>临时占位牌：普通能力 1。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempCommonPower1 : ModCardTemplate
{
    public SetsunaTempCommonPower1() : base(1, CardType.Power, CardRarity.Common, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：普通能力 2。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempCommonPower2 : ModCardTemplate
{
    public SetsunaTempCommonPower2() : base(1, CardType.Power, CardRarity.Common, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：普通能力 3。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempCommonPower3 : ModCardTemplate
{
    public SetsunaTempCommonPower3() : base(1, CardType.Power, CardRarity.Common, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：罕见攻击 1。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempUncommonAttack1 : ModCardTemplate
{
    public SetsunaTempUncommonAttack1() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("strike_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(12m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4m);
}

/// <summary>临时占位牌：罕见攻击 2。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempUncommonAttack2 : ModCardTemplate
{
    public SetsunaTempUncommonAttack2() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("strike_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(12m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4m);
}

/// <summary>临时占位牌：罕见攻击 3。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempUncommonAttack3 : ModCardTemplate
{
    public SetsunaTempUncommonAttack3() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("strike_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(12m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4m);
}

/// <summary>临时占位牌：罕见技能 1。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempUncommonSkill1 : ModCardTemplate
{
    public SetsunaTempUncommonSkill1() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("defend_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(11m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4m);
}

/// <summary>临时占位牌：罕见技能 2。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempUncommonSkill2 : ModCardTemplate
{
    public SetsunaTempUncommonSkill2() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("defend_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(11m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4m);
}

/// <summary>临时占位牌：罕见技能 3。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempUncommonSkill3 : ModCardTemplate
{
    public SetsunaTempUncommonSkill3() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("defend_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(11m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4m);
}

/// <summary>临时占位牌：罕见能力 1。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempUncommonPower1 : ModCardTemplate
{
    public SetsunaTempUncommonPower1() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：罕见能力 2。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempUncommonPower2 : ModCardTemplate
{
    public SetsunaTempUncommonPower2() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：罕见能力 3。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempUncommonPower3 : ModCardTemplate
{
    public SetsunaTempUncommonPower3() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：稀有攻击 1。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempRareAttack1 : ModCardTemplate
{
    public SetsunaTempRareAttack1() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("strike_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(16m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(5m);
}

/// <summary>临时占位牌：稀有攻击 2。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempRareAttack2 : ModCardTemplate
{
    public SetsunaTempRareAttack2() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("strike_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(16m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(5m);
}

/// <summary>临时占位牌：稀有攻击 3。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempRareAttack3 : ModCardTemplate
{
    public SetsunaTempRareAttack3() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("strike_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(16m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(5m);
}

/// <summary>临时占位牌：稀有技能 1。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempRareSkill1 : ModCardTemplate
{
    public SetsunaTempRareSkill1() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("defend_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(15m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(5m);
}

/// <summary>临时占位牌：稀有技能 2。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempRareSkill2 : ModCardTemplate
{
    public SetsunaTempRareSkill2() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("defend_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(15m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(5m);
}

/// <summary>临时占位牌：稀有技能 3。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempRareSkill3 : ModCardTemplate
{
    public SetsunaTempRareSkill3() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("defend_silent");

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(15m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(5m);
}

/// <summary>临时占位牌：稀有能力 1。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempRarePower1 : ModCardTemplate
{
    public SetsunaTempRarePower1() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：稀有能力 2。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempRarePower2 : ModCardTemplate
{
    public SetsunaTempRarePower2() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：稀有能力 3。</summary>
[RegisterCard(typeof(SetsunaCardPool))]
public sealed class SetsunaTempRarePower3 : ModCardTemplate
{
    public SetsunaTempRarePower3() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

