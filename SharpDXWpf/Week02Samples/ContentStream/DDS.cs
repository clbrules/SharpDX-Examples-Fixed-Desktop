using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Week02Samples.ContentStream
{
	//--------------------------------------------------------------------------------------
	// dds.h
	//
	// This header defines constants and structures that are useful when parsing 
	// DDS files.  DDS files were originally designed to use several structures
	// and constants that are native to DirectDraw and are defined in ddraw.h,
	// such as DDSURFACEDESC2 and DDSCAPS2.  This file defines similar 
	// (compatible) constants and structures so that one can use DDS files 
	// without needing to include ddraw.h.
	//--------------------------------------------------------------------------------------
	public class DDS
	{
		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		public struct PIXELFORMAT
		{
			public int dwSize;
			public int dwFlags;
			public int dwFourCC;
			public int dwRGBBitCount;
			public int dwRBitMask;
			public int dwGBitMask;
			public int dwBBitMask;
			public int dwABitMask;

			public const int SizeInBytes = 8 * 4;

			public PIXELFORMAT(
				int dwFlags,
				int dwFourCC,
				int dwRGBBitCount,
				int dwRBitMask,
				int dwGBitMask,
				int dwBBitMask,
				int dwABitMask
			)
			{
				dwSize = SizeInBytes;
				this.dwFlags = dwFlags;
				this.dwFourCC = dwFourCC;
				this.dwRGBBitCount = dwRGBBitCount;
				this.dwRBitMask = dwRBitMask;
				this.dwGBitMask = dwGBitMask;
				this.dwBBitMask = dwBBitMask;
				this.dwABitMask = dwABitMask;
			}
		}

		public const int FOURCC      = 0x00000004;  // DDPF_FOURCC
		public const int RGB         = 0x00000040;  // DDPF_RGB
		public const int RGBA        = 0x00000041;  // DDPF_RGB | DDPF_ALPHAPIXELS
		public const int LUMINANCE   = 0x00020000;  // DDPF_LUMINANCE
		public const int ALPHA       = 0x00000002;  // DDPF_ALPHA

		public static int MAKEFOURCC(char ch0, char ch1, char ch2, char ch3)
		{
			return
				((int)(ushort)(ch0) | ((int)(ushort)(ch1) << 8) |
				((int)(ushort)(ch2) << 16) | ((int)(ushort)(ch3) << 24));
		}

		public readonly PIXELFORMAT DDSPF_DXT1 = new PIXELFORMAT( FOURCC, MAKEFOURCC('D', 'X', 'T', '1'), 0, 0, 0, 0, 0 );
		public readonly PIXELFORMAT DDSPF_DXT2 = new PIXELFORMAT( FOURCC, MAKEFOURCC('D', 'X', 'T', '2'), 0, 0, 0, 0, 0 );
		public readonly PIXELFORMAT DDSPF_DXT3 = new PIXELFORMAT( FOURCC, MAKEFOURCC('D', 'X', 'T', '3'), 0, 0, 0, 0, 0 );
		public readonly PIXELFORMAT DDSPF_DXT4 = new PIXELFORMAT( FOURCC, MAKEFOURCC('D', 'X', 'T', '4'), 0, 0, 0, 0, 0 );
		public readonly PIXELFORMAT DDSPF_DXT5 = new PIXELFORMAT( FOURCC, MAKEFOURCC('D', 'X', 'T', '5'), 0, 0, 0, 0, 0 );
		public readonly PIXELFORMAT DDSPF_A8R8G8B8 = new PIXELFORMAT( RGBA, 0, 32, 0x00ff0000, 0x0000ff00, 0x000000ff, unchecked((int)0xff000000) );
		public readonly PIXELFORMAT DDSPF_A1R5G5B5 = new PIXELFORMAT( RGBA, 0, 16, 0x00007c00, 0x000003e0, 0x0000001f, 0x00008000 );
		public readonly PIXELFORMAT DDSPF_A4R4G4B4 = new PIXELFORMAT( RGBA, 0, 16, 0x00000f00, 0x000000f0, 0x0000000f, 0x0000f000 );
		public readonly PIXELFORMAT DDSPF_R8G8B8 = new PIXELFORMAT( RGB, 0, 24, 0x00ff0000, 0x0000ff00, 0x000000ff, 0x00000000 );
		public readonly PIXELFORMAT DDSPF_R5G6B5 = new PIXELFORMAT( RGB, 0, 16, 0x0000f800, 0x000007e0, 0x0000001f, 0x00000000 );
		// This indicates the DDS_HEADER_DXT10 extension is present (the format is in dxgiFormat)
		public readonly PIXELFORMAT DDSPF_DX10 = new PIXELFORMAT( FOURCC, MAKEFOURCC('D', 'X', '1', '0'), 0, 0, 0, 0, 0 );

		public const int  HEADER_FLAGS_TEXTURE        =0x00001007;  // DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT 
		public const int  HEADER_FLAGS_MIPMAP         =0x00020000;  // DDSD_MIPMAPCOUNT
		public const int  HEADER_FLAGS_VOLUME         =0x00800000;  // DDSD_DEPTH
		public const int  HEADER_FLAGS_PITCH          =0x00000008;  // DDSD_PITCH
		public const int  HEADER_FLAGS_LINEARSIZE     =0x00080000;  // DDSD_LINEARSIZE

		public const int  SURFACE_FLAGS_TEXTURE =0x00001000; // DDSCAPS_TEXTURE
		public const int  SURFACE_FLAGS_MIPMAP  =0x00400008; // DDSCAPS_COMPLEX | DDSCAPS_MIPMAP
		public const int  SURFACE_FLAGS_CUBEMAP =0x00000008; // DDSCAPS_COMPLEX

		public const int  CUBEMAP_POSITIVEX =0x00000600; // DDSCAPS2_CUBEMAP | DDSCAPS2_CUBEMAP_POSITIVEX
		public const int  CUBEMAP_NEGATIVEX =0x00000a00; // DDSCAPS2_CUBEMAP | DDSCAPS2_CUBEMAP_NEGATIVEX
		public const int  CUBEMAP_POSITIVEY =0x00001200; // DDSCAPS2_CUBEMAP | DDSCAPS2_CUBEMAP_POSITIVEY
		public const int  CUBEMAP_NEGATIVEY =0x00002200; // DDSCAPS2_CUBEMAP | DDSCAPS2_CUBEMAP_NEGATIVEY
		public const int  CUBEMAP_POSITIVEZ =0x00004200; // DDSCAPS2_CUBEMAP | DDSCAPS2_CUBEMAP_POSITIVEZ
		public const int  CUBEMAP_NEGATIVEZ =0x00008200; // DDSCAPS2_CUBEMAP | DDSCAPS2_CUBEMAP_NEGATIVEZ

		public const int  CUBEMAP_ALLFACES =( CUBEMAP_POSITIVEX | CUBEMAP_NEGATIVEX |
									   CUBEMAP_POSITIVEY | CUBEMAP_NEGATIVEY |
									   CUBEMAP_POSITIVEZ | CUBEMAP_NEGATIVEZ );

		public const int  FLAGS_VOLUME =0x00200000; // DDSCAPS2_VOLUME

	
		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		public struct HEADER
		{
			public int dwSize;
			public int dwHeaderFlags;
			public int dwHeight;
			public int dwWidth;
			public int dwPitchOrLinearSize;
			public int dwDepth; // only if DDS_HEADER_FLAGS_VOLUME is set in dwHeaderFlags
			public int dwMipMapCount;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
			public int[] dwReserved1;
			public PIXELFORMAT ddspf;
			public int dwSurfaceFlags;
			public int dwCubemapFlags;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
			public int[] dwReserved2;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		public struct HEADER_DXT10
		{
			public SharpDX.DXGI.Format dxgiFormat;
			public SharpDX.Direct3D11.ResourceDimension resourceDimension; // same a DX10 !
			public int miscFlag;
			public int arraySize;
			public int reserved;
		}
	}
}
