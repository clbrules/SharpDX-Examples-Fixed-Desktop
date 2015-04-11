using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Week02Samples
{
	/// <summary>
	/// Manipulate a pointer to a stream. Copy is made through the BufferEx class.
	/// </summary>
	public struct StreamPtr
	{
		Stream stream;
		long offset;

		public StreamPtr(Stream s)
			: this(s, s.Position)
		{
		}
		public StreamPtr(Stream s, long pos)
		{
			if (s == null)
				throw new ArgumentNullException("stream");
			stream = s;
			offset = pos;
		}

		public static StreamPtr operator +(StreamPtr sp, long plus) { return new StreamPtr(sp.stream, sp.offset + plus); }
		public static StreamPtr operator -(StreamPtr sp, long plus) { return new StreamPtr(sp.stream, sp.offset - plus); }

		public static long operator -(StreamPtr sp, StreamPtr sp2) 
		{
			if (sp.stream != sp2.stream)
				throw new ArgumentException();
			return sp.offset - sp2.offset; 
		}

		public static explicit operator Stream(StreamPtr ptr) 
		{
			ptr.stream.Position = ptr.offset;
			return ptr.stream;
		}
	}
}
