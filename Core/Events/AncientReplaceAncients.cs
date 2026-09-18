using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Acts;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_WhiteAlbum2.Core.Ancients;

/// <summary>
/// 占位先古之民：每个给 3 个空白先古遗物，别的一概不做。
/// </summary>
/// <remarks>
/// <para>
/// 用 RitsuLib 的 <see cref="ModAncientEventTemplate" />：选项 key 自动带命名空间
/// （<c>&lt;先古id&gt;.pages.INITIAL.options.&lt;遗物id&gt;</c>），
/// <c>CreateModRelicOption&lt;T&gt;()</c> 会自动"给遗物 + 结束事件"。
/// </para>
/// <para>
/// 三幕各一个；第一幕本体有两个候选 Act（<c>Overgrowth</c> / <c>Underdocks</c>），两个都注册上。
/// </para>
/// <para>
/// <b>对话（talk 键）不是可选项，是必须的</b>：本体 <c>NEventRoom.SetupLayout</c> 会在进先古时
/// <c>Rng.Chaotic.NextItem(validDialogues)</c> 挑一段对话，列表为空时它返回 null，
/// 下一句 <c>ancientDialogue.Lines</c> 直接 <b>NullReferenceException</c> ——
/// 表现就是"进入先古之后黑屏卡住"。
/// 所以本地化里必须给 <c>firstVisitEver</c> 和 <c>ANY</c> 各一段（<c>ANY</c> 那段带 <c>r</c> 后缀=可重复），
/// 这样无论第几次来、用哪个角色，都有对话可选。
/// </para>
/// </remarks>
public abstract class BlankAncient : ModAncientEventTemplate
{
    /// <summary>只用本体捏奥的表现资源，避免缺资源黑屏。</summary>
    public override EventAssetProfile AssetProfile => NeowAssets.EventProfile;

    /// <inheritdoc />
    public override AncientEventPresentationAssetProfile AncientPresentationAssetProfile =>
        NeowAssets.PresentationProfile;

    /// <summary>本先古给出的三个空白遗物选项（三幕各自不同）。</summary>
    protected abstract IReadOnlyList<EventOption> RelicChoices { get; }

    /// <summary>调试/历史记录用的完整选项表。</summary>
    public override IEnumerable<EventOption> AllPossibleOptions => RelicChoices;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return RelicChoices;
    }
}

/// <summary>第一幕的占位先古。</summary>
[RegisterActAncient(typeof(Overgrowth))]
[RegisterActAncient(typeof(Underdocks))]
public sealed class ActOneAncient : BlankAncient
{
    protected override IReadOnlyList<EventOption> RelicChoices =>
    [
        CreateModRelicOption<ActOneRelicAlpha>(),
        CreateModRelicOption<ActOneRelicBeta>(),
        CreateModRelicOption<ActOneRelicGamma>(),
    ];
}

/// <summary>第二幕的占位先古。</summary>
[RegisterActAncient(typeof(Hive))]
public sealed class ActTwoAncient : BlankAncient
{
    protected override IReadOnlyList<EventOption> RelicChoices =>
    [
        CreateModRelicOption<ActTwoRelicAlpha>(),
        CreateModRelicOption<ActTwoRelicBeta>(),
        CreateModRelicOption<ActTwoRelicGamma>(),
    ];
}

/// <summary>第三幕的占位先古。</summary>
[RegisterActAncient(typeof(Glory))]
public sealed class ActThreeAncient : BlankAncient
{
    protected override IReadOnlyList<EventOption> RelicChoices =>
    [
        CreateModRelicOption<ActThreeRelicAlpha>(),
        CreateModRelicOption<ActThreeRelicBeta>(),
        CreateModRelicOption<ActThreeRelicGamma>(),
    ];
}
