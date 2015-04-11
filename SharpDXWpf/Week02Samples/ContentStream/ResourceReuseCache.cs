using System;
using System.Collections.Generic;
using System.ComponentModel;
using SharpDX.Direct3D10;
using Buffer = SharpDX.Direct3D10.Buffer;
using D3DFORMAT = SharpDX.Direct3D9.Format;

namespace Week02Samples.ContentStream
{
	public class DEVICE_TEXTURE : IDisposable
	{
		public int Width;
		public int Height;
		public int MipLevels;
		public int Format;
		public ShaderResourceView pRV10;
		public Texture2D pStaging10;
		public long EstimatedSize;
		public bool bInUse;
		public int RecentUseCounter;

		public bool Match(int Width, int Height, int MipLevels, int Format)
		{
			return this.Width == Width &&
				this.Height == Height &&
				this.MipLevels == MipLevels &&
				this.Format == Format;
		}

		public void Dispose()
		{
			pRV10.Dispose();
			pStaging10.Dispose();
			pRV10 = null;
			pStaging10 = null;
		}
	}

	public class DEVICE_VERTEX_BUFFER : IDisposable
	{
		public int iSizeBytes;
		public Buffer pVB10;
		public bool bInUse; 
		public int RecentUseCounter;
	
		public void  Dispose()
		{
            while (!pVB10.IsDisposed)
                pVB10.Dispose();
		}
	}

	public class DEVICE_INDEX_BUFFER : IDisposable
	{
		public int iSizeBytes;
		public int ibFormat;
		public Buffer pIB10;
		public bool bInUse; 
		public int RecentUseCounter;

        public void Dispose()
        {
            while (!pIB10.IsDisposed)
                pIB10.Dispose();
        }
	}

	public class ResourceReuseCache : IDisposable, INotifyPropertyChanged
	{
		Device m_Device;
		List<DEVICE_TEXTURE> m_TextureList = new List<DEVICE_TEXTURE>();
		List<DEVICE_VERTEX_BUFFER> m_VBList = new List<DEVICE_VERTEX_BUFFER>();
		List<DEVICE_INDEX_BUFFER> m_IBList = new List<DEVICE_INDEX_BUFFER>();
		bool m_bSilent;

		#region (private) Texture Functions

		int FindTexture(ShaderResourceView pRV10 ) 
		{
			if (pRV10 == null)
				return -1;
			return m_TextureList.FindIndex(texTest => texTest.pRV10.NativePointer == pRV10.NativePointer); 
		}

		int EnsureFreeTexture(int Width, int Height, int MipLevels, int Format )
		{
			// see if we have a free one available
			int freeIndex = m_TextureList.FindIndex(aTEX => !aTEX.bInUse && aTEX.Match(Width, Height, MipLevels, Format));
			if (freeIndex > -1)
			{
				m_TextureList[freeIndex].bInUse = true;
				return freeIndex;
			}

			// haven't found a free one
			// try to create a new one
			long newSize = FormatEx.GetEstimatedSize( Width, Height, MipLevels, Format );
			long sizeNeeded = UsedManagedMemory + newSize;
			if( sizeNeeded > MaxManagedMemory )
				DestroyLRUResources( newSize );

			if( !DontCreateResources )
			{
				DEVICE_TEXTURE tex = new DEVICE_TEXTURE
				{
					Width = Width,
					Height = Height,
					MipLevels = MipLevels,
					Format = Format,
					EstimatedSize = newSize,
					RecentUseCounter = 0,
				};

				if( !m_bSilent )
					Console.WriteLine( "RESOURCE WARNING: Device needs to create new Texture" );

				{
					var desc = new Texture2DDescription
					{
						Width = Width,
						Height = Height,
						MipLevels = MipLevels,
						ArraySize = 1,
						Format = FormatEx.ToDXGI(( D3DFORMAT )Format),
						SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
						Usage = ResourceUsage.Default,
						BindFlags = BindFlags.ShaderResource,
						CpuAccessFlags = CpuAccessFlags.None,
						OptionFlags = ResourceOptionFlags.None,
					};

					using (Texture2D pTex2D = new Texture2D(m_Device, desc))
					{
						var SRVDesc = new ShaderResourceViewDescription
						{
							Format = desc.Format,
							Dimension = SharpDX.Direct3D.ShaderResourceViewDimension.Texture2D,
							Texture2D = { MipLevels = desc.MipLevels, }
						};
						tex.pRV10 = new ShaderResourceView(m_Device, pTex2D, SRVDesc);
					}

					desc.Usage = ResourceUsage.Staging;
					desc.BindFlags = BindFlags.None;
					desc.CpuAccessFlags = CpuAccessFlags.Write;
					tex.pStaging10 = new Texture2D(m_Device, desc);
				}

				UsedManagedMemory += tex.EstimatedSize;
				tex.bInUse = true;

				int index = m_TextureList.Count;
				m_TextureList.Add( tex );
				return index;
			}

			return -1;
		}

