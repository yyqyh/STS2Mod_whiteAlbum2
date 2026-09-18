using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Rewards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS_WhiteAlbum2.Core.Together.Multiplayer;

namespace STS_WhiteAlbum2.Core.Pvp;

/// <summary>
/// 决斗的入口事件：「对决邀请」。
/// </summary>
/// <remarks>
/// <para>
/// 结构照抄酒狐（STS2_WineFox）的事件写法：继承 RitsuLib 的 <see cref="ModEventTemplate" />，
/// 用 <c>[RegisterActEvent]</c> 挂到第一幕的事件池，文案走 <c>localization/zhs/events.json</c>
/// （<c>InitialOptionKey</c> / <c>PageDescription</c> 会按 <c>&lt;事件Id&gt;.pages...</c> 拼键）。
/// </para>
/// <para>
/// <b>只有一个选项</b>是有意的：这个事件本身就是"被挑战"，玩家在进节点时已经做了选择
/// （不想要决斗可以关掉 mod 开关）。
/// </para>
/// <para>
/// 进战斗走的是<b>选项回调里的 <c>EnterCombatWithoutExitingEvent</c></b>，而不是覆盖
/// <c>CanonicalEncounter</c>。后者会让本体走"事件战斗"那条更早的初始化路径 ——
/// 实测那条路上事件还没绑好 Owner，<c>EventOption</c> 构造时会在
/// <c>CharacterModel.AddDetailsTo</c> 里 NRE。
/// </para>
/// <para>
/// 那个遭遇只是<b>占位</b>——它的怪物会被决斗逻辑在 <c>CombatState.AddCreature</c> 处全部拦下，
/// 实际打的是"两个玩家轮流行动"的决斗。
/// </para>
/// </remarks>
[RegisterActEvent(typeof(Overgrowth))]
public sealed class DuelInvitationEvent : ModEventTemplate
{
    /// <summary>联机时事件对所有玩家一致（决斗必须是两个人的事）。</summary>
    public override bool IsShared => true;

    /// <summary>
    /// 立绘路径显式指定（照酒狐的写法）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>目前借用本体的立绘</b>：先用「战痕累累的训练假人」这张图（打靶的意象跟决斗挺搭）。
    /// </para>
    /// <para>
    /// 之所以不塞自己的图：RitsuLib 判存在性走的是 <c>ResourceLoader.Exists</c>，
    /// 实测 mod 包里的 png（<c>res://</c> 路径与 <c>uid://</c> 都试过）在那个索引里查不到 ——
    /// 图确实打进 PCK 了，但运行时读不到。本体的资源一定在索引里，所以直接引用它最省事。
    /// </para>
    /// <para>
    /// 以后要换成自己的立绘，把这张图换成 <c>res://STS_WhiteAlbum2/images/events/xxx.png</c> 即可，
    /// 但要先把"mod 资源进不了索引"这件事解决掉。
    /// </para>
    /// </remarks>
    public override EventAssetProfile AssetProfile =>
        new(InitialPortraitPath: "res://images/events/battleworn_dummy.png");

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new(this, BeginDuel, InitialOptionKey("BEGIN")),
        ];
    }

    private Task BeginDuel()
    {
        if (!DuelConfig.Enabled)
        {
            Log.Warn("[STS_WhiteAlbum2] 决斗事件被触发，但本局不满足“两个不同 mod 角色”条件，忽略");
            SetEventFinished(PageDescription("BEGIN"));
            return Task.CompletedTask;
        }

        // 决斗是"两个人各自为战"：先把共生体关系解除，并把共享卡组按奇偶分给两边。
        TogetherPair.UnbindForDuel("duel_invitation");

        // 登记"下一场战斗是决斗"：这样只有这场受影响，其它战斗照旧。
        DuelState.ArmForNextCombat();

        // 进入战斗（占位遭遇，怪物会被决斗逻辑在 AddCreature 处全部拦下），然后收掉事件页。
        EnterCombatWithoutExitingEvent(ModelDb.Encounter<CorpseSlugsWeak>(), [], shouldResumeAfterCombat: false);

        SetEventFinished(PageDescription("BEGIN"));
        return Task.CompletedTask;
    }
}
