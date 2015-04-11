using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using D3DFORMAT = SharpDX.Direct3D9.Format;
using DXGI_FORMAT = SharpDX.DXGI.Format;

namespace Week02Samples.ContentStream
{
	public static class FormatEx
	{
		public static DXGI_FORMAT ToDXGI(this D3DFORMAT d3dformat)
		{
			switch (d3dformat)
			{
				case D3DFORMAT.A32B32G32R32F:
					return DXGI_FORMAT.R32G32B32A32_Float;

				case D3DFORMAT.A16B16G16R16:
					return DXGI_FORMAT.R16G16B16A16_UNorm;
				case D3DFORMAT.A16B16G16R16F:
					return DXGI_FORMAT.R16G16B16A16_Float;
				case D3DFORMAT.G32R32F:
					return DXGI_FORMAT.R32G32_Float;

				case D3DFORMAT.R8G8B8:
				case D3DFORMAT.A8R8G8B8:
				case D3DFORMAT.X8R8G8B8:
					return DXGI_FORMAT.R8G8B8A8_UNorm;

				case D3DFORMAT.G16R16:
				case D3DFORMAT.V16U16:
					return DXGI_FORMAT.R16G16_UNorm;

				case D3DFORMAT.G16R16F:
					return DXGI_FORMAT.R16G16_Float;
				case D3DFORMAT.R32F:
					return DXGI_FORMAT.R32_Float;

				case D3DFORMAT.R16F:
					return DXGI_FORMAT.R16_Float;

				case D3DFORMAT.A8:
					return DXGI_FORMAT.A8_UNorm;
				case D3DFORMAT.P8:
				case D3DFORMAT.L8:
					return DXGI_FORMAT.R8_UNorm;

				case D3DFORMAT.Dxt1:
					return DXGI_FORMAT.BC1_UNorm;
				case D3DFORMAT.Dxt2:
					return DXGI_FORMAT.BC1_UNorm_SRgb;
				case D3DFORMAT.Dxt3:
					return DXGI_FORMAT.BC2_UNorm;
				case D3DFORMAT.Dxt4:
					return DXGI_FORMAT.BC2_UNorm_SRgb;
				case D3DFORMAT.Dxt5:
					return DXGI_FORMAT.BC3_UNorm;

				default:
					return DXGI_FORMAT.Unknown;
			}
		}
		public static D3DFORMAT ToD3D9(this DXGI_FORMAT dxgiformat)
		{
			switch( dxgiformat )
			{

				case DXGI_FORMAT.R32G32B32A32_Float:
					return D3DFORMAT.A32B32G32R32F;

				case DXGI_FORMAT.R16G16B16A16_UNorm:
					return D3DFORMAT.A16B16G16R16;
				case DXGI_FORMAT.R16G16B16A16_Float:
					return D3DFORMAT.A16B16G16R16F;
				case DXGI_FORMAT.R32G32_Float:
					return D3DFORMAT.G32R32F;

				case DXGI_FORMAT.R8G8B8A8_UNorm:
					return D3DFORMAT.A8R8G8B8;

				case DXGI_FORMAT.R16G16_UNorm:
					return D3DFORMAT.G16R16;

				case DXGI_FORMAT.R16G16_Float:
					return D3DFORMAT.G16R16F;
				case DXGI_FORMAT.R32_Float:
					return D3DFORMAT.R32F;

				case DXGI_FORMAT.R16_Float:
					return D3DFORMAT.R16F;

				case DXGI_FORMAT.A8_UNorm:
					return D3DFORMAT.A8;
				case DXGI_FORMAT.R8_UNorm:
					return D3DFORMAT.L8;

				case DXGI_FORMAT.BC1_UNorm:
					return D3DFORMAT.Dxt1;
				case DXGI_FORMAT.BC2_UNorm:
					return D3DFORMAT.Dxt3;
				case DXGI_FORMAT.BC3_UNorm:
					return D3DFORMAT.Dxt5;

				default:
					return D3DFORMAT.Unknown;
			}
		}


