using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using SharpDX;

namespace Week02Samples
{
	/// <summary>
	/// A call which make repetitive (variable) buffer operation easier on memory
	/// by sharing a common buffer of the smallest size enough for all operation.
	/// It also some handy method to read write class / strut (which have been
	/// appropriately tagged for interop) from and to a Stream.
	/// </summary>
	/// <remarks>It runs some specially optimized code with native stream.</remarks>
	public unsafe class BufferEx
	{
		#region GetBuffer(), CurrentLength

		byte[] buffer;

		/// <summary>
		/// Return a common shared buffer, to ease memory allocation
		/// </summary>
		public byte[] GetBuffer(int len)
		{
			if (buffer == null || buffer.Length < len)
				buffer = new byte[len];
			return buffer;
		}

		public int CurrentLength { get { return buffer != null ? buffer.Length : 0; } }

		#endregion

		#region CopyMemory()

		/// <summary>
		/// Copy a portion of a stream unto another.
		/// Special code is used for unmanaged stream.
		/// </summary>
		public void CopyMemory(Stream dst, Stream src, int len, int buffsize)
		{
			while (len > 0)
			{
				int alen = len > buffsize ? buffsize : len;
				CopyMemory(dst, src, alen);
				len -= alen;
			}
		}

		/// <summary>
		/// Copy a portion of a stream unto another.
		/// Special code is used for unmanaged stream.
		/// </summary>
		public void CopyMemory(Stream dst, Stream src, int len)
		{
			var buf = GetBuffer(len);
			src.Read(buf, 0, len);
			dst.Write(buf, 0, len);
		}

		#endregion

		#region ToBytes<T>(), ToValue()

		public static void ToBytes<T>(T value, byte[] buf, ref int offset)
			where T : struct
		{
			int n = Marshal.SizeOf(typeof(T));
			if (offset < 0 || offset + n > buf.Length)
				throw new ArgumentOutOfRangeException();

			fixed (byte* dst = &buf[offset])
				Marshal.StructureToPtr(value, (IntPtr)dst, false);
			offset += n;
		}

		public static T ToValue<T>(byte[] buf, ref int offset)
			where T : struct
		{
			int n = Marshal.SizeOf(typeof(T));
			if (offset < 0 || offset + n > buf.Length)
				throw new ArgumentException();

			fixed (byte* pbuf = &buf[offset])
			{
				var result = (T)Marshal.PtrToStructure((IntPtr)pbuf, typeof(T));
				offset += n;
				return result;
			}
		}

		#endregion

		#region Write<T>(), Read<T>()

		public void Write<T>(Stream str, params T[] values)
			where T : struct
		{
			if (values == null)
				return;
			Write<T>(str, values, 0, values.Length);
		}

		public void Write<T>(Stream str, T[] values, int offset, int count)
			where T : struct
		{
			if (values == null || count == 0)
				return;
			if (count < 0 || offset < 0 || offset + count > values.Length)
				throw new ArgumentOutOfRangeException();

			int n = Marshal.SizeOf(typeof(T));
			var buf = GetBuffer(n * count);
			int bufoffset = 0;
			for (int i = 0; i < values.Length; i++)
				ToBytes(values[offset + i], buf, ref bufoffset);

			str.Write(buf, 0, bufoffset);
		}

		public T Read<T>(Stream str)
			where T : struct
		{
			int n = Marshal.SizeOf(typeof(T));
			var buf = GetBuffer(n);
			str.Read(buf, 0, n);
			int offset = 0;
			return ToValue<T>(buf, ref offset);
		}

		public void Read<T>(Stream str, T[] values, int offset, int count)
			where T : struct
		{
			if (count < 0 || offset < 0 || offset + count > values.Length)
				throw new ArgumentOutOfRangeException();

			int n = Marshal.SizeOf(typeof(T));
			var buf = GetBuffer(n * count);
			str.Read(buf, 0, n * count);

			int bufoffset = 0;
			for (int i = 0; i < count; i++)
				values[offset + i] = ToValue<T>(buf, ref bufoffset);
		}

		public T[] Read<T>(Stream str, int count)
			where T : struct
		{
			var result = new T[count];
			Read<T>(str, result, 0, count);
			return result;
		}

		#endregion
	}
}
