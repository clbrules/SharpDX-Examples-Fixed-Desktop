using System;
using System.IO;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D10;
using SharpDX.WPF;

using Buffer = SharpDX.Direct3D10.Buffer;
using D3DFORMAT = SharpDX.Direct3D9.Format;


namespace Week02Samples.ContentStream
{
    //--------------------------------------------------------------------------------------
    // IDataLoader is an interface that the AsyncLoader class uses to load data from disk.
    //
    // Load is called from the IO thread to load data.
    // Decompress is called by one of the processing threads to decompress the data.
    // Destroy is called by the graphics thread when it has consumed the data.
    //--------------------------------------------------------------------------------------
    public interface IDataLoader : IDisposable
    {
        Stream Decompress();
        void Load();
    }

    //--------------------------------------------------------------------------------------
    // IDataProcessor is an interface that the AsyncLoader class uses to process and copy
    // data into locked resource pointers.
    //
    // Process is called by one of the processing threads to process the data before it is
    //   consumed.
    // LockDeviceObject is called from the Graphics thread to lock the device object (D3D9).
    // UnLockDeviceObject is called from the Graphics thread to unlock the device object, or
    //   to call updatesubresource for D3D10.
    // CopyToResource copies the data from memory to the locked device object (D3D9).
    // SetResourceError is called to set the resource pointer to an error code in the event
    //   that something went wrong.
    // Destroy is called by the graphics thread when it has consumed the data.
    //--------------------------------------------------------------------------------------
    public interface IDataProcessor : IDisposable
    {
        bool LockDeviceObject();
        void UnLockDeviceObject();
        void Process(Stream pData);
        bool CopyToResource();
    }

    #region Texture Loader & Processor

    public class TextureLoader : IDataLoader, IDisposable
    {
        string m_szFileName;
        Stream m_pData;
        PackedFile m_pPackedFile;

        public TextureLoader(string szFileName, PackedFile pPackedFile)
        {
            m_pPackedFile = pPackedFile;
            m_szFileName = szFileName;
        }

        public void Dispose()
        {
            if(m_pData != null)
                m_pData.Dispose();
            m_pData = null;
        }

        //--------------------------------------------------------------------------------------
        // The SDK uses only DXTn (BCn) textures with a few small non-compressed texture.  However,
        // for a game that uses compressed textures or textures in a zip file, this is the place
        // to decompress them.
        //--------------------------------------------------------------------------------------
        public Stream Decompress() { return m_pData; }

        //--------------------------------------------------------------------------------------
        // Load the texture from the packed file.  If not-memory mapped, allocate enough memory
        // to hold the data.
        //--------------------------------------------------------------------------------------
        public void Load()
        {
            m_pData = m_pPackedFile.GetPackedFile(m_szFileName);
        }
    }

    public class TextureProcessor : IDataProcessor
    {
        public const int MAX_MIP_LEVELS = 32;

        Device m_Device;
        Stream m_pData;
        ResourceReuseCache m_pResourceReuseCache;
        ShaderResourceView m_pRealRV10;
        Texture2D m_pStaging10;
        readonly DataRectangle[] m_pLockedRects10 = new DataRectangle[MAX_MIP_LEVELS];
        int m_iNumLockedPtrs;
        int m_SkipMips;
        Action<ShaderResourceView> setRV;

        bool PopulateTexture()
        {
            if( 0 == m_iNumLockedPtrs )
                return false;

            var buff = new BufferEx();
            buff.GetBuffer(1 << 16);
            var dataptr = new StreamPtr(m_pData, 0);

            m_pData.Position = sizeof(int);
            DDS.HEADER pSurfDesc9 = buff.Read<DDS.HEADER>((Stream)(dataptr + sizeof(int)));

            int Width = pSurfDesc9.dwWidth;
            int Height = pSurfDesc9.dwHeight;
            int MipLevels = pSurfDesc9.dwMipMapCount;
            if( MipLevels > m_SkipMips )
                MipLevels -= m_SkipMips;
            else
                m_SkipMips = 0;
            if( 0 == MipLevels )
                MipLevels = 1;
            D3DFORMAT Format = FormatEx.GetD3D9Format( pSurfDesc9.ddspf );

            // Skip X number of mip levels
            int BytesToSkip = 0;
            for( int i = 0; i < m_SkipMips; i++ )
            {
                int SurfaceBytes = FormatEx.GetSurfaceInfo( Width, Height, Format);

                BytesToSkip += SurfaceBytes;
                Width = Width >> 1;
                Height = Height >> 1;
            }

            // Lock, fill, unlock
            int NumBytes, RowBytes, NumRows;
            long position = sizeof(int) + Marshal.SizeOf(typeof(DDS.HEADER)) + BytesToSkip;
            var pSrcBits = dataptr + sizeof(int) + Marshal.SizeOf(typeof(DDS.HEADER)) + BytesToSkip;
            for( int i = 0; i < m_iNumLockedPtrs; i++ )
            {
                FormatEx.GetSurfaceInfo( Width, Height, Format, out NumBytes, out RowBytes, out NumRows );

                var pDestBits = new StreamPtr(m_pLockedRects10[i].DataPointer, 0);

                // Copy stride line by line
                for( int h = 0; h < NumRows; h++ )
                {
                    buff.CopyMemory((Stream)pDestBits, (Stream)pSrcBits, RowBytes, buff.CurrentLength);
                    pDestBits += m_pLockedRects10[i].Pitch;
                    pSrcBits += RowBytes;
                }

                Width = Width >> 1;
                Height = Height >> 1;
                if( Width == 0 )
                    Width = 1;
                if( Height == 0 )
                    Height = 1;
            }
            return true;
        }

