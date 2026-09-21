using MegaCrit.Sts2.Core.Nodes.Combat;
using Godot;

namespace STS2_WhiteAlbum2.Core.Combat;

/// <summary>本 mod 的战斗能量计数器。</summary>
/// <remarks>
/// 必须继承 <see cref="NEnergyCounter" /> 而不是引用它：mod 的 Godot 场景里不能引用游戏内的脚本资源，
/// 导出 PCK 时会报 "Cannot set object script"。节点结构照本体 silent_energy_counter.tscn。
/// </remarks>
public partial class AlbumEnergyCounter : NEnergyCounter
{
    
    public override void _Ready()
    {
        base._Ready();

        // WineFox default burst tint
        var burst = new Color(0.964706f, 0.611765f, 0.768627f, 0.6f);
        if (GetNodeOrNull<CpuParticles2D>("%BurstBack") is { } back)
            back.Color = burst;
        if (GetNodeOrNull<CpuParticles2D>("%BurstFront") is { } front)
            front.Color = burst;
    }
    
}