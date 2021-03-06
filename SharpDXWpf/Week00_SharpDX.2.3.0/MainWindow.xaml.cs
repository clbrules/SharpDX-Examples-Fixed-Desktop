﻿using System.Windows;
using SharpDX.WPF;

namespace Week00
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			FPS = new FPS();
			InitializeComponent();

			dxview.Renderer = new Scene() { Renderer = new D3D10(), FPS = FPS, };
			img.Source = dxview.Surface;
			dxview11.Renderer = new Scene_11() { Renderer = new D3D11() };
			dxview2d.Renderer = new Scene3d10_2d1() { Renderer = new D2D1() };

			//dxview11_2d.Renderer = new Scene_11_2D1() { Renderer = new D3D11_2D1() };
		}

		public FPS FPS { get; set; }
	}
}