        //--------------------------------------------------------------------------------------
        public TextureProcessor(
            Action<ShaderResourceView> setRV,
            Device pDevice, 
            ResourceReuseCache pResourceReuseCache,
            int SkipMips ) 
        {
            m_Device = pDevice;
            m_pResourceReuseCache = pResourceReuseCache;
            m_SkipMips = SkipMips;
            this.setRV = setRV;
        }

        public void Dispose()
        {
        }

        //--------------------------------------------------------------------------------------
        // LockDeviceObject is called by the graphics thread to find an appropriate resource from
        // the resource reuse cache.  If no resource is found, the return code tells the calling
        // thread to try again later.  For D3D9, this function also locks all mip-levels.
        //--------------------------------------------------------------------------------------
        public bool LockDeviceObject()
        {
            m_iNumLockedPtrs = 0;

            if( m_pResourceReuseCache == null )
                throw new InvalidOperationException();

            // setup the pointers in the process request
            var buff = new BufferEx();
            buff.GetBuffer(1 << 16);
            var psdat = new StreamPtr(m_pData, 0);
            DDS.HEADER pSurfDesc9 = buff.Read<DDS.HEADER>((Stream)(psdat + sizeof(int)));

            int Width = pSurfDesc9.dwWidth;
            int Height = pSurfDesc9.dwHeight;
            int MipLevels = pSurfDesc9.dwMipMapCount;
            if( MipLevels > m_SkipMips )
                MipLevels -= m_SkipMips;
            else
                m_SkipMips = 0;
            if( 0 == MipLevels )
                MipLevels = 1;
            D3DFORMAT Format = FormatEx.GetD3D9Format( pSurfDesc9.ddspf );

            // Skip X number of mip levels
            for( int i = 0; i < m_SkipMips; i++ )
            {
                Width = Width >> 1;
                Height = Height >> 1;
            }

            // Find an appropriate resource
            m_pRealRV10 = m_pResourceReuseCache.GetFreeTexture10(Width, Height, MipLevels, ( int )Format, out m_pStaging10);
            if (m_pRealRV10 == null) // try again
                return false;

            // Lock
            m_iNumLockedPtrs = MipLevels - m_SkipMips;
            for( int i = 0; i < m_iNumLockedPtrs; i++ )
            {
                m_pLockedRects10[i] = m_pStaging10.Map(i, MapMode.Write, MapFlags.None);
            }
            return true;
        }

        //--------------------------------------------------------------------------------------
        // On D3D9, this unlocks the resource.  On D3D10, this actually populates the resource.
        //--------------------------------------------------------------------------------------
        public void UnLockDeviceObject()
        {
            if( 0 == m_iNumLockedPtrs )
                return;

            // Find an appropriate resource
            // Unlock
            for (int i = 0; i < m_iNumLockedPtrs; i++)
                m_pStaging10.Unmap(i);

            // Lloyd: warning: argument are swapped in CopyResource() compare to C++
            using (Resource pDest = m_pRealRV10.Resource)
                m_Device.CopyResource(m_pStaging10, pDest);

            setRV(m_pRealRV10);
        }

        //--------------------------------------------------------------------------------------
        // Any texture processing would go here.
        //--------------------------------------------------------------------------------------
        //public void Process( void* pData, SIZE_T cBytes )
        public void Process(Stream pData)
        {
            if( m_pResourceReuseCache != null)
            {	
                pData.Position = 0;
                int dwMagicNumber = pData.Read<int>();
                if( dwMagicNumber != 0x20534444 )
                    throw new ArgumentException();
            }
            m_pData = pData;
        }

        //--------------------------------------------------------------------------------------
        // Copies the data to the locked pointer on D3D9
        //--------------------------------------------------------------------------------------
        public bool CopyToResource()
        {
            return PopulateTexture();
        }
    }

    #endregion

    #region Vertex Loader & Processor