		#endregion

		#region (private) Vertex Buffer functions

		int FindVB(Buffer pVB) 
		{
			if (pVB == null)
				return -1;
			return m_VBList.FindIndex(vb => vb.pVB10.NativePointer == pVB.NativePointer); 
		}

		//--------------------------------------------------------------------------------------
		int EnsureFreeVB( int iSizeBytes )
		{
			// Find the closest match
			int closestindex = m_VBList.FindIndex(vb => !vb.bInUse && vb.iSizeBytes == iSizeBytes);

			// if we found a closest match, return it
			if( -1 != closestindex )
			{
				DEVICE_VERTEX_BUFFER vb = m_VBList[ closestindex ];
				vb.bInUse = true;
				return closestindex;
			}

			// haven't found a free one
			// try to create a new one
			long newSize = iSizeBytes;
			long sizeNeeded = UsedManagedMemory + newSize;
			if( sizeNeeded > MaxManagedMemory )
				DestroyLRUResources( newSize );

			if( !DontCreateResources )
			{
				DEVICE_VERTEX_BUFFER vb = new DEVICE_VERTEX_BUFFER
				{
					iSizeBytes = iSizeBytes,
					RecentUseCounter = 0,
				};

				if( !m_bSilent )
					Console.WriteLine( "RESOURCE WARNING: Device needs to create new Vertex Buffer" );

				{
					var BufferDesc = new BufferDescription 
					{
						SizeInBytes = iSizeBytes,
						Usage = ResourceUsage.Default,
						BindFlags = BindFlags.VertexBuffer,
						CpuAccessFlags = CpuAccessFlags.None,
						OptionFlags = ResourceOptionFlags.None,
					};
					vb.pVB10 = new Buffer(m_Device, BufferDesc);
				}

				UsedManagedMemory += vb.iSizeBytes;
				vb.bInUse = true;
				int index = m_VBList.Count;
				m_VBList.Add( vb );
				return index;
			}

			return -1;
		}

		#endregion

		#region (private) Index Buffer

		int FindIB(Buffer pIB) 
		{
			if (pIB == null)
				return -1;
			return m_IBList.FindIndex(IB => IB.pIB10.NativePointer == pIB.NativePointer); 
		}

