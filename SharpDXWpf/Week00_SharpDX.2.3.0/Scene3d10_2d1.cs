using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpDX.WPF;

namespace Week00
{
	public class Scene3d10_2d1 : SceneBase<D2D1>
	{
		Scene sc3d = new Scene();
		SceneDwrite sc2d = new SceneDwrite();

		public override D2D1 Renderer
		{
			set
			{
				base.Renderer = value;
				sc3d.Renderer = value;
				sc2d.Renderer = value;
			}
		}

		public override void RenderScene(DrawEventArgs args) { /* implemented by individual scenes */ }
	}
}
