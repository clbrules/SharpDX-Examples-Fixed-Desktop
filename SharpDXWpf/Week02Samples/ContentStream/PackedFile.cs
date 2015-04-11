using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpDX;
using System.Runtime.InteropServices;
using System.IO.MemoryMappedFiles;
using System.IO;
using SharpDX.Direct3D10;

namespace Week02Samples.ContentStream
{
	public class LEVEL_ITEM
	{
		public Vector3 vCenter;
		public readonly DEVICE_VERTEX_BUFFER VB = new DEVICE_VERTEX_BUFFER();
		public readonly DEVICE_INDEX_BUFFER IB = new DEVICE_INDEX_BUFFER();
		public readonly DEVICE_TEXTURE Diffuse = new DEVICE_TEXTURE();
		public readonly DEVICE_TEXTURE Normal = new DEVICE_TEXTURE();
		public string szVBName;
		public string szIBName;
		public string szDiffuseName;
		public string szNormalName;
		public bool bLoaded;
		public bool bLoading;
		public bool bInLoadRadius;
		public bool bInFrustum;
		public int CurrentCountdownDiff;
		public int CurrentCountdownNorm;
		public bool bHasBeenRenderedDiffuse;
		public bool bHasBeenRenderedNormal;
	}

	public class PackedFile : IDisposable
	{
		#region internal structures

		// Lloyd: no 'Pack = 4/8' it will be read / write by this program and
		// can't read my current data written by the C++ program...
		[StructLayout(LayoutKind.Sequential)]
		public struct PACKED_FILE_HEADER
		{
			public long FileSize;
			public long NumFiles;
			public long NumChunks;
			public long Granularity;
			public int MaxChunksInVA;

			public long TileBytesSize;
			public float TileSideSize;
			public float LoadingRadius;
			public long VideoMemoryUsageAtFullMips;
		}

		// Lloyd: no 'Pack = 4/8' it will be read / write by this program and
		// can't read my current data written by the C++ program...
		[StructLayout(LayoutKind.Sequential)]
		public struct CHUNK_HEADER
		{
			public long ChunkOffset;
			public long ChunkSize;
		}

		// Lloyd: no 'Pack = 4/8' it will be read / write by this program and
		// can't read my current data written by the C++ program...
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode/*, Pack = 4*/)]
		public struct FILE_INDEX
		{
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = WinAPI.MAX_PATH)]
			public string szFileName;
			public long FileSize;
			public long ChunkIndex;
			public long OffsetIntoChunk;
			public Vector3 vCenter;
		}

		public class MAPPED_CHUNK
		{
			//public MemoryMappedViewStream pMappingPointer;
			//public MemoryMappedViewAccessor pMappingPointer;
			public int UseCounter;
			public bool bInUse;
		}

		#endregion

		PACKED_FILE_HEADER m_FileHeader;
		FILE_INDEX[] m_pFileIndices;
		CHUNK_HEADER[] m_pChunks;
		MAPPED_CHUNK[] m_pMappedChunks;

		FileStream m_hFile;
		MemoryMappedFile m_hFileMapping;
		int m_MaxChunksMapped;
#if OLD
		int m_ChunksMapped;
		int m_CurrentUseCounter;