		//--------------------------------------------------------------------------------------
		// Return the BPP for a particular format
		//--------------------------------------------------------------------------------------
		public static int BitsPerPixel(D3DFORMAT fmt)
		{
			switch (fmt)
			{
				case D3DFORMAT.A32B32G32R32F:
					return 128;

				case D3DFORMAT.A16B16G16R16:
				case D3DFORMAT.Q16W16V16U16:
				case D3DFORMAT.A16B16G16R16F:
				case D3DFORMAT.G32R32F:
					return 64;

				case D3DFORMAT.A8R8G8B8:
				case D3DFORMAT.X8R8G8B8:
				case D3DFORMAT.A2B10G10R10:
				case D3DFORMAT.A8B8G8R8:
				case D3DFORMAT.X8B8G8R8:
				case D3DFORMAT.G16R16:
				case D3DFORMAT.A2R10G10B10:
				case D3DFORMAT.Q8W8V8U8:
				case D3DFORMAT.V16U16:
				case D3DFORMAT.X8L8V8U8:
				case D3DFORMAT.A2W10V10U10:
				case D3DFORMAT.D32:
				case D3DFORMAT.D24S8:
				case D3DFORMAT.D24X8:
				case D3DFORMAT.D24X4S4:
				case D3DFORMAT.D32SingleLockable:
				case D3DFORMAT.D24SingleS8:
				case D3DFORMAT.Index32:
				case D3DFORMAT.G16R16F:
				case D3DFORMAT.R32F:
					return 32;

				case D3DFORMAT.R8G8B8:
					return 24;

				case D3DFORMAT.A4R4G4B4:
				case D3DFORMAT.X4R4G4B4:
				case D3DFORMAT.R5G6B5:
				case D3DFORMAT.L16:
				case D3DFORMAT.A8L8:
				case D3DFORMAT.X1R5G5B5:
				case D3DFORMAT.A1R5G5B5:
				case D3DFORMAT.A8R3G3B2:
				case D3DFORMAT.V8U8:				
                case D3DFORMAT.MtCxV8U8:
				case D3DFORMAT.L6V5U5:				
                case D3DFORMAT.G8R8_G8B8:
				case D3DFORMAT.R8G8_B8G8:
				case D3DFORMAT.D16Lockable:
				case D3DFORMAT.D15S1:
				case D3DFORMAT.D16:
				case D3DFORMAT.Index16:
				case D3DFORMAT.R16F:
				case D3DFORMAT.Yuy2:
					return 16;

				case D3DFORMAT.R3G3B2:
				case D3DFORMAT.A8:
				case D3DFORMAT.A8P8:
				case D3DFORMAT.P8:
				case D3DFORMAT.L8:
				case D3DFORMAT.A4L4:
					return 8;

				case D3DFORMAT.Dxt1:
					return 4;
				case D3DFORMAT.Dxt2:
				case D3DFORMAT.Dxt3:
				case D3DFORMAT.Dxt4:
				case D3DFORMAT.Dxt5:
					return 8;

				default:
					throw new NotSupportedException();
			}
		}

		//--------------------------------------------------------------------------------------
		// Get surface information for a particular format
		//--------------------------------------------------------------------------------------
		public static int GetSurfaceInfo(int width, int height, D3DFORMAT fmt)
		{
			int pRowBytes, pNumRows, pNumBytes;
			GetSurfaceInfo(width, height, fmt, out pNumBytes, out pRowBytes, out pNumRows);
			return pNumBytes;
		}

		public static void GetSurfaceInfo(int width, int height, D3DFORMAT fmt, out int pNumBytes, out int pRowBytes, out int pNumRows)
		{
			int numBytes = 0;
			int rowBytes = 0;
			int numRows = 0;

			// From the DXSDK docs:
			//
			//     When computing DXTn compressed sizes for non-square textures, the 
			//     following formula should be used at each mipmap level:
			//
			//         max(1, width ÷ 4) x max(1, height ÷ 4) x 8(DXT1) or 16(DXT2-5)
			//
			//     The pitch for DXTn formats is different from what was returned in 
			//     Microsoft DirectX 7.0. It now refers the pitch of a row of blocks. 
			//     For example, if you have a width of 16, then you will have a pitch 
			//     of four blocks (4*8 for DXT1, 4*16 for DXT2-5.)"

			if (fmt == D3DFORMAT.Dxt1 || fmt == D3DFORMAT.Dxt2 || fmt == D3DFORMAT.Dxt3 || fmt == D3DFORMAT.Dxt4 || fmt == D3DFORMAT.Dxt5)
			{
				// Note: we support width and/or height being 0 in order to compute
				// offsets in functions like CBufferLockEntry::CopyBLEToPerfectSizedBuffer().
				int numBlocksWide = 0;
				if (width > 0)
					numBlocksWide = Math.Max(1, width / 4);
				int numBlocksHigh = 0;
				if (height > 0)
					numBlocksHigh = Math.Max(1, height / 4);
				//int numBlocks = numBlocksWide * numBlocksHigh;
				int numBytesPerBlock = (fmt == D3DFORMAT.Dxt1 ? 8 : 16);
				rowBytes = numBlocksWide * numBytesPerBlock;
				numRows = numBlocksHigh;
			}
			else
			{
				int bpp = BitsPerPixel(fmt);
				rowBytes = (width * bpp + 7) / 8; // round up to nearest byte
				numRows = height;
			}
			numBytes = rowBytes * numRows;

			// return the results
			pNumBytes = numBytes;
			pRowBytes = rowBytes;
			pNumRows = numRows;
		}

