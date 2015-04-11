using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpDX.WPF;
using Week00;
using System.IO;
using System.Windows.Media.Imaging;
using SharpDX.Direct3D11;
using System.Windows.Media;
using System.Windows;
using SharpDX;
using SharpDX.DXGI;

namespace Week00ToFile
{
	class Program
	{
		static SharpDX.Direct3D11.Device CreateDevice()
		{
			SharpDX.Direct3D11.Device dev = null;
			dev = DeviceUtil.Create11(SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport);

			// using WARP (no graphic card needed!)
			// about WARP: http://msdn.microsoft.com/en-us/library/gg615082(v=vs.85).aspx
			if (dev == null)
				dev = new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Warp, SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport);

			return dev;
		}

		unsafe static void Main(string[] args)
		{
			using (var dev = CreateDevice())
			using (var d3d = new D3D11(dev))
			{
				// do not share, not necessary and will crash WARP drivers
				d3d.RenderTargetOptionFlags = ResourceOptionFlags.None;
				d3d.Reset(256, 256);

				var sc = new Scene_11() { Renderer = d3d };
				d3d.Render(new DrawEventArgs() { RenderSize = new System.Windows.Size(256, 256) });

				Save(d3d, "scene11.png");
			}

			using (var d3d = new D2D1())
			{
				d3d.Reset(512, 512);

				var sc = new SceneDwrite() { Renderer = d3d };
				d3d.Render(new DrawEventArgs() { RenderSize = new System.Windows.Size(512, 512) });

				Save(d3d, "scene2D.png");
			}
		}

		unsafe static void Save(D3D d3d, string file)
		{
			var wb = d3d.ToImage();
			using (var stream = new FileStream(file, FileMode.Create))
			{
				var encoder = new PngBitmapEncoder();
				encoder.Frames.Add(BitmapFrame.Create(wb));
				encoder.Save(stream);
			}
		}
	}
}