#endif

		#region ctor(), ~ctor()

		public PackedFile()
		{
			m_MaxChunksMapped = 78;
		}

		public void Dispose()
		{
			UnloadPackedFile();
		}

		#endregion

		#region File Utils

		static long GetSize(string szFile)
		{
			var fi = new FileInfo(szFile);
			if (fi.Exists)
				return fi.Length;
			return 0;
		}

		static long AlignToGranularity(long Offset, long Granularity)
		{
			long floor = Offset / Granularity;
			return (floor + 1) * Granularity;
		}

		//--------------------------------------------------------------------------------------
		// Write bytes into the file until the granularity is reached (see CreatePackedFile below)
		//--------------------------------------------------------------------------------------
		static long FillToGranularity(FileStream hFile, long CurrentOffset, long Granularity)
		{
			hFile.Position = CurrentOffset;
			long NewOffset = AlignToGranularity(hFile.Position, Granularity);
			for (long i = hFile.Position; i < NewOffset; i++)
				hFile.WriteByte(0);
			return hFile.Position;
		}

		#endregion

		#region CreatePackedFile()

		//--------------------------------------------------------------------------------------
		// Creates a packed file.  The file is a flat uncompressed file containing all resources
		// needed for the sample.  The file consists of chunks of data.  Each chunk represents
		// a mappable window that can be accessed by MapViewOfFile.  Since MapViewOfFile can
		// only map a view onto a file in 64k granularities, each chunk must start on a 64k
		// boundary.  The packed file also creates an index.  This index is loaded into memory
		// at startup and is not memory mapped.  The index is used to find the locations of 
		// resource files within the packed file.
		//--------------------------------------------------------------------------------------
		public unsafe void CreatePackedFile(
			Device pDev10, 
			string szFileName, 
			int SqrtNumTiles, 
			int SidesPerTile, 
			float fWorldScale, 
			float fHeightScale )
		{
			m_FileHeader.NumFiles = 4 * SqrtNumTiles * SqrtNumTiles;

			List<FILE_INDEX> TempFileIndices = new List<FILE_INDEX>();
			List<CHUNK_HEADER> TempHeaderList = new List<CHUNK_HEADER>();
			List<string> FullFilePath = new List<string>();

			string strDiffuseTexture = "ContentStream\\2kPanels_Diff.dds";
			string strNormalTexture = "ContentStream\\2kPanels_Norm.dds";
			string strTerrainHeight = "ContentStream\\terrain1.bmp";
			if(!File.Exists(strDiffuseTexture) 
				|| !File.Exists(strNormalTexture)
				|| !File.Exists(strTerrainHeight))
				throw new InvalidOperationException("Resource file(s) missing");

			long SizeDiffuse = GetSize( strDiffuseTexture );
			long SizeNormal = GetSize( strNormalTexture );

			var Terrain = new Terrain();
			Terrain.LoadTerrain(
				strTerrainHeight, 
				SqrtNumTiles, 
				SidesPerTile, 
				fWorldScale, 
				fHeightScale, 
				true);

			TERRAIN_TILE pTile = Terrain.GetTile( 0 );
			long SizeTerrainVB = pTile.RawVertices.Length * sizeof(TERRAIN_VERTEX);
			long SizeTerrainIB = Terrain.NumIndices * sizeof(ushort);

			float fTileWidth = pTile.BBox.max.X - pTile.BBox.min.X;
			float fChunkSpan = (float)Math.Sqrt( m_MaxChunksMapped ) - 1;
			long TotalTerrainTileSize = SizeTerrainVB + SizeTerrainIB + SizeDiffuse + SizeNormal;
			long LastChunkMegs = 0;
			long ChunkMegs = TotalTerrainTileSize;
			int iSqrt = 1;
			long SizeTry = 512;
			long VideoMemoryUsage = 0;
			long PrevVideoMemoryUsage = 0;
			long VidMemLimit = 512 * 1024 * 1024;
			int PrevMaxLoadedTiles = 0;
			int MaxLoadedTiles = 0;
			float fLoadingRadius = 0;
			float fPrevLoadingRadius = 0;
			while( VideoMemoryUsage < VidMemLimit )
			{
				LastChunkMegs = ChunkMegs;
				ChunkMegs = TotalTerrainTileSize;

				fPrevLoadingRadius = fLoadingRadius;
				fLoadingRadius = iSqrt * fTileWidth * ( fChunkSpan / 2.0f );
				float fLoadingArea = (float)Math.PI * fLoadingRadius * fLoadingRadius;
				float fTileArea = fTileWidth * fTileWidth;
				PrevMaxLoadedTiles = MaxLoadedTiles;
				MaxLoadedTiles = ( int )Math.Floor( fLoadingArea / fTileArea );
				PrevVideoMemoryUsage = VideoMemoryUsage;
				VideoMemoryUsage = ( long )( MaxLoadedTiles * TotalTerrainTileSize );

				iSqrt ++;
				SizeTry += 32;
			}
			iSqrt --;
			m_MaxChunksMapped = PrevMaxLoadedTiles + 20;
			ChunkMegs = LastChunkMegs;
			VideoMemoryUsage = PrevVideoMemoryUsage;
			fLoadingRadius = fPrevLoadingRadius;

			// Create Chunks
			int ChunkSide = SqrtNumTiles;
			int NumChunks = ChunkSide * ChunkSide;

			for( int i = 0; i < NumChunks; i++ )
			{	
				CHUNK_HEADER pChunkHeader = new CHUNK_HEADER();
				TempHeaderList.Add( pChunkHeader );
			}

			// Create indices
			int iTile = 0;
			for( int y = 0; y < SqrtNumTiles; y++ )
			{
				for( int x = 0; x < SqrtNumTiles; x++ )
				{
					int ChunkX = x;
					int ChunkY = y;
					int ChunkIndex = ChunkY * ChunkSide + ChunkX;

					// Tile
					pTile = Terrain.GetTile( iTile );
					Vector3 vCenter = ( pTile.BBox.min + pTile.BBox.max ) / 2.0f;

					// TerrainVB
					FILE_INDEX pFileIndex = new FILE_INDEX
					{
						szFileName = string.Format("terrainVB{0}_{1}", x, y),
						FileSize = SizeTerrainVB,
						ChunkIndex = ChunkIndex,
						OffsetIntoChunk = 0, // unknown
						vCenter = vCenter,
					};
					TempFileIndices.Add( pFileIndex );
					FullFilePath.Add( "VB" );

					// TerrainIB
					pFileIndex = new FILE_INDEX
					{
						szFileName = string.Format("terrainIB{0}_{1}", x, y ),
						FileSize = SizeTerrainIB,
						ChunkIndex = ChunkIndex,
						OffsetIntoChunk = 0, // unknown
						vCenter = vCenter,
					};
					TempFileIndices.Add( pFileIndex );
					FullFilePath.Add( "IB" );

					// TerrainDiffuse
					pFileIndex = new FILE_INDEX
					{
						szFileName = string.Format("terrainDiff{0}_{1}", x, y ),
						FileSize = SizeDiffuse,
						ChunkIndex = ChunkIndex,
						OffsetIntoChunk = 0, // unknown
						vCenter = vCenter,
					};
					TempFileIndices.Add( pFileIndex );
					FullFilePath.Add( strDiffuseTexture );

					// TerrainDiffuse
					pFileIndex = new FILE_INDEX
					{
						szFileName = string.Format("terrainNorm{0}_{1}", x, y ),
						FileSize = SizeNormal,
						ChunkIndex = ChunkIndex,
						OffsetIntoChunk = 0, // unknown
						vCenter = vCenter,
					};
					TempFileIndices.Add( pFileIndex );
					FullFilePath.Add( strNormalTexture );

					iTile++;
				}
			}

			// Get granularity
			WinAPI.SYSTEM_INFO SystemInfo;
			WinAPI.GetSystemInfo(out SystemInfo );
			long Granularity = SystemInfo.dwAllocationGranularity; // Allocation granularity (always 64k)

			// Calculate offsets into chunks
			for( int c = 0; c < NumChunks; c++ )
			{
				CHUNK_HEADER pChunkHeader = TempHeaderList[c];
				pChunkHeader.ChunkSize = 0;

				for( int i = 0; i < TempFileIndices.Count; i++ )
				{
					FILE_INDEX pIndex = TempFileIndices[ i ];

					if( pIndex.ChunkIndex == c )
					{
						pIndex.OffsetIntoChunk = pChunkHeader.ChunkSize;
						pChunkHeader.ChunkSize += pIndex.FileSize;

						// .NET WARNING: update the value in the list! beware of struct!
						TempFileIndices[ i ] = pIndex;
					}
				}

				// .NET WARNING: update the value in the list! beware of struct!
				TempHeaderList[c] = pChunkHeader;
			}

			long IndexSize = sizeof( PACKED_FILE_HEADER ) 
				+ sizeof( CHUNK_HEADER ) * TempHeaderList.Count 
				+ Marshal.SizeOf(typeof(FILE_INDEX)) * TempFileIndices.Count;
			long ChunkOffset = AlignToGranularity( IndexSize, Granularity );

			// Align chunks to the proper granularities
			for( int c = 0; c < NumChunks; c++ )
			{
				CHUNK_HEADER pChunkHeader = TempHeaderList[ c ];
				pChunkHeader.ChunkOffset = ChunkOffset;

				ChunkOffset += AlignToGranularity( pChunkHeader.ChunkSize, Granularity );

				// .NET WARNING: update the value in the list! beware of struct!
				TempHeaderList[c] = pChunkHeader;
			}

			// Fill in the header data
			m_FileHeader.FileSize = ChunkOffset;
			m_FileHeader.NumChunks = TempHeaderList.Count;
			m_FileHeader.NumFiles = TempFileIndices.Count;
			m_FileHeader.Granularity = Granularity;
			m_FileHeader.MaxChunksInVA = m_MaxChunksMapped;

			m_FileHeader.TileBytesSize = TotalTerrainTileSize;
			m_FileHeader.TileSideSize = pTile.BBox.max.X - pTile.BBox.min.X;
			m_FileHeader.LoadingRadius = fLoadingRadius;
			m_FileHeader.VideoMemoryUsageAtFullMips = VideoMemoryUsage;

			// Open the file
			using (FileStream hFile = new FileStream(szFileName, FileMode.Create, FileAccess.Write))
			{
				var buff = new BufferEx();

				// write the header
				buff.Write(hFile, m_FileHeader);

				// write out chunk headers
				foreach (var item in TempHeaderList)
					buff.Write(hFile, item);

				// write the index
				foreach (var item in TempFileIndices)
					buff.Write(hFile, item);

				// Fill in up to the granularity
				long CurrentFileSize = IndexSize;
				CurrentFileSize = FillToGranularity(hFile, CurrentFileSize, Granularity);

				// Write out the files
				for (int c = 0; c < TempHeaderList.Count; c++)
				{
					for (int i = 0; i < TempFileIndices.Count; i++)
					{
						FILE_INDEX pIndex = TempFileIndices[i];

						if (pIndex.ChunkIndex == c)
						{
							// Write out the indexed file
							byte[] pTempData;
							if (FullFilePath[i] == "VB")
							{
								using (var ms = new MemoryStream())
								{
									foreach (var item in Terrain.GetTile(i / 4).RawVertices)
										buff.Write(ms, item);
									pTempData = ms.ToArray();
								}
							}
							else if (FullFilePath[i] == "IB")
							{
								pTempData = new byte[pIndex.FileSize];
								fixed (ushort* pi = &Terrain.Indices[0])
									Marshal.Copy((IntPtr)pi, pTempData, 0, pTempData.Length);
							}
							else
							{
								using (var hIndexFile = new FileStream(FullFilePath[i], FileMode.Open, FileAccess.Read))
								{
									pTempData = new byte[pIndex.FileSize];
									hIndexFile.Read(pTempData, 0, pTempData.Length);
								}
							}
							hFile.Write(pTempData, 0, pTempData.Length);
							CurrentFileSize += pIndex.FileSize;
						}
					}

					// Fill in up to the granularity
					CurrentFileSize = FillToGranularity(hFile, CurrentFileSize, Granularity);
				}
			}
		}

		#endregion

		#region LoadPackedFile()

		//--------------------------------------------------------------------------------------
		// Loads the index of a packed file and optionally creates mapped pointers using
		// MapViewOfFile for each of the different chunks in the file.  The chunks must be
		// aligned to the proper granularity (64k) or MapViewOfFile will fail.
		//--------------------------------------------------------------------------------------
		public void LoadPackedFile(string szFileName, List<LEVEL_ITEM> pLevelItemArray)
		{
			// Open the file
			m_hFile = new FileStream(szFileName, FileMode.Open, FileAccess.Read);
			var buff = new BufferEx();

			// read the header
			m_FileHeader = buff.Read<PACKED_FILE_HEADER>(m_hFile);

			// Make sure the granularity is the same
			WinAPI.SYSTEM_INFO SystemInfo;
			WinAPI.GetSystemInfo( out SystemInfo );
			if( m_FileHeader.Granularity != SystemInfo.dwAllocationGranularity )
				throw new InvalidOperationException("Pack File Granularity doesn't match the system");

#if OLD
			m_ChunksMapped = 0;	
#endif

			// Create the chunk and index data
			// Load the chunk and index data
			m_pChunks = buff.Read<CHUNK_HEADER>(m_hFile, (int)m_FileHeader.NumChunks);
			m_pFileIndices = buff.Read<FILE_INDEX>(m_hFile, (int)m_FileHeader.NumFiles);

			// Load the level item array
			for( long i = 0; i < m_FileHeader.NumFiles; i += 4 )
			{
				LEVEL_ITEM pLevelItem = new LEVEL_ITEM
				{
					vCenter = m_pFileIndices[i].vCenter,
					szVBName = m_pFileIndices[i].szFileName,
					szIBName = m_pFileIndices[i + 1].szFileName,
					szDiffuseName = m_pFileIndices[i + 2].szFileName,
					szNormalName = m_pFileIndices[i + 3].szFileName,
					bLoaded = false,
					bLoading = false,
					bInLoadRadius = false,
					bInFrustum = false,
					CurrentCountdownDiff = 0,
					CurrentCountdownNorm = 0,
					bHasBeenRenderedDiffuse = false,
					bHasBeenRenderedNormal = false,
				};
				pLevelItemArray.Add( pLevelItem );
			}

			m_pMappedChunks = new MAPPED_CHUNK[m_FileHeader.NumChunks];
			for (int i = 0; i < m_FileHeader.NumChunks; i++)
				m_pMappedChunks[i] = new MAPPED_CHUNK();

			m_hFileMapping = MemoryMappedFile.CreateFromFile(
				m_hFile,
				null,
				0,
				MemoryMappedFileAccess.Read,
				null,
				HandleInheritability.None,
				true);
		}

		#endregion

		#region UnloadPackedFile()
		
		public void UnloadPackedFile()
		{
#if OLD
			if (m_pMappedChunks != null)
			{
				for(int i = 0; i < m_FileHeader.NumChunks; i++ )
				{
					if (m_pMappedChunks[i].bInUse)
						m_pMappedChunks[i].pMappingPointer.Dispose();
				}
			}
#endif
			m_pMappedChunks = null;

			if (m_hFileMapping != null)
				m_hFileMapping.Dispose();
			m_hFileMapping = null;

			if (m_hFile != null)
				m_hFile.Dispose();
			m_hFile = null;

			m_pChunks = null;
			m_pFileIndices = null;
		}

		#endregion

		#region EnsureChunkMapped()

		public void EnsureChunkMapped( int iChunk )
		{
#if OLD
			if( !m_pMappedChunks[iChunk].bInUse )
			{
				if( m_ChunksMapped == m_MaxChunksMapped )
				{
					// We need to free a chunk
					int lruValue = m_CurrentUseCounter;
					long lruChunk = -1;
					for( long i = 0; i < m_FileHeader.NumChunks; i++ )
					{
						if( m_pMappedChunks[i].bInUse )
						{
							if( lruChunk == -1 || m_pMappedChunks[i].UseCounter < lruValue )
							{
								lruValue = m_pMappedChunks[i].UseCounter;
								lruChunk = i;
							}
						}
					}

					//m_pMappedChunks[lruChunk].pMappingPointer.Dispose();
					//m_pMappedChunks[lruChunk].pMappingPointer = null;
					m_pMappedChunks[lruChunk].bInUse = false;
					m_ChunksMapped --;

					Console.WriteLine("Unmapped File Chunk");
				}

				// Map this chunk
				//m_pMappedChunks[iChunk].pMappingPointer = m_hFileMapping.CreateViewAccessor(m_pChunks[iChunk].ChunkOffset, m_pChunks[iChunk].ChunkSize);
				//m_pMappedChunks[iChunk].pMappingPointer = m_hFileMapping.CreateViewStream(m_pChunks[iChunk].ChunkOffset, m_pChunks[iChunk].ChunkSize);
				m_pMappedChunks[iChunk].bInUse = true;
				m_ChunksMapped++;
			}

			// Set our use counter for the LRU check
			m_pMappedChunks[iChunk].UseCounter = m_CurrentUseCounter;
			m_CurrentUseCounter ++;
#endif
		}

		#endregion

		#region GetPackedFileInfo()

		//--------------------------------------------------------------------------------------
		// Finds information about a resource using the index
		//--------------------------------------------------------------------------------------
		public bool GetPackedFileInfo(string szFile, out int pDataBytes)
		{
			int iFoundIndex = -1;
			for( int i = 0; i < m_FileHeader.NumFiles; i++ )
				if (szFile == m_pFileIndices[i].szFileName)
				{
					iFoundIndex = i;
					break;
				}

			if (-1 == iFoundIndex)
			{
				pDataBytes = 0;
				return false;
			}

			pDataBytes = (int)m_pFileIndices[iFoundIndex].FileSize;
			return true;
		}

		#endregion

		#region GetPackedFile()

		//--------------------------------------------------------------------------------------
		// Finds the location of a resource in a packed file and returns its contents in 
		// *ppData.
		//--------------------------------------------------------------------------------------
		//public void GetPackedFile(string szFile, out Stream ppData, out int pDataBytes)
		public Stream GetPackedFile(string szFile)
		{
			// Look the file up in the index
			int iFoundIndex = -1;
			for( int i = 0; i < m_FileHeader.NumFiles; i++ )
				if (szFile == m_pFileIndices[i].szFileName)
				{
					iFoundIndex = i;
					break;
				}

			if (-1 == iFoundIndex)
				return null;

#if OLD
			// Memory mapped io
			EnsureChunkMapped((int)m_pFileIndices[iFoundIndex].ChunkIndex);
			ppData = m_pMappedChunks[m_pFileIndices[iFoundIndex].ChunkIndex].pMappingPointer;
			pDataBytes = (int)m_pFileIndices[iFoundIndex].FileSize;
			ppData.Position = m_pFileIndices[iFoundIndex].OffsetIntoChunk;
#else
			long chunkoffset = m_pChunks[m_pFileIndices[iFoundIndex].ChunkIndex].ChunkOffset;
			long start = chunkoffset + m_pFileIndices[iFoundIndex].OffsetIntoChunk;
			long size = m_pFileIndices[iFoundIndex].FileSize;
			return m_hFileMapping.CreateViewStream(start, size, MemoryMappedFileAccess.Read);
#endif
		}

		#endregion

		//--------------------------------------------------------------------------------------
		public bool UsingMemoryMappedIO { get { return m_pMappedChunks != null; } }

		public int MaxChunksMapped
		{
			get { return m_MaxChunksMapped; }
			set { m_MaxChunksMapped = value; }
		}

		public long TileBytesSize { get { return m_FileHeader.TileBytesSize; } }

		public float TileSideSize { get { return m_FileHeader.TileSideSize; } }

		public float LoadingRadius { get { return m_FileHeader.LoadingRadius; } }

		public int MaxChunksInVA { get  { return m_FileHeader.MaxChunksInVA; } }

		public long NumChunks { get { return m_FileHeader.NumChunks; } }

		public long VideoMemoryUsageAtFullMips { get { return m_FileHeader.VideoMemoryUsageAtFullMips; } }
	}
}
