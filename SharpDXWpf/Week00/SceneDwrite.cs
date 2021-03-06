﻿using System.Drawing;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.WPF;

namespace Week00
{
    public class SceneDwrite : Week00.SceneBase<D2D1>
	{
		TextFormat TextFormat;
		SolidColorBrush SceneColorBrush;

		protected override void Attach()
		{
			if (Renderer == null)
				return;

			SceneColorBrush = new SolidColorBrush(Renderer.RenderTarget2D, new Color4(1, 1, 0, 1));

			// Initialize a TextFormat
			TextFormat = new TextFormat(Renderer.FactoryDW, "Calibri", 32) 
			{
				TextAlignment = TextAlignment.Center, 
				ParagraphAlignment = ParagraphAlignment.Center 
			};

			Renderer.RenderTarget2D.TextAntialiasMode = TextAntialiasMode.Cleartype;
		}

		protected override void Detach()
		{
			Disposer.SafeDispose(ref TextFormat);
			Disposer.SafeDispose(ref SceneColorBrush);
		}

		public override void RenderScene(DrawEventArgs args)
		{
			Renderer.RenderTarget2D.DrawText("Hello Direct 2D", TextFormat, new SharpDX.RectangleF(0, 0, (float)args.RenderSize.Width, (float)args.RenderSize.Height), SceneColorBrush);
		}
	}
}