		//--------------------------------------------------------------------------------------
		static bool ISBITMASK(DDS.PIXELFORMAT ddpf, uint r, uint g, uint b, uint a) { return ddpf.dwRBitMask == r && ddpf.dwGBitMask == g && ddpf.dwBBitMask == b && ddpf.dwABitMask == a; }

		public static D3DFORMAT GetD3D9Format(this DDS.PIXELFORMAT ddpf)
		{
			// See DDSTextureLoader for a more complete example of this...

			if ((ddpf.dwFlags & DDS.RGB) != 0)	//rgb codes
			// Only do the more common formats
			{
				if (32 == ddpf.dwRGBBitCount)
				{
					if (ISBITMASK(ddpf, 0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000))
						return D3DFORMAT.A8R8G8B8;
					if (ISBITMASK(ddpf, 0x00ff0000, 0x0000ff00, 0x000000ff, 0x00000000))
						return D3DFORMAT.X8R8G8B8;
					if (ISBITMASK(ddpf, 0x000000ff, 0x00ff0000, 0x0000ff00, 0xff000000))
						return D3DFORMAT.A8B8G8R8;
					if (ISBITMASK(ddpf, 0x000000ff, 0x00ff0000, 0x0000ff00, 0x00000000))
						return D3DFORMAT.X8B8G8R8;
					if (ISBITMASK(ddpf, 0xffffffff, 0x00000000, 0x00000000, 0x00000000))
						return D3DFORMAT.R32F;
				}

				if (24 == ddpf.dwRGBBitCount)
				{
					if (ISBITMASK(ddpf, 0x00ff0000, 0x0000ff00, 0x000000ff, 0x00000000))
						return D3DFORMAT.R8G8B8;
				}

				if (16 == ddpf.dwRGBBitCount)
				{
					if (ISBITMASK(ddpf, 0x0000F800, 0x000007E0, 0x0000001F, 0x00000000))
						return D3DFORMAT.R5G6B5;
				}
			}
			else if ((ddpf.dwFlags & DDS.LUMINANCE) != 0)
			{
				if (8 == ddpf.dwRGBBitCount)
				{
					return D3DFORMAT.L8;
				}
			}
			else if ((ddpf.dwFlags & DDS.ALPHA) != 0)
			{
				if (8 == ddpf.dwRGBBitCount)
				{
					return D3DFORMAT.A8;
				}
			}
			else if ((ddpf.dwFlags & DDS.FOURCC) != 0) //fourcc codes (dxtn)
			{
				if (DDS.MAKEFOURCC('D', 'X', 'T', '1') == ddpf.dwFourCC)
					return D3DFORMAT.Dxt1;
				if (DDS.MAKEFOURCC('D', 'X', 'T', '2') == ddpf.dwFourCC)
					return D3DFORMAT.Dxt2;
				if (DDS.MAKEFOURCC('D', 'X', 'T', '3') == ddpf.dwFourCC)
					return D3DFORMAT.Dxt3;
				if (DDS.MAKEFOURCC('D', 'X', 'T', '4') == ddpf.dwFourCC)
					return D3DFORMAT.Dxt4;
				if (DDS.MAKEFOURCC('D', 'X', 'T', '5') == ddpf.dwFourCC)
					return D3DFORMAT.Dxt5;
			}

			return D3DFORMAT.Unknown;
		}

		public static long GetEstimatedSize(int Width, int Height, int MipLevels, int Format)
		{
			long SizeTotal = 0;
			int Size = 0;

			while (Width > 0 && Height > 0 && MipLevels > 0)
			{
				Size = FormatEx.GetSurfaceInfo(Width, Height, (D3DFORMAT)Format);

				SizeTotal += Size;
				Width = Width >> 1;
				Height = Height >> 1;
				MipLevels--;
			}

			return SizeTotal;
		}
	}
}
