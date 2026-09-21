using STS2_WhiteAlbum2.Core.Character;

namespace STS2_WhiteAlbum2.Core.Settings;

/// <summary>本 mod 的全部持久化设置（together + pvp + 通用）。</summary>
/// <remarks>加字段给个合理默认值即可，老配置缺字段时反序列化会取默认值，不用写迁移。</remarks>
public sealed class WhiteAlbumSetting
{
    // ==================== together ====================

    /// <summary>是否开启共生体。</summary>
    public bool SymbiosisEnabled { get; set; } = true;

    /// <summary>开局是否把回声（p2）的初始卡组复制进共享卡组。</summary>
    public bool MergeStarterDecks { get; set; } = true;

    /// <summary>共享血池上限提升：p2 最大生命的百分比（0~100）。只在新开一局生效一次。</summary>
    public int HpBonusPercent { get; set; }

    /// <summary>共生体人数上限（2~4）。</summary>
    public int GroupSize { get; set; } = 2;

    /// <summary>是否共享金币。</summary>
    public bool ShareGold { get; set; } = true;

    /// <summary>是否把原版事件改成共享事件（两人投票，只有一次选择）。</summary>
    /// <remarks>
    /// <para>背景：联机时每个玩家各有一份事件实例，而共生体共用一副卡组 —— 两个人可能同时对同一张牌选附魔/移除/升级。</para>
    /// <para>开启：非共享事件按共享事件处理，两人投票、只有一个选择，从根上消除并发。</para>
    /// <para>代价：事件奖励由"每人一份"变成"整组一份"；共享事件结束时不再发校验和。</para>
    /// <para>不开也能用：默认会在共享卡组变动时刷新另一个人的选牌界面，并在应用前拦掉失效的选择（见 Deck/CardOwnershipPatches.cs）。</para>
    /// </remarks>
    public bool ShareEvents { get; set; }

    /// <summary>共生体存档登记：种子 → 成员 netId（逗号分隔）。</summary>
    public Dictionary<string, string> SymbioticRuns { get; set; } = [];

    // ==================== pvp ====================

    /// <summary>是否开启决斗。</summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>是否生成「对决邀请」事件。</summary>
    public bool EventEnabled { get; set; } = true;

    /// <summary>调试：把事件固定在本局第一个问号房（关掉走正式流程）。</summary>
    public bool EventDebugFirstQuestion { get; set; } = true;

    /// <summary>回合上限：打满还没分胜负就按剩余血量比例判定。</summary>
    public int MaxRounds { get; set; } = 30;

    // ==================== 通用 ====================

    /// <summary>单人角色选择界面是否显示两个 mod 角色。</summary>
    public bool CharactersVisibleInSingleplayer { get; set; } = true;

    // ==================== 决斗的运行时判定（原 WhiteAlbumSetting.Duel）====================

    /// <summary>调试总开关：关掉时所有「调试：」项不生效。</summary>
    public bool Debug { get; set; } = true;

    
    /// <summary>本局是否满足决斗的角色条件：正好两人，且分别选了不同的 mod 角色。</summary>
    public static bool DuelCharacterConditionMet => AlbumCharacterRules.CurrentRunHasRequiredAlbumPair();

    /// <summary>决斗是否真正启用（设置开启 + 角色条件满足）。</summary>
    public static bool DuelEnabled => WhiteAlbumSettingStore.Current.Enabled && DuelCharacterConditionMet;

    /// <summary>是否生成决斗事件（关掉等于入口完全不出现）。</summary>
    public static bool DuelEventEnabled => DuelEnabled && WhiteAlbumSettingStore.Current.EventEnabled;

    /// <summary>事件是否固定在第一个问号房（调试总开关也开着才算）。</summary>
    public static bool DuelEventDebugFirstQuestion =>
        WhiteAlbumSettingStore.Current.Debug && WhiteAlbumSettingStore.Current.EventDebugFirstQuestion;

    /// <summary>决斗回合上限（ 1~999）。</summary>
    public static int DuelMaxRounds => Math.Clamp(WhiteAlbumSettingStore.Current.MaxRounds, 1, 999);
}
