using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using SharpDX;

namespace Week02Samples.ContentStream
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct TERRAIN_VERTEX
	{
		public Vector3 pos;
		public Vector3 norm;
		public Vector2 uv;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct BOUNDING_BOX
	{
		public Vector3 min;
		public Vector3 max;
	}

	public class TERRAIN_TILE
	{
		public Color4 Color;
		public BOUNDING_BOX BBox;
		public TERRAIN_VERTEX[] RawVertices;
	}

	public class Terrain
	{
		// normal variable
		int m_SqrtNumTiles;
		int m_NumSidesPerTile;
		float m_fWorldScale;
		float m_fHeightScale;
		int m_HeightMapX;
		int m_HeightMapY;
		float[] m_pHeightBits;

		public float WorldScale { get { return m_fWorldScale; } }
		public int NumTileVertices { get { return (m_NumSidesPerTile + 1) * (m_NumSidesPerTile + 1); } }

		#region LoadTerrain(..)

		public void LoadTerrain(
			string strHeightMap, 
			int SqrtNumTiles, 
			int NumSidesPerTile, 
			float fWorldScale,
			float fHeightScale, 
			bool bCreateTiles)
		{
			m_SqrtNumTiles = SqrtNumTiles;
			m_fWorldScale = fWorldScale;
			m_fHeightScale = fHeightScale;
			m_NumSidesPerTile = NumSidesPerTile;
			m_NumIndices = (m_NumSidesPerTile + 2) * 2 * (m_NumSidesPerTile) - 2;

			LoadHeightImage(strHeightMap);

			// Refactored, extracted the code below (inline in the C++ version)
			if (bCreateTiles)
				CreateTerrain(SqrtNumTiles, NumSidesPerTile);
		}

		private unsafe void LoadHeightImage(string strHeightMap)
		{
			if (!File.Exists(strHeightMap))
				throw new ArgumentException("No such file " + strHeightMap);

			using (var imgstream = new FileStream(strHeightMap, FileMode.Open, FileAccess.Read))
			{
				var bframe = BitmapFrame.Create(imgstream);
				var bitmap = new WriteableBitmap(bframe);
				int U = m_HeightMapX = bitmap.PixelWidth;
				int V = m_HeightMapY = bitmap.PixelHeight;
				m_pHeightBits = new float[U * V];

				//int iStep = bitmap.Format.BitsPerPixel / 8;
				bitmap.Lock();
				try
				{
					byte* pBits = (byte*)bitmap.BackBuffer;
					int iHeight = 0;
					int iBitmap = 0;
					int stride = bitmap.BackBufferStride;
					for (int y = 0; y < V; y++)
					{
						// Lloyd: the WriteableBitmap will be 4 bit color RGBA
						// => average RGB, ignore A, ignore iStep
						// Lloyd (2): for some reason the order of Row seems reverse to the HBITMAP version
						iBitmap = (V - 1 - y) * stride;
						for (int x = 0; x < U * 4; x += 4)
						{
							m_pHeightBits[iHeight] = 0;
							for (int c = 0; c < 3; c++)
								m_pHeightBits[iHeight] += pBits[iBitmap + c];

							m_pHeightBits[iHeight] /= 3 * 255.0f;
							m_pHeightBits[iHeight] *= m_fHeightScale;
							iHeight++;
							iBitmap += 4;
						}
					}
				}
				finally { bitmap.Unlock(); }
			}
		}


		//--------------------------------------------------------------------------------------
		int HEIGHT_INDEX(int a, int b) { return ((b) * m_HeightMapX + (a)); }
		float LINEAR_INTERPOLATE(float a, float b, float x) { return (a * (1.0f - x) + b * x); }
		public float GetHeightOnMap(Vector3 pPos)
		{
			// move x and z into [0..1] range
			Vector2 uv = GetUVForPosition( pPos );
			float x = uv.X;
			float z = uv.Y;

			// scale into heightmap space
			x *= m_HeightMapX;
			z *= m_HeightMapY;
			x += 0.5f;
			z += 0.5f;
			if( x >= m_HeightMapX - 1 )
				x = ( float )m_HeightMapX - 2;
			if( z >= m_HeightMapY - 1 )
				z = ( float )m_HeightMapY - 2;
			z = Math.Max( 0, z );
			x = Math.Max( 0, x );

			// bilinearly interpolate
			int integer_X = (int)( x );
			float fractional_X = x - integer_X;

			int integer_Z = (int)( z );
			float fractional_Z = z - integer_Z;

			float v1 = m_pHeightBits[ HEIGHT_INDEX( integer_X,    integer_Z ) ];
			float v2 = m_pHeightBits[ HEIGHT_INDEX( integer_X + 1,integer_Z ) ];
			float v3 = m_pHeightBits[ HEIGHT_INDEX( integer_X,    integer_Z + 1 ) ];
			float v4 = m_pHeightBits[ HEIGHT_INDEX( integer_X + 1,integer_Z + 1 ) ];

			float i1 = LINEAR_INTERPOLATE( v1 , v2 , fractional_X );
			float i2 = LINEAR_INTERPOLATE( v3 , v4 , fractional_X );

			float result = LINEAR_INTERPOLATE( i1 , i2 , fractional_Z );

			return result;
		}


		//--------------------------------------------------------------------------------------
		public Vector3 GetNormalOnMap(Vector3 pPos)
		{
			// Calculate the normal
			float xDelta = ( m_fWorldScale / ( float )m_SqrtNumTiles ) / ( float )m_NumSidesPerTile;
			float zDelta = ( m_fWorldScale / ( float )m_SqrtNumTiles ) / ( float )m_NumSidesPerTile;

			Vector3 vLeft = pPos - new Vector3(xDelta, 0, 0);
			Vector3 vRight = pPos + new Vector3(xDelta, 0, 0);
			Vector3 vUp = pPos + new Vector3(0, 0, zDelta);
			Vector3 vDown = pPos - new Vector3(0, 0, zDelta);

			vLeft.Y = GetHeightOnMap( vLeft );
			vRight.Y = GetHeightOnMap( vRight );
			vUp.Y = GetHeightOnMap( vUp );
			vDown.Y = GetHeightOnMap( vDown );

			Vector3 e0 = vRight - vLeft;
			Vector3 e1 = vUp - vDown;
			Vector3 ortho;
			Vector3 norm;
			Vector3.Cross(ref e1, ref e0, out ortho); //D3DXVec3Cross( &ortho, &e1, &e0 );
			Vector3.Normalize(ref ortho, out norm);

			return norm;
		}

		public float GetHeightForTile(int iTile, Vector3 pPos)
		{
			// TODO: impl
			return 0.0f;
		}

		Vector2 GetUVForPosition(Vector3 pPos)
		{
			Vector2 uv;
			uv.X = ( pPos.X / m_fWorldScale ) + 0.5f;
			uv.Y = ( pPos.Z / m_fWorldScale ) + 0.5f;
			return uv;
		}

		/// <summary>
		/// Use the same random seed each time for consistent repros
		/// (as in the sample: ContentStreaming10.cpp:587)
		/// </summary>
		public static float RPercent()
		{
			return 2 * (float)RAND.NextDouble() - 1;
		}
		static Random RAND = new Random(100);

		#endregion

		#region CreateTerrain(..)

		public int NumTiles { get { return m_pTiles.Length; } }
		public TERRAIN_TILE GetTile(int index) { return m_pTiles[index]; }
		public ushort[] Indices { get { return m_pTerrainRawIndices; } }
		public int NumIndices { get { return m_NumIndices; } }

		// create variable
		TERRAIN_TILE[] m_pTiles;
		int m_NumIndices;
		ushort[] m_pTerrainRawIndices;

		// Refactored, extracted the LoadTerrain() (inline in the C++ version)
		private void CreateTerrain(int SqrtNumTiles, int NumSidesPerTile)
		{
			m_pTiles = new TERRAIN_TILE[SqrtNumTiles * SqrtNumTiles];

			int iTile = 0;
			float zStart = -m_fWorldScale / 2.0f;
			float zDelta = m_fWorldScale / (float)m_SqrtNumTiles;
			float xDelta = m_fWorldScale / (float)m_SqrtNumTiles;
			for (int z = 0; z < m_SqrtNumTiles; z++)
			{
				float xStart = -m_fWorldScale / 2.0f;
				for (int x = 0; x < m_SqrtNumTiles; x++)
				{
					BOUNDING_BOX BBox;
					BBox.min = new Vector3(xStart, 0, zStart);
					BBox.max = new Vector3(xStart + xDelta, 0, zStart + zDelta);

					m_pTiles[iTile] = GenerateTile(BBox);

					iTile++;
					xStart += xDelta;
				}
				zStart += zDelta;
			}

			// Create the indices for the tile strips
			m_pTerrainRawIndices = new ushort[m_NumIndices];

			ushort vIndex = 0;
			int iIndex = 0;
			for (int z = 0; z < m_NumSidesPerTile; z++)
			{
				for (int x = 0; x < m_NumSidesPerTile + 1; x++)
				{
					m_pTerrainRawIndices[iIndex] = vIndex;
					iIndex++;
					m_pTerrainRawIndices[iIndex] = (ushort)(vIndex + m_NumSidesPerTile + 1);
					iIndex++;
					vIndex++;
				}
				if (z != m_NumSidesPerTile - 1)
				{
					// add a degenerate tri
					m_pTerrainRawIndices[iIndex] = (ushort)(vIndex + m_NumSidesPerTile);
					iIndex++;
					m_pTerrainRawIndices[iIndex] = vIndex;
					iIndex++;
				}
			}
		}

		private TERRAIN_TILE GenerateTile(BOUNDING_BOX pBBox)
		{
			var pTile = new TERRAIN_TILE();

			// Alloc memory for the vertices
			pTile.RawVertices = new TERRAIN_VERTEX[( m_NumSidesPerTile + 1 ) * ( m_NumSidesPerTile + 1 )];
			pTile.BBox = pBBox;
			pTile.Color.Red = 0.60f + RPercent() * 0.40f;
			pTile.Color.Green = 0.60f + RPercent() * 0.40f;
			pTile.Color.Blue = 0.60f + RPercent() * 0.40f;
			pTile.Color.Alpha = 1.0f;

			int iVertex = 0;
			float zStart = pBBox.min.Z;
			float xDelta = ( pBBox.max.X - pBBox.min.X ) / ( float )m_NumSidesPerTile;
			float zDelta = ( pBBox.max.Z - pBBox.min.Z ) / ( float )m_NumSidesPerTile;

			// Loop through terrain vertices and get height from the heightmap
			for( int z = 0; z < m_NumSidesPerTile + 1; z++ )
			{
				float xStart = pBBox.min.X;
				for( int x = 0; x < m_NumSidesPerTile + 1; x++ )
				{
					var pos = new Vector3( xStart,0,zStart );
					//Vector3 norm;
					pos.Y = GetHeightOnMap(pos);
					pTile.RawVertices[iVertex].pos = pos;
					//pTile.RawVertices[iVertex].uv = GetUVForPosition( &pos );
					//pTile.RawVertices[iVertex].uv.Y = 1.0f - pTile.RawVertices[iVertex].uv.Y;
					pTile.RawVertices[iVertex].uv.X = ( float )x / ( ( float )m_NumSidesPerTile );
					pTile.RawVertices[iVertex].uv.Y = 1.0f - ( float )z / ( ( float )m_NumSidesPerTile );
					pTile.RawVertices[iVertex].norm = GetNormalOnMap(pos);

					iVertex ++;
					xStart += xDelta;
				}
				zStart += zDelta;
			}
			return pTile;
		}

		#endregion
	}
}