		//--------------------------------------------------------------------------------------
		int EnsureFreeIB( int iSizeBytes, int ibFormat )
		{
			// Find the closest match
			int closestindex = m_IBList.FindIndex(IB =>
			{
				if (!IB.bInUse)
				{
					if (0 == ibFormat)
						ibFormat = IB.ibFormat;
					if (IB.iSizeBytes == iSizeBytes && IB.ibFormat == ibFormat)
						return true;
				}
				return false;
			});

			// if we found a closest match, return it
			if( -1 != closestindex )
			{
				DEVICE_INDEX_BUFFER IB = m_IBList[ closestindex ];
				IB.bInUse = true;
				return closestindex;
			}

			// We haven't found a free one, so create a new one
			long newSize = iSizeBytes;
			long sizeNeeded = UsedManagedMemory + newSize;
			if( sizeNeeded > MaxManagedMemory )
				DestroyLRUResources( newSize );

			if( !DontCreateResources )
			{
				DEVICE_INDEX_BUFFER IB = new DEVICE_INDEX_BUFFER
				{
					iSizeBytes = iSizeBytes,
					ibFormat = ibFormat,
					RecentUseCounter = 0,
				};

				if( !m_bSilent )
					Console.WriteLine( "RESOURCE WARNING: Device needs to create new Index Buffer" );

				{
					var BufferDesc = new BufferDescription
					{
						SizeInBytes = iSizeBytes,
						Usage = ResourceUsage.Default,
						BindFlags = BindFlags.IndexBuffer,
						CpuAccessFlags = CpuAccessFlags.None,
						OptionFlags = ResourceOptionFlags.None,
					};
					IB.pIB10 = new Buffer(m_Device, BufferDesc);
				}

				UsedManagedMemory += IB.iSizeBytes;
				IB.bInUse = true;
				int index = m_IBList.Count;
				m_IBList.Add( IB );
				return index;
			}

			return -1;
		}

		#endregion

		#region (private) DestroyXXX

		public void OnDestroy()
		{
			foreach (var item in m_TextureList)
				item.Dispose();
			foreach (var item in m_VBList)
				item.Dispose();
			foreach (var item in m_IBList)
				item.Dispose();

			m_TextureList.Clear();
			m_VBList.Clear();
			m_IBList.Clear();
			UsedManagedMemory = 0;
		}

		void DestroyLRUResources( long SizeGainNeeded )
		{
			long ReleasedSize = 0;
			while( ReleasedSize < SizeGainNeeded )
			{
				ReleasedSize += DestroyLRUTexture();
				if( ReleasedSize > SizeGainNeeded )
					return;

				ReleasedSize += DestroyLRUVB();
				if( ReleasedSize > SizeGainNeeded )
					return;

				ReleasedSize += DestroyLRUIB();
				if( ReleasedSize > SizeGainNeeded )
					return;
			}
		}

		//--------------------------------------------------------------------------------------

		static int IndexLessUsed<T>(List<T> list, Func<T, int> getUsageCount)
		{
			if (list.Count == 0)
				return -1;

			int index = 0;
			for (int i = 1; i < list.Count; i++)
			{
				if(getUsageCount(list[i]) < getUsageCount(list[index]))
					index = i;
			}
			return index;
		}

		long DestroyLRUTexture()
		{
			int index = IndexLessUsed(m_TextureList, aTex => aTex.RecentUseCounter);
			if (index < 0)
				return 0;

			DEVICE_TEXTURE pLRURes = m_TextureList[index];
			m_TextureList.RemoveAt(index);
			long SizeGain = pLRURes.EstimatedSize;
			UsedManagedMemory -= SizeGain;
			pLRURes.Dispose();
			return SizeGain;
		}

		//--------------------------------------------------------------------------------------
		long DestroyLRUVB()
		{
			int index = IndexLessUsed(m_VBList, aVBex => aVBex.RecentUseCounter);
			if (index < 0)
				return 0;

			DEVICE_VERTEX_BUFFER pLRURes = m_VBList[index];
			m_VBList.RemoveAt(index);
			long SizeGain = pLRURes.iSizeBytes;
			UsedManagedMemory -= SizeGain;
			pLRURes.Dispose();
			return SizeGain;
		}

		//--------------------------------------------------------------------------------------
		long DestroyLRUIB()
		{
			int index = IndexLessUsed(m_IBList, aIBex => aIBex.RecentUseCounter);
			if (index < 0)
				return 0;

			DEVICE_INDEX_BUFFER pLRURes = m_IBList[index];
			m_IBList.RemoveAt(index);
			long SizeGain = pLRURes.iSizeBytes;
			UsedManagedMemory -= SizeGain;
			pLRURes.Dispose();
			return SizeGain;
		}

		#endregion

		// -- public area ---

		#region ctor, ~ctor

