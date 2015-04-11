using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpDX;
using System.Runtime.InteropServices;

namespace Week02Samples.SimpleBezier
{
	public static class MobiusStrip
	{
		public readonly static Vector3[] Points = new Vector3[64] {
			new Vector3(1.0f, -0.5f, 0.0f ),
			new Vector3(1.0f, -0.5f, 0.5f ),
			new Vector3(0.5f, -0.3536f, 1.354f ),
			new Vector3(0.0f, -0.3536f, 1.354f ),
			new Vector3(1.0f, -0.1667f, 0.0f ),
			new Vector3(1.0f, -0.1667f, 0.5f ),
			new Vector3(0.5f, -0.1179f, 1.118f ),
			new Vector3(0.0f, -0.1179f, 1.118f ),
			new Vector3(1.0f, 0.1667f, 0.0f ),
			new Vector3(1.0f, 0.1667f, 0.5f ),
			new Vector3(0.5f, 0.1179f, 0.8821f ),
			new Vector3(0.0f, 0.1179f, 0.8821f ),
			new Vector3(1.0f, 0.5f, 0.0f ),
			new Vector3(1.0f, 0.5f, 0.5f ),
			new Vector3(0.5f, 0.3536f, 0.6464f ),
			new Vector3(0.0f, 0.3536f, 0.6464f ),
			new Vector3(0.0f, -0.3536f, 1.354f ),
			new Vector3(-0.5f, -0.3536f, 1.354f ),
			new Vector3(-1.5f, 0.0f, 0.5f ),
			new Vector3(-1.5f, 0.0f, 0.0f ),
			new Vector3(0.0f, -0.1179f, 1.118f ),
			new Vector3(-0.5f, -0.1179f, 1.118f ),
			new Vector3(-1.167f, 0.0f, 0.5f ),
			new Vector3(-1.167f, 0.0f, 0.0f ),
			new Vector3(0.0f, 0.1179f, 0.8821f ),
			new Vector3(-0.5f, 0.1179f, 0.8821f ),
			new Vector3(-0.8333f, 0.0f, 0.5f ),
			new Vector3(-0.8333f, 0.0f, 0.0f ),
			new Vector3(0.0f, 0.3536f, 0.6464f ),
			new Vector3(-0.5f, 0.3536f, 0.6464f ),
			new Vector3(-0.5f, 0.0f, 0.5f ),
			new Vector3(-0.5f, 0.0f, 0.0f ),
			new Vector3(-1.5f, 0.0f, 0.0f ),
			new Vector3(-1.5f, 0.0f, -0.5f ),
			new Vector3(-0.5f, 0.3536f, -1.354f ),
			new Vector3(0.0f, 0.3536f, -1.354f ),
			new Vector3(-1.167f, 0.0f, 0.0f ),
			new Vector3(-1.167f, 0.0f, -0.5f ),
			new Vector3(-0.5f, 0.1179f, -1.118f ),
			new Vector3(0.0f, 0.1179f, -1.118f ),
			new Vector3(-0.8333f, 0.0f, 0.0f ),
			new Vector3(-0.8333f, 0.0f, -0.5f ),
			new Vector3(-0.5f, -0.1179f, -0.8821f ),
			new Vector3(0.0f, -0.1179f, -0.8821f ),
			new Vector3(-0.5f, 0.0f, 0.0f ),
			new Vector3(-0.5f, 0.0f, -0.5f ),
			new Vector3(-0.5f, -0.3536f, -0.6464f ),
			new Vector3(0.0f, -0.3536f, -0.6464f ),
			new Vector3(0.0f, 0.3536f, -1.354f ),
			new Vector3(0.5f, 0.3536f, -1.354f ),
			new Vector3(1.0f, 0.5f, -0.5f ),
			new Vector3(1.0f, 0.5f, 0.0f ),
			new Vector3(0.0f, 0.1179f, -1.118f ),
			new Vector3(0.5f, 0.1179f, -1.118f ),
			new Vector3(1.0f, 0.1667f, -0.5f ),
			new Vector3(1.0f, 0.1667f, 0.0f ),
			new Vector3(0.0f, -0.1179f, -0.8821f ),
			new Vector3(0.5f, -0.1179f, -0.8821f ),
			new Vector3(1.0f, -0.1667f, -0.5f ),
			new Vector3(1.0f, -0.1667f, 0.0f ),
			new Vector3(0.0f, -0.3536f, -0.6464f ),
			new Vector3(0.5f, -0.3536f, -0.6464f ),
			new Vector3(1.0f, -0.5f, -0.5f ),
			new Vector3(1.0f, -0.5f, 0.0f ),
		};
	}
}
