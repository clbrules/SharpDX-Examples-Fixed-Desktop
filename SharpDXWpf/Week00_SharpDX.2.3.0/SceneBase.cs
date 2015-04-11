using SharpDX.WPF;

namespace Week00
{
	public abstract class SceneBase<T> : IDirect3D
		where T : D3D
	{
		public virtual T Renderer 
		{
			get { return context; }
			set
			{
				if (Renderer != null)
				{
					Renderer.Rendering -= ContextRendering;
					Detach();
				}
				context = value;
				if (Renderer != null)
				{
					Renderer.Rendering += ContextRendering;
					Attach();
				}
			}
		}
		T context;

		void ContextRendering(object aCtx, DrawEventArgs args) { RenderScene(args); }

		protected virtual void Attach()
		{

		}
		protected virtual void Detach()
		{

		}

		public abstract void RenderScene(DrawEventArgs args);

		void IDirect3D.Reset(DrawEventArgs args)
		{
			if (Renderer != null)
				Renderer.Reset(args);
		}

		void IDirect3D.Render(DrawEventArgs args)
		{
			if (Renderer != null)
				Renderer.Render(args);
		}
	}
}