		public ResourceReuseCache(Device pDev )
		{
			MaxManagedMemory = 1024 * 1024 * 32;
			m_Device = pDev;
			m_bSilent = false;
		}

		~ResourceReuseCache() { Dispose(false); }

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}

		void Dispose(bool disposing) { OnDestroy(); }

		#endregion

		#region Texture functions: GetXXX(), UnusedXXX()

		public ShaderResourceView GetFreeTexture10(int Width, int Height, int MipLevels, int Format, out Texture2D ppStaging10)
		{
			int iTex = EnsureFreeTexture( Width, Height, MipLevels, Format );
			if (-1 == iTex)
			{
				ppStaging10 = null;
				return null;
			}
			else
			{
				ppStaging10 = m_TextureList[iTex].pStaging10;
				return m_TextureList[iTex].pRV10;
			}
		}

		public int GetNumTextures() { return m_TextureList.Count; }

		public DEVICE_TEXTURE GetTexture( int i ) { return m_TextureList[ i ]; }

		public void UnuseDeviceTexture10(ShaderResourceView pRV)
		{
			int index = FindTexture(pRV);
			if (index >= 0)
			{
				DEVICE_TEXTURE tex = m_TextureList[index];
				tex.bInUse = false;
			}
		}

		#endregion

		#region Vertex Buffer functions: GetXXX(), UnusedXXX()

		public Buffer GetFreeVB10(int sizeBytes)
		{
			int iVB = EnsureFreeVB( sizeBytes );
			if( -1 == iVB )
				return null;
			else
				return m_VBList[ iVB ].pVB10;
		}

		public int GetNumVBs() { return m_VBList.Count; }

		public DEVICE_VERTEX_BUFFER GetVB( int i ) { return m_VBList[ i ]; }

		public void UnuseDeviceVB10(Buffer pVB)
		{
			int index = FindVB( pVB );
			if( index >= 0 )
			{
				DEVICE_VERTEX_BUFFER vb = m_VBList[ index ];
				vb.bInUse = false;
			}
		}

		#endregion

		#region Index Buffer functions: GetXXX(), UnusedXXX()

		public Buffer GetFreeIB10( int sizeBytes, int ibFormat )
		{
			int iIB = EnsureFreeIB( sizeBytes, ibFormat );
			if( -1 == iIB )
				return null;
			else
				return m_IBList[ iIB ].pIB10;
		}

		public int GetNumIBs() { return m_IBList.Count; }

		public DEVICE_INDEX_BUFFER GetIB( int i ) { return m_IBList[ i ]; }

		public void UnuseDeviceIB10(Buffer pIB)
		{
			int index = FindIB( pIB );
			if( index >= 0 )
			{
				DEVICE_INDEX_BUFFER IB = m_IBList[ index ];
				IB.bInUse = false;
			}
		}

		#endregion

		#region MaxManagedMemory

		public long MaxManagedMemory
		{
			get { return mMaxManagedMemory; }
			set
			{
				if (value == mMaxManagedMemory)
					return;
				mMaxManagedMemory = value;
				OnPropertyChanged("MaxManagedMemory");
			}
		}
		long mMaxManagedMemory;

		#endregion

		#region UsedManagedMemory

		public long UsedManagedMemory
		{
			get { return mUsedManagedMemory; }
			set
			{
				if (value == mUsedManagedMemory)
					return;
				mUsedManagedMemory = value;
				OnPropertyChanged("UsedManagedMemory");
			}
		}
		long mUsedManagedMemory;

		#endregion

		#region DontCreateResources

		public bool DontCreateResources
		{
			get { return mDontCreateResources; }
			set
			{
				if (value == mDontCreateResources)
					return;
				mDontCreateResources = value;
				OnPropertyChanged("DontCreateResources");
			}
		}
		bool mDontCreateResources;

		#endregion

		#region INotifyPropertyChanged Members

		void OnPropertyChanged(string name)
		{
			var e = PropertyChanged;
			if (e != null)
				e(this, new PropertyChangedEventArgs(name));
		}

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion
	}
}