    public class VertexBufferLoader : IDataLoader
    {
        public VertexBufferLoader()
        {
        }
        public void Dispose()
        {
        }
        public Stream Decompress()
        {
            return null;
        }
        public void Load()
        {
        }
    }

    public class VertexBufferProcessor : IDataProcessor
    {
        Device m_Device;
        BufferDescription m_BufferDesc;
        Stream m_pData;
        ResourceReuseCache m_pResourceReuseCache;
        Buffer m_pRealBuffer10;
        Action<Buffer> setBuffer;

        public VertexBufferProcessor(
            Action<Buffer> setBuffer,
            Device pDevice, 
            BufferDescription pBufferDesc,
            Stream pData,
            ResourceReuseCache pResourceReuseCache )
        {
            m_Device = pDevice;
            m_pData = pData;
            m_pResourceReuseCache = pResourceReuseCache;
            m_BufferDesc = pBufferDesc;
            this.setBuffer = setBuffer;
        }

        public void Dispose()
        {
        }

        public Buffer Buffer { get { return m_pRealBuffer10; } }

        //--------------------------------------------------------------------------------------
        // LockDeviceObject is called by the graphics thread to find an appropriate resource from
        // the resource reuse cache.  If no resource is found, the return code tells the calling
        // thread to try again later.  For D3D9, this function also locks the resource.
        //--------------------------------------------------------------------------------------
        public bool LockDeviceObject()
        {
            if( m_pResourceReuseCache == null )
                throw new InvalidOperationException();

            m_pRealBuffer10 = m_pResourceReuseCache.GetFreeVB10( m_BufferDesc.SizeInBytes );
            if( null == m_pRealBuffer10 )
                return false;

            return true;
        }

        //--------------------------------------------------------------------------------------
        // On D3D9, this unlocks the resource.  On D3D10, this actually populates the resource.
        //--------------------------------------------------------------------------------------
        public void UnLockDeviceObject()
        {
            
            var data = m_pData;
            var buff = m_pRealBuffer10;
            IntPtr pData = new IntPtr();
            Marshal.StructureToPtr(data, pData, true);
            m_Device.UpdateSubresource(new DataBox(pData), m_pRealBuffer10, 0);
            setBuffer(m_pRealBuffer10);
        }

        //--------------------------------------------------------------------------------------
        public void Process(Stream data)
        {
        }

        //--------------------------------------------------------------------------------------
        // Copies the data to the locked pointer on D3D9
        //--------------------------------------------------------------------------------------
        public bool CopyToResource()
        {
            return true;
        }
    }

    #endregion

    #region IndexBuffer Loader & Processor

    public class IndexBufferLoader : IDataLoader
    {
        public IndexBufferLoader()
        {
        }
        public void Dispose()
        {
        }
        public Stream Decompress()
        {
            return null;
        }
        public void Load()
        {
        }
    }

    public class IndexBufferProcessor : IDataProcessor
    {
        Device m_Device;
        BufferDescription m_BufferDesc;
        Stream m_pData;
        ResourceReuseCache m_pResourceReuseCache;
        Buffer m_pRealBuffer10;
        Action<Buffer> setBuffer;

        public IndexBufferProcessor(
            Action<Buffer> setBuffer,
            Device pDevice, 
            BufferDescription pBufferDesc,
            Stream pData,
            ResourceReuseCache pResourceReuseCache ) 
        {
            m_Device = pDevice;
            m_pData = pData;
            m_pResourceReuseCache = pResourceReuseCache;
            m_BufferDesc = pBufferDesc;
            this.setBuffer = setBuffer;
        }

        public void Dispose()
        {
        }

        public Buffer Buffer { get { return m_pRealBuffer10; } }

        //--------------------------------------------------------------------------------------
        // LockDeviceObject is called by the graphics thread to find an appropriate resource from
        // the resource reuse cache.  If no resource is found, the return code tells the calling
        // thread to try again later.  For D3D9, this function also locks the resource.
        //--------------------------------------------------------------------------------------
        public bool LockDeviceObject()
        {
            if( m_pResourceReuseCache == null )
                throw new InvalidOperationException();

            m_pRealBuffer10 = m_pResourceReuseCache.GetFreeIB10(m_BufferDesc.SizeInBytes, 0);
            if( null == m_pRealBuffer10 )
                return false;

            return true;
        }

        //--------------------------------------------------------------------------------------
        // On D3D9, this unlocks the resource.  On D3D10, this actually populates the resource.
        //--------------------------------------------------------------------------------------
        public void UnLockDeviceObject()
        {
            m_Device.UpdateSubresource(m_pData, m_pRealBuffer10, 0);
            setBuffer(m_pRealBuffer10);
        }

        public void Process(Stream data)
        {
        }

        public bool CopyToResource()
        {
            return true;
        }
    }

    #endregion

    // TODO MeshLoader
}
