using Godot;

using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.addons.mega_text;

namespace STS2_WhiteAlbum2.Core.Combat;

/// <summary>计数器的数字标签。</summary>
/// <remarks>
/// <para>本体在 _Ready 里写的是 GetNode&lt;MegaLabel&gt;("Label")，所以这个节点必须是 MegaLabel 的子类。</para>
/// <para>
/// <b>必须自带 theme font override</b>：<c>MegaLabel._Ready</c> 会调
/// <c>MegaLabelHelper.AssertThemeFontOverride</c>，没有 override 时直接抛
/// <c>InvalidOperationException: … has no theme font override</c>（本体为了绕开一个 Godot 引擎 bug 加的硬检查）。
/// 本体自己的计数器场景是用一个 FontVariation 子资源设的，mod 场景里不带本体资源，
/// 所以在这里运行时补一份（字体直接引用本体的共享字体资源）。
/// </para>
/// </remarks>
public partial class AlbumCounterLabel : MegaLabel
{
    /// <summary>本体的共享字体（FontVariation → <c>res://fonts/kreon_bold.ttf</c>）。</summary>
    private const string GameFontPath = "res://themes/kreon_bold_shared.tres";

    public override void _Ready()
    {
        if (!HasThemeFontOverride("font"))
        {
            if (GD.Load<Font>(GameFontPath) is { } font)
            {
                AddThemeFontOverride("font", font);
            }
            else
            {
                Log.Warn($"[{Const.ModId}] 计数器字体加载失败：{GameFontPath}（MegaLabel 会抛异常）");
            }
        }

        base._Ready();
    }
}
