using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace Week02Samples
{
	public static class StreamEx
	{
		public unsafe static T Read<T>(this Stream s)
			where T : struct
		{
			int n = Marshal.SizeOf(typeof(T));
			var buf = new byte[n];
			s.Read(buf, 0, n);
			fixed (byte* pbuf = &buf[0])
				return (T)Marshal.PtrToStructure((IntPtr)pbuf, typeof(T));
		}
	}
}
