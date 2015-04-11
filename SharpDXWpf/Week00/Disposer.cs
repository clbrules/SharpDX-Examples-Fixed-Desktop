using System;
using System.Linq;
using SharpDX;
using System.Collections.Generic;

namespace Week00
{
	public static class Disposer
	{
		public static void SafeDispose<T>(ref T obj)
			where T : class, IDisposable
		{
			if (obj != null)
			{
				obj.Dispose();
				obj = null;
			}
		}
	}
}
