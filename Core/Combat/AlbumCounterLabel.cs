using MegaCrit.Sts2.addons.mega_text;

namespace STS2_WhiteAlbum2.Core.Combat;

/// <summary>计数器的数字标签。</summary>
/// <remarks>本体在 _Ready 里写的是 GetNode&lt;MegaLabel&gt;("Label")，所以这个节点必须是 MegaLabel 的子类。</remarks>
public partial class AlbumCounterLabel : MegaLabel
{
}
