using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Week02Samples.ContentStream
{
	/// <summary>
	/// Interaction logic for View.xaml
	/// </summary>
	public partial class View : UserControl
	{
		public View()
		{
			InitializeComponent();
		}

		public Renderer Scene
		{
			get { return (Renderer)FindResource("scene"); }
		}

		public Renderer.LOAD_TYPE[] LoadTypes
		{
			get
			{
				return new[]
				{
					// Lloyd: some multithreading bug, nut no advantage => not worth debugging!
					//Renderer.LOAD_TYPE.MULTITHREAD,
					Renderer.LOAD_TYPE.SINGLETHREAD,
				};
			}
		}

		private void DoRun(object sender, RoutedEventArgs e)
		{
			Scene.AppState = Renderer.APP_STATE.RENDER_SCENE;
		}

		private void DoDeletePackfile(object sender, RoutedEventArgs e)
		{
			var path = System.IO.Path.Combine(
				Renderer.GetPackedFilePath(),
				Renderer.g_strFile
				);
			if (System.IO.File.Exists(path))
				System.IO.File.Delete(path);
		}

		private void DoStartOver(object sender, RoutedEventArgs e)
		{
			var sc = Scene;
			sc.AppState = Renderer.APP_STATE.STARTUP;
			dxview.Surface.SetBackBuffer(sc.RenderTarget);
		}
	}

	public class EnumConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (value is Renderer.LOAD_TYPE)
			{
				var lt = (Renderer.LOAD_TYPE)value;
				switch (lt)
				{
					case Renderer.LOAD_TYPE.MULTITHREAD:
						return "On Demand Multi-Threaded";
					case Renderer.LOAD_TYPE.SINGLETHREAD:
						return "On Demand Single-Threaded";
				}
			}
			if (value is Renderer.APP_STATE)
			{
				var lt = (Renderer.APP_STATE)value;
				switch (lt)
				{
					case Renderer.APP_STATE.RENDER_SCENE:
						return Visibility.Hidden;
					case Renderer.APP_STATE.STARTUP:
						return Visibility.Visible;
				}
			}
			return value;
		}
		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
