using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Week02Samples.ContentStream
{
	public static class WinAPI
	{
		public const int MAX_PATH = 260;

		[DllImport("kernel32.dll")]
		public static extern void GetSystemInfo([MarshalAs(UnmanagedType.Struct)] out SYSTEM_INFO lpSystemInfo);

		[StructLayout(LayoutKind.Sequential)]
		public struct SYSTEM_INFO
		{
			internal _PROCESSOR_INFO_UNION uProcessorInfo;
			public uint dwPageSize;
			public IntPtr lpMinimumApplicationAddress;
			public IntPtr lpMaximumApplicationAddress;
			public IntPtr dwActiveProcessorMask;
			public uint dwNumberOfProcessors;
			public uint dwProcessorType;
			public uint dwAllocationGranularity;
			public ushort dwProcessorLevel;
			public ushort dwProcessorRevision;
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct _PROCESSOR_INFO_UNION
		{
			[FieldOffset(0)]
			internal uint dwOemId;
			[FieldOffset(0)]
			internal ushort wProcessorArchitecture;
			[FieldOffset(2)]
			internal ushort wReserved;
		}
	}
}
