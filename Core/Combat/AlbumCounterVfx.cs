using System.Reflection;

using Godot;
using Godot.Collections;

using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace STS2_WhiteAlbum2.Core.Combat;

/// <summary>计数器粒子的容器。</summary>
/// <remarks>
/// 场景跨程序集不能序列化基类的私有 <c>_particles</c> 字段（导出会失败），
/// 所以在 _Ready 里从子节点收集 GPUParticles2D 再写进基类字段。
/// </remarks>
public partial class AlbumCounterVfx : NParticlesContainer
{
	private static readonly FieldInfo ParticlesField =
		typeof(NParticlesContainer).GetField("_particles", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("NParticlesContainer._particles not found");

	public override void _Ready()
	{
		if (ParticlesField.GetValue(this) is not Array<GpuParticles2D> { Count: > 0 })
		{
			var list = new Array<GpuParticles2D>();
			foreach (var child in GetChildren())
			{
				if (child is GpuParticles2D gpu)
				{
					list.Add(gpu);
				}
			}

			ParticlesField.SetValue(this, list);
		}

		base._Ready();
	}
}
