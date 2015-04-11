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
using SharpDX.Direct3D;
using SharpDX.WPF;

namespace Week02Samples
{
	/// <summary>
	/// Interaction logic for SafeViewer.xaml
	/// </summary>
	public partial class SafeViewer : UserControl
	{
		public SafeViewer()
		{
			InitializeComponent();
		}

		#region SceneTemplate

		public DataTemplate SceneTemplate
		{
			get { return (DataTemplate)GetValue(SceneTemplateProperty); }
			set { SetValue(SceneTemplateProperty, value); }
		}

		public static readonly DependencyProperty SceneTemplateProperty =
			DependencyProperty.Register(
				"SceneTemplate",
				typeof(DataTemplate),
				typeof(SafeViewer),
				new PropertyMetadata((d, e) => ((SafeViewer)d).OnSceneTemplateChanged((DataTemplate)e.OldValue, (DataTemplate)e.NewValue)));

		private void OnSceneTemplateChanged(DataTemplate oldValue, DataTemplate newValue)
		{
			Update();
		}

		#endregion SceneTemplate

		#region MinimumHardware

		public FeatureLevel MinimumHardware
		{
			get { return (FeatureLevel)GetValue(MinimumHardwareProperty); }
			set { SetValue(MinimumHardwareProperty, value); }
		}

		public static readonly DependencyProperty MinimumHardwareProperty =
			DependencyProperty.Register(
				"MinimumHardware",
				typeof(FeatureLevel),
				typeof(SafeViewer),
				new PropertyMetadata((d, e) => ((SafeViewer)d).OnMinimumHardwareChanged((FeatureLevel)e.OldValue, (FeatureLevel)e.NewValue)));

		private void OnMinimumHardwareChanged(FeatureLevel oldValue, FeatureLevel newValue)
		{
			Update();
		}

		#endregion MinimumHardware

		void Update()
		{
			var dt = SceneTemplate;
			if (dt == null)
			{
				Scene = null;
				uiContent.Content = null;
				return;
			}

			var min = MinimumHardware;
			using (var dg = new DisposeGroup())
			{
				var ada = DeviceUtil.GetBestAdapter(dg);
				var level = SharpDX.Direct3D11.Device.GetSupportedFeatureLevel(ada);
				if (level < min)
				{
					Scene = null;
				}
				else
				{
					Scene = dt.LoadContent();
				}
			}
		}
		object Scene
		{
			get { return mScene; }
			set
			{
				if (value == mScene)
					return;
				mScene = value;
				uiContent.Content = value;
				uiContent.Visibility = value != null ? Visibility.Visible : Visibility.Collapsed;
				uiNA.Visibility = value == null ? Visibility.Visible : Visibility.Collapsed;
			}
		}
		object mScene;
	}
}
