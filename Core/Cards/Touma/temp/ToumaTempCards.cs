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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempBasicAttack1 : ModCardTemplate
{
    public ToumaTempBasicAttack1() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempBasicAttack2 : ModCardTemplate
{
    public ToumaTempBasicAttack2() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempBasicAttack3 : ModCardTemplate
{
    public ToumaTempBasicAttack3() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempBasicSkill1 : ModCardTemplate
{
    public ToumaTempBasicSkill1() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempBasicSkill2 : ModCardTemplate
{
    public ToumaTempBasicSkill2() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempBasicSkill3 : ModCardTemplate
{
    public ToumaTempBasicSkill3() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempCommonAttack1 : ModCardTemplate
{
    public ToumaTempCommonAttack1() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempCommonAttack2 : ModCardTemplate
{
    public ToumaTempCommonAttack2() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempCommonAttack3 : ModCardTemplate
{
    public ToumaTempCommonAttack3() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempCommonSkill1 : ModCardTemplate
{
    public ToumaTempCommonSkill1() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempCommonSkill2 : ModCardTemplate
{
    public ToumaTempCommonSkill2() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempCommonSkill3 : ModCardTemplate
{
    public ToumaTempCommonSkill3() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempCommonPower1 : ModCardTemplate
{
    public ToumaTempCommonPower1() : base(1, CardType.Power, CardRarity.Common, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：普通能力 2。</summary>
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempCommonPower2 : ModCardTemplate
{
    public ToumaTempCommonPower2() : base(1, CardType.Power, CardRarity.Common, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：普通能力 3。</summary>
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempCommonPower3 : ModCardTemplate
{
    public ToumaTempCommonPower3() : base(1, CardType.Power, CardRarity.Common, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：罕见攻击 1。</summary>
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempUncommonAttack1 : ModCardTemplate
{
    public ToumaTempUncommonAttack1() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempUncommonAttack2 : ModCardTemplate
{
    public ToumaTempUncommonAttack2() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempUncommonAttack3 : ModCardTemplate
{
    public ToumaTempUncommonAttack3() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempUncommonSkill1 : ModCardTemplate
{
    public ToumaTempUncommonSkill1() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempUncommonSkill2 : ModCardTemplate
{
    public ToumaTempUncommonSkill2() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempUncommonSkill3 : ModCardTemplate
{
    public ToumaTempUncommonSkill3() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempUncommonPower1 : ModCardTemplate
{
    public ToumaTempUncommonPower1() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：罕见能力 2。</summary>
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempUncommonPower2 : ModCardTemplate
{
    public ToumaTempUncommonPower2() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：罕见能力 3。</summary>
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempUncommonPower3 : ModCardTemplate
{
    public ToumaTempUncommonPower3() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：稀有攻击 1。</summary>
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempRareAttack1 : ModCardTemplate
{
    public ToumaTempRareAttack1() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempRareAttack2 : ModCardTemplate
{
    public ToumaTempRareAttack2() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempRareAttack3 : ModCardTemplate
{
    public ToumaTempRareAttack3() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempRareSkill1 : ModCardTemplate
{
    public ToumaTempRareSkill1() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempRareSkill2 : ModCardTemplate
{
    public ToumaTempRareSkill2() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempRareSkill3 : ModCardTemplate
{
    public ToumaTempRareSkill3() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
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
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempRarePower1 : ModCardTemplate
{
    public ToumaTempRarePower1() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：稀有能力 2。</summary>
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempRarePower2 : ModCardTemplate
{
    public ToumaTempRarePower2() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

/// <summary>临时占位牌：稀有能力 3。</summary>
[RegisterCard(typeof(ToumaCardPool))]
public sealed class ToumaTempRarePower3 : ModCardTemplate
{
    public ToumaTempRarePower3() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => AlbumCardArt.Silent("blade_of_ink");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}

