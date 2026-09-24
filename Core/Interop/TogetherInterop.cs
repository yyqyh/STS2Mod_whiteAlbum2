using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

using STS2RitsuLib.Interop;

namespace STS2_WhiteAlbum2.Core.Interop;

/// <summary>
/// together 的对外接口存根（RitsuLib 的 ModInterop：运行时按 mod id 转发，不产生编译期引用）。
/// </summary>
/// <remarks>
/// <para>
/// 只声明本 mod 真正用到的成员，签名与 <c>Together.Core.Api.TogetherApi</c> 逐字一致。
/// 解析失败时存根返回默认值、不抛异常，所以判断"要不要为共享让路"一律先看
/// <see cref="IsActive" /> / <see cref="IsBound" />。
/// </para>
/// <para>
/// 依赖在 <c>STS2_WhiteAlbum2.json</c> 里声明（<c>id = together</c>，<c>min_version = 0.3.0</c>），
/// 清单依赖会被拓扑排序，所以本 mod 初始化时 together 已经就绪。
/// </para>
/// </remarks>
[ModInterop("together", "Together.Core.Api.TogetherApi")]
internal static class TogetherInterop
{
    /// <summary>总闸门：本局真的在共享（联机 + 已配对）。</summary>
    public static bool IsActive => default;

    /// <summary>是否已配对（<b>不看</b>是否联机）。判战场行为用 <see cref="IsActive" />。</summary>
    public static bool IsBound => default;

    /// <summary>共生体开关的生效值（联机时客户端跟随主机）。</summary>
    public static bool SymbiosisEnabled => default;

    /// <summary>
    /// 注册"谁该成组"的规则：返回非空名单即生效，返回 <c>null</c> 表示这条不管这一局。
    /// </summary>
    /// <remarks>只在 mod 初始化时调一次；规则必须只读、且两端算出同一个答案。</remarks>
    public static void RegisterPairRule(string name, Func<IRunState, IReadOnlyList<Player>?> select) { }

    /// <summary>本局解除绑定（进决斗时用）：断开共享、按奇偶拆卡组，本局不再自动配对。</summary>
    public static bool Unbind(string reason = "external") => default;
}
