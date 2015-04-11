using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpDX.WPF;
using SharpDX.Direct3D10;
using SharpDX;
using System.IO;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D10.Buffer;
using System.Windows;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using System.Runtime.InteropServices;

namespace Week02Samples.ContentStream
{
	public class Renderer : D3D10
	{
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			// NOTE: SharpDX 1.3 requires explicit Dispose() of everything

			Set(ref g_PackFile, null);
			Set(ref g_pAsyncLoader, null);
			Set(ref g_pEffect10, null);
		}

		// Direct3D 10 resources
		AsyncLoader g_pAsyncLoader;
		ResourceReuseCache g_pResourceReuseCache;
		PackedFile g_PackFile = new PackedFile();
		Effect g_pEffect10;
		InputLayout g_pLayoutObject;

		// Effect variables
		EffectMatrixVariable g_pmWorld;
		EffectMatrixVariable g_pmWorldViewProjection;
		EffectVectorVariable g_pEyePt;
		EffectShaderResourceVariable g_ptxDiffuse;
		EffectShaderResourceVariable g_ptxNormal;
		EffectTechnique g_pRenderTileDiff;
		EffectTechnique g_pRenderTileBump;
		EffectTechnique g_pRenderTileWire;

		float g_fFOV = 70.0f;
		float g_fAspectRatio = 1.0f;
		float g_fInitialLoadTime = 0.0f;
		int g_NumModelsInUse = 0;
		float g_fLoadingRadius;
		float g_fVisibleRadius = 1500;
		float g_fViewHeight = 7.5f;
		int g_NumResourceToLoadPerFrame = 1;
		int g_UploadToVRamEveryNthFrame = 3;
		int g_SkipMips = 0;
		int g_NumProcessingThreads = 1;
		long g_AvailableVideoMem = 0;
		bool g_bUseWDDMPaging = false;
		bool g_bStartupResourcesLoaded = false;
		bool g_bWireframe = false;

		List<LEVEL_ITEM> g_LevelItemArray = new List<LEVEL_ITEM>();
		List<LEVEL_ITEM> g_VisibleItemArray = new List<LEVEL_ITEM>();
		List<LEVEL_ITEM> g_LoadedItemArray = new List<LEVEL_ITEM>();
		Terrain g_Terrain = new Terrain();

		public enum LOAD_TYPE
		{
			MULTITHREAD = 0,
			SINGLETHREAD,
		}
		LOAD_TYPE g_LoadType = LOAD_TYPE.SINGLETHREAD; // LOAD_TYPE.MULTITHREAD;

		public enum APP_STATE
		{
			STARTUP = 0,
			RENDER_SCENE
		}
		APP_STATE g_AppState = APP_STATE.STARTUP; // Current state of app


		public Renderer()
		{
			using (var xgidev = Device.QueryInterface<SharpDX.DXGI.Device>())
			using (var adapter = xgidev.Adapter)
			{
				g_AvailableVideoMem = adapter.Description.DedicatedVideoMemory;
				if(g_AvailableVideoMem == 0)
					g_AvailableVideoMem = adapter.Description.SharedSystemMemory;
				// Lloyd: adapter.Description.SharedSystemMemory is negative!?!
				if (g_AvailableVideoMem < 0)
					g_AvailableVideoMem *= -1;
			}

			Camera = new FirstPersonCamera();
			Camera.SetScalers(0.0f, 100.0f);
			Camera.EnableYAxisMovement = false;

			OnD3D10CreateDevice();
		}

		#region OnD3D10CreateDevice()

		//--------------------------------------------------------------------------------------
		// Create any D3D10 resources that aren't dependant on the back buffer
		//--------------------------------------------------------------------------------------
		void OnD3D10CreateDevice() 
		{
			using (var dg = new DisposeGroup())
			{
				var sFlags = ShaderFlags.EnableBackwardsCompatibility;
#if DEBUG
				sFlags |= ShaderFlags.Debug;
#endif
				// Read the D3DX effect file
				var Defines = new[] { new ShaderMacro("D3D10", "TRUE") };
				var sbcfile = dg.Add(ShaderBytecode.CompileFromFile(
					"ContentStream\\ContentStream.fx", 
					"fx_4_0", 
					sFlags, EffectFlags.None, Defines, null));
				Set(ref g_pEffect10, new Effect(Device, sbcfile));

				// Get the effect variable handles
				g_pmWorld = g_pEffect10.GetVariableByName("g_mWorld").AsMatrix();
				g_pmWorldViewProjection = g_pEffect10.GetVariableByName("g_mWorldViewProj").AsMatrix();
				g_pEyePt = g_pEffect10.GetVariableByName("g_vEyePt").AsVector();
				g_ptxDiffuse = g_pEffect10.GetVariableByName("g_txDiffuse").AsShaderResource();
				g_ptxNormal = g_pEffect10.GetVariableByName("g_txNormal").AsShaderResource();

				g_pRenderTileDiff = g_pEffect10.GetTechniqueByName("RenderTileDiff10");
				g_pRenderTileBump = g_pEffect10.GetTechniqueByName("RenderTileBump10");
				g_pRenderTileWire = g_pEffect10.GetTechniqueByName("RenderTileWire10");

				// Create a layout for the object data.
				var layoutObject = new InputElement[]
				{
					// Lloyd: watch out! trap! offset and slot are swapped between C++ and C#
					new InputElement ( "POSITION", 0, Format.R32G32B32_Float,  0, 0, InputClassification.PerVertexData, 0 ),
					new InputElement ( "NORMAL",   0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0 ),
					new InputElement ( "TEXCOORD", 0, Format.R32G32_Float,    24, 0, InputClassification.PerVertexData, 0 ),
				};
				var PassDesc = g_pRenderTileBump.GetPassByIndex(0).Description;
				var sbcsign = dg.Add(PassDesc.Signature);
				Set(ref g_pLayoutObject, new InputLayout(Device, sbcsign, layoutObject));

				// Setup the camera's view parameters
				Camera.SetViewParams(new Vector3(0.0f, 2.0f, 0.0f), new Vector3(0.0f, 2.0f, 1.0f));

				// Create the async loader
				g_pAsyncLoader = new AsyncLoader(g_NumProcessingThreads);

				// Create the texture reuse cache
				g_pResourceReuseCache = new ResourceReuseCache(Device);

				// Load resources if they haven't been already (coming from a device recreate)
				if( APP_STATE.RENDER_SCENE == g_AppState )
					LoadStartupResources(RenderTime);
			}
		}

		#endregion

		int W = 1, H = 1;
		public override void Reset(int w, int h)
		{
			base.Reset(w, h);

			W = w;
			H = h;
			g_fAspectRatio = w / (float)h;
			Camera.SetProjParams(g_fFOV.DEG2RAD(), g_fAspectRatio, 0.1f, g_fVisibleRadius);
		}

		public override void RenderScene(DrawEventArgs args)
		{
			if (APP_STATE.RENDER_SCENE == g_AppState)
			{
				OnFrameMove(args.TotalTime, args.DeltaTime, g_pAsyncLoader);
				OnD3D10FrameRender(args.TotalTime, args.DeltaTime, g_pAsyncLoader);
				RenderText();
			}
		}

		static float RPercent() { return Terrain.RPercent(); }

		public const string g_strFile = "ContentPackedFile.packedfile";
		const long g_PackedFileSize = 3408789504u;
		public static string GetPackedFilePath()
		{
			string dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			return Path.Combine(dir, "ContentStreaming");
		}

		#region SmartLoadMesh()

		//--------------------------------------------------------------------------------------
		// Load a mesh using one of three techniques
		//--------------------------------------------------------------------------------------
		void SmartLoadMesh(LEVEL_ITEM pItem)
		{
			AsyncLoader pContext = null;
			if (LOAD_TYPE.MULTITHREAD == g_LoadType)
				pContext = g_pAsyncLoader;

			Stream pData;
			pData = g_PackFile.GetPackedFile(pItem.szVBName);
			if (pData == null)
				return;

			var bufferDesc = new BufferDescription
			{
				SizeInBytes = (int)pData.Length,
				BindFlags = BindFlags.VertexBuffer,
			};
			CreateVertexBuffer10(b => pItem.VB.pVB10 = b, bufferDesc, pData, pContext);

			pData = g_PackFile.GetPackedFile(pItem.szIBName);
			if (pData == null)
				return;

			bufferDesc.SizeInBytes = (int)pData.Length;
			bufferDesc.BindFlags = BindFlags.IndexBuffer;
			CreateIndexBuffer10(b => pItem.IB.pIB10 = b, bufferDesc, pData, pContext);

			CreateTextureFromFile10(pItem.szDiffuseName, rv => pItem.Diffuse.pRV10 = rv, pContext);
			CreateTextureFromFile10(pItem.szNormalName, rv => pItem.Normal.pRV10 = rv, pContext);
		}

		void CreateVertexBuffer10(Action<Buffer> setBuffer, BufferDescription BufferDesc, Stream pData, AsyncLoader pAsyncLoader)
		{
			var pLoader = new VertexBufferLoader();
			var pProcessor = new VertexBufferProcessor(
				setBuffer,
				Device,
				BufferDesc,
				pData,
				g_pResourceReuseCache);
			ProcessLP(pLoader, pProcessor, pAsyncLoader);
		}

		void CreateIndexBuffer10(Action<Buffer> setBuffer, BufferDescription BufferDesc, Stream pData, AsyncLoader pAsyncLoader)
		{
			var pLoader = new IndexBufferLoader();
			var pProcessor = new IndexBufferProcessor(setBuffer, Device, BufferDesc, pData, g_pResourceReuseCache);
			ProcessLP(pLoader, pProcessor, pAsyncLoader);
		}

		void CreateTextureFromFile10(string szFileName, Action<ShaderResourceView> setRV, AsyncLoader pAsyncLoader)
		{
			var pLoader = new TextureLoader(szFileName, g_PackFile);
			var pProcessor = new TextureProcessor(setRV, Device, g_pResourceReuseCache, g_SkipMips);
			ProcessLP(pLoader, pProcessor, pAsyncLoader);
		}

		void ProcessLP(IDataLoader loader, IDataProcessor processor, AsyncLoader pAsyncLoader)
		{
			if (pAsyncLoader == null || LOAD_TYPE.SINGLETHREAD == LoadType)
			{
				try
				{
					loader.Load();
					var pLocalData = loader.Decompress();
					processor.Process(pLocalData);
					processor.LockDeviceObject();
					try
					{
						processor.CopyToResource();
					}
					finally
					{
						processor.UnLockDeviceObject();
					}
				}
				finally
				{
					processor.Dispose();
					loader.Dispose();
				}
			}
			else
			{
				pAsyncLoader.AddWorkItem(loader, processor);
			}
		}

		#endregion

		#region LoadStartupResources()

		//--------------------------------------------------------------------------------------
		// Load the resources necessary at the beginning of a level
		//--------------------------------------------------------------------------------------
		public void LoadStartupResources(TimeSpan fTime)
		{
			if( g_bStartupResourcesLoaded )
				return;

			//WCHAR strPath[MAX_PATH] = {0};
			//WCHAR strDirectory[MAX_PATH] = {0};
			string strDirectory = GetPackedFilePath();
			if (!Directory.Exists(strDirectory))
			{
				var di = Directory.CreateDirectory(strDirectory);
				if (!di.Exists)
				{
					MessageBox.Show("There was an error creating the pack file.  ContentStreaming will now exit.", "Error");
					Environment.Exit(0);
				}
			}

			// Find the pack file
			int SqrtNumTiles = 20;
			int SidesPerTile = 50;
			float fWorldScale = 6667.0f;
			float fHeightScale = 300.0f;
			string strPath = Path.Combine(strDirectory, g_strFile);
			bool bCreatePackedFile = false;
			if(!File.Exists( strPath ))
			{
				bCreatePackedFile = true;
			}
			else
			{
				var fi = new FileInfo(strPath);
				long Size = fi.Length;
				if(Size != g_PackedFileSize)
					bCreatePackedFile = true;
			}

			if( bCreatePackedFile )
			{
				var di = new DriveInfo(strPath.Substring(0, 1));

				// Check for necessary disk space
				if( di.AvailableFreeSpace < g_PackedFileSize )
				{
					MessageBox.Show(string.Format("There is not enough free disk space to create the file {0} (needs {1} bytes).  ContentStreaming will now exit.", strPath, g_PackedFileSize), "Error");
					Environment.Exit(0);
					return;
				}

				if( MessageBoxResult.No == 
					MessageBox.Show("This is the first time ContentStreaming has been run.\r\nThe sample will need to create a 3.3 gigabyte pack file in order to demonstrate loading assets from a packed file format\r\n. The application will be freezed.\r\n\r\n Do you wish to continue?", "Warning", MessageBoxButton.YesNo) )
				{
					Environment.Exit(0);
					return;
				}

				g_PackFile.CreatePackedFile(Device, strPath, SqrtNumTiles, SidesPerTile, fWorldScale, fHeightScale);
			}

			// Create a pseudo terrain
			var str = "ContentStream\\terrain1.bmp";
			if(!File.Exists(str))
				return;
			g_Terrain.LoadTerrain(str, SqrtNumTiles, SidesPerTile, fWorldScale, fHeightScale, false);

			g_PackFile.LoadPackedFile(strPath, g_LevelItemArray);

			// This ensure that the loading radius can cover at most 9 chunks
			int maxChunks = g_PackFile.MaxChunksInVA;
			g_PackFile.MaxChunksMapped = maxChunks;

			LoadingRadius = g_PackFile.LoadingRadius;
			VisibleRadius = g_fLoadingRadius;
			Camera.SetProjParams(g_fFOV.DEG2RAD(), g_fAspectRatio, 0.1f, VisibleRadius);

			// Determine our available texture memory and try to skip mip levels to fit into it
			g_pResourceReuseCache.MaxManagedMemory = g_AvailableVideoMem;
			g_SkipMips = 0;
			long FullUsage = g_PackFile.VideoMemoryUsageAtFullMips;
			while( FullUsage > g_AvailableVideoMem )
			{	
				FullUsage = FullUsage >> 2;
				g_SkipMips ++;
			}

			// Tell the resource cache to create all resources
			g_pResourceReuseCache.OnDestroy();
			g_pResourceReuseCache.DontCreateResources = false;

			g_bStartupResourcesLoaded = true;
		}

		#endregion
        
		#region OnFrameMove()

		void OnFrameMove(TimeSpan fTime, TimeSpan fElapsedTime, AsyncLoader pUserContext)
		{
			if(APP_STATE.RENDER_SCENE == g_AppState)
			{
				// Update the camera's position based on user input 
				Camera.FrameMove(fElapsedTime);

				// Keep us close to the terrain
				Vector3 vEye = Camera.Position;
				Vector3 vAt = Camera.LookAt;
				Vector3 vDir = vAt - vEye;
				float fHeight = g_Terrain.GetHeightOnMap(vEye);
				vEye.Y = fHeight + g_fViewHeight;
				vAt = vEye + vDir;
				//CurrentCamera.SetViewParams(vEye, vAt);
				Camera.Position = vEye;
				Camera.LookAt = vAt;

				// Find visible sets
				CalculateVisibleItems( vEye, g_fVisibleRadius, g_fLoadingRadius );

				// Ensure resources within a certain radius are loaded
				EnsureResourcesLoaded(g_fVisibleRadius, g_fLoadingRadius);

				// Never unload when using WDDM paging
				if( !g_bUseWDDMPaging )
					EnsureUnusedResourcesUnloaded(fTime);

				CheckForLoadDone();
			}
		}

		//--------------------------------------------------------------------------------------
		// Calculate our visible and potentially visible items
		//--------------------------------------------------------------------------------------
		void CalculateVisibleItems(Vector3 vEye, float fVisRadius, float fLoadRadius)
		{
			g_VisibleItemArray.Clear();
			g_LoadedItemArray.Clear();

			// setup cull planes
			Vector3 vLeftNormal;
			Vector3 vRightNormal;
			float leftD;
			float rightD;
			GetCameraCullPlanes(out vLeftNormal, out vRightNormal, out leftD, out rightD);
			float fTileSize = g_PackFile.TileSideSize;

			for (int i = 0; i < g_LevelItemArray.Count; i++)
			{
				LEVEL_ITEM pItem = g_LevelItemArray[i];

				// Default is not in loading radius
				pItem.bInLoadRadius = false;

				Vector3 vDelta = vEye - pItem.vCenter;
				float len2 = vDelta.LengthSquared();
				if (len2 < fVisRadius * fVisRadius)
				{
					pItem.bInFrustum = false;

					if (len2 < fTileSize * fTileSize ||
						(Vector3.Dot(pItem.vCenter, vLeftNormal) < leftD + fTileSize &&
							Vector3.Dot(pItem.vCenter, vRightNormal) < rightD + fTileSize)
						)
					{
						pItem.bInFrustum = true;
					}

					g_VisibleItemArray.Add(pItem);
				}
				if (len2 < fLoadRadius * fLoadRadius)
				{
					pItem.bInLoadRadius = true;
					g_LoadedItemArray.Add(pItem);
				}
			}
		}

		//--------------------------------------------------------------------------------------
		// GetCameraCullPlanes
		//--------------------------------------------------------------------------------------
		void GetCameraCullPlanes(out Vector3 p1Normal, out Vector3 p2Normal, out float p1D, out float p2D)
		{
			Vector3 vEye = Camera.Position;
			Vector3 vDir = Camera.LookAt - vEye;
			vDir.Normalize();

			// setup clip planes
			Vector3 vLeftNormal;
			Vector3 vRightNormal;
			Matrix mRotLeft;
			Matrix mRotRight;
			float fAngle = (float)Math.PI / 2.0f + (g_fFOV.DEG2RAD() / 2.0f) * 1.3333f;
			mRotLeft = Matrix.RotationY(-fAngle);
			mRotRight = Matrix.RotationY(fAngle);
			vLeftNormal = mRotLeft.TransformNormal(vDir);
			vRightNormal = mRotRight.TransformNormal(vDir);
			
			p1D =  Vector3.Dot(vLeftNormal, vEye);
			p2D = Vector3.Dot(vRightNormal, vEye);
			p1Normal = vLeftNormal;
			p2Normal = vRightNormal;
		}

		//--------------------------------------------------------------------------------------
		// Ensure resources within a certain radius are loaded
		//--------------------------------------------------------------------------------------
		int EnsureResourcesLoaded(float fVisRadius, float fLoadRadius)
		{
			int NumToLoad = 0;
			for (int i = 0; i < g_LoadedItemArray.Count; i++)
			{
				LEVEL_ITEM pItem = g_LoadedItemArray[i];

				if (!pItem.bLoaded && !pItem.bLoading)
				{
					pItem.bLoading = true;
					NumToLoad++;
					SmartLoadMesh(pItem);
				}
			}

			return NumToLoad;
		}

		//--------------------------------------------------------------------------------------
		void FreeUpMeshResources(LEVEL_ITEM pItem)
		{
			g_pResourceReuseCache.UnuseDeviceTexture10(pItem.Diffuse.pRV10);
			g_pResourceReuseCache.UnuseDeviceTexture10(pItem.Normal.pRV10);
			g_pResourceReuseCache.UnuseDeviceVB10(pItem.VB.pVB10);
			g_pResourceReuseCache.UnuseDeviceIB10(pItem.IB.pIB10);
		}

		//--------------------------------------------------------------------------------------
		// Ensure resources that are unused are unloaded
		//--------------------------------------------------------------------------------------
		int EnsureUnusedResourcesUnloaded(TimeSpan fTime)
		{
			int NumToUnload = 0;

			for (int i = 0; i < g_LevelItemArray.Count; i++)
			{
				LEVEL_ITEM pItem = g_LevelItemArray[i];

				if (pItem.bLoaded && !pItem.bInLoadRadius)
				{
					// Unload the mesh textures from the texture cache
					FreeUpMeshResources(pItem);
					pItem.bLoading = false;
					pItem.bLoaded = false;
					pItem.bHasBeenRenderedDiffuse = false;
					pItem.bHasBeenRenderedNormal = false;
				}
			}

			return NumToUnload;
		}


		//--------------------------------------------------------------------------------------
		// If an item is done loading, label it as loaded
		//--------------------------------------------------------------------------------------
		void CheckForLoadDone()
		{
			for (int i = 0; i < g_LevelItemArray.Count; i++)
			{
				LEVEL_ITEM pItem = g_LevelItemArray[i];

				if (pItem.bLoading)
				{
					if (pItem.VB.pVB10 != null &&
						pItem.IB.pIB10 != null)
					{
						pItem.bLoading = false;
						pItem.bLoaded = true;

						pItem.CurrentCountdownDiff = 5;
						pItem.CurrentCountdownNorm = 10;
					}
				}
			}
		}

		#endregion

		#region OnD3D10FrameRender()

		//--------------------------------------------------------------------------------------
		// Render the scene using the D3D10 device
		//--------------------------------------------------------------------------------------
		void OnD3D10FrameRender(TimeSpan fTime, TimeSpan fElapsedTime, AsyncLoader pUserContext)
		{
			Device.ClearRenderTargetView(RenderTargetView, new Color4(1.0f, 0.627f, 0.627f, 0.980f));
			Device.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

			// Render the scene
			if( APP_STATE.RENDER_SCENE == g_AppState )
				RenderScene(fTime, fElapsedTime, pUserContext);

			// Load in up to g_NumResourceToLoadPerFrame resources at the end of every frame
			if( LOAD_TYPE.MULTITHREAD == g_LoadType && APP_STATE.RENDER_SCENE == g_AppState )
			{
				int NumResToProcess = g_NumResourceToLoadPerFrame;
				g_pAsyncLoader.ProcessDeviceWorkItems( NumResToProcess );
			}
		}

		// Render the scene using the programmable pipeline
		//--------------------------------------------------------------------------------------
		static int iFrameNum = 0;
		void RenderScene(TimeSpan fTime, TimeSpan fElapsedTime, AsyncLoader pUserContext)
		{
			Matrix mWorld;
			Matrix mView;
			Matrix mProj;
			Matrix mWorldViewProjection;

			// Get the projection & view matrix from the camera class
			mProj = Camera.Projection;
			mView = Camera.View;

			// Set the eye vector
			Vector3 vEyePt = Camera.Position;
			Vector4 vEyePt4;
			vEyePt4.X = vEyePt.X;
			vEyePt4.Y = vEyePt.Y;
			vEyePt4.Z = vEyePt.Z;
			vEyePt4.W = 0;
			g_pEyePt.Set(vEyePt4);

			int NewTextureUploadsToVidMem = 0;
			if (iFrameNum % g_UploadToVRamEveryNthFrame > 0)
				NewTextureUploadsToVidMem = g_NumResourceToLoadPerFrame;
			iFrameNum++;

			// Render the level
			Device.InputAssembler.InputLayout = (g_pLayoutObject);
			Device.InputAssembler.PrimitiveTopology = (PrimitiveTopology.TriangleStrip);
			for (int i = 0; i < g_VisibleItemArray.Count; i++)
			{
				LEVEL_ITEM pItem = g_VisibleItemArray[i];

				mWorld = Matrix.Identity;
				mWorldViewProjection = mWorld * mView * mProj;

				g_pmWorldViewProjection.SetMatrix(mWorldViewProjection);
				g_pmWorld.SetMatrix(mWorld);

				if (!pItem.bLoaded)
					continue;

				int Stride = Marshal.SizeOf(typeof(TERRAIN_VERTEX));
				int Offset = 0;
				Device.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(pItem.VB.pVB10, Stride, Offset));
				Device.InputAssembler.SetIndexBuffer(pItem.IB.pIB10, Format.R16_UInt, 0);

				bool bDiff = pItem.Diffuse.pRV10 != null ? true : false;
				if (bDiff && pItem.CurrentCountdownDiff > 0)
				{
					bDiff = false;
					pItem.CurrentCountdownDiff--;
				}
				bool bNorm = pItem.Normal.pRV10 != null ? true : false;
				if (bNorm && pItem.CurrentCountdownNorm > 0)
				{
					bNorm = false;
					pItem.CurrentCountdownNorm--;
				}

				bool bCanRenderDiff = bDiff;
				bool bCanRenderNorm = bDiff && bNorm && pItem.bHasBeenRenderedDiffuse;
				if (bDiff && !pItem.bHasBeenRenderedDiffuse)
				{
					if (NewTextureUploadsToVidMem >= g_NumResourceToLoadPerFrame)
						bCanRenderDiff = false;
					else
						NewTextureUploadsToVidMem++;
				}
				if (bCanRenderDiff && bNorm && !pItem.bHasBeenRenderedNormal)
				{
					if (NewTextureUploadsToVidMem >= g_NumResourceToLoadPerFrame)
						bCanRenderNorm = false;
					else
						NewTextureUploadsToVidMem++;
				}

				if (!bCanRenderDiff && !bCanRenderNorm)
					continue;

				// Render the scene with this technique 
				EffectTechnique pTechnique = null;
				if (g_bWireframe)
				{
					pTechnique = g_pRenderTileWire;
					g_ptxDiffuse.SetResource(pItem.Diffuse.pRV10);
				}
				else if (bCanRenderNorm)
				{
					pItem.bHasBeenRenderedNormal = true;
					pTechnique = g_pRenderTileBump;
					g_ptxDiffuse.SetResource(pItem.Diffuse.pRV10);
					g_ptxNormal.SetResource(pItem.Normal.pRV10);
				}
				else if (bCanRenderDiff)
				{
					pItem.bHasBeenRenderedDiffuse = true;
					pTechnique = g_pRenderTileDiff;
					g_ptxDiffuse.SetResource(pItem.Diffuse.pRV10);
				}

				// Apply the technique contained in the effect 
				var Desc = pTechnique.Description;
				for (int iPass = 0; iPass < Desc.PassCount; iPass++)
				{
					pTechnique.GetPassByIndex(iPass).Apply();
					Device.DrawIndexed(g_Terrain.NumIndices, 0, 0);
				}
			}
		}

		void RenderText()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("D3D10, {0}x{1}, {2}\r\n", 
				RenderTarget.Description.Width, 
				RenderTarget.Description.Height,
				RenderTarget.Description.Format);

			using (var xgidev = Device.QueryInterface<SharpDX.DXGI.Device>())
			using (var adap = xgidev.Adapter)
			{
				string desc = adap.Description.Description;
				int c0 = desc.IndexOf((char)0);
				if (c0 > -1)
					desc = desc.Substring(0, c0);
				sb.AppendFormat("HARDWARE: {0}\r\n", desc);
			}
			sb.AppendFormat("InitialLevel Loaded in {0:0.00} seconds\r\n", g_fInitialLoadTime);
			sb.AppendFormat("Models in Use: {0}\r\n", g_NumModelsInUse );
			sb.AppendLine();
			if( g_pResourceReuseCache != null )
			{
				int NumTextures = g_pResourceReuseCache.GetNumTextures();
				int NumUsed = 0;
				long EstimatedManagedMemory = g_pResourceReuseCache.UsedManagedMemory;

				for( int i = 0; i < NumTextures; i++ )
				{
					DEVICE_TEXTURE tex = g_pResourceReuseCache.GetTexture( i );
					if( tex.bInUse )
						NumUsed ++;
				}

				sb.AppendFormat("Estimated video memory used: {0} (mb) of {1} (mb)\r\n", 
								 ( EstimatedManagedMemory / ( 1024 * 1024 ) ), 
								 ( g_AvailableVideoMem / ( 1024 * 1024 ) ) );
				sb.AppendLine();

				sb.AppendFormat("TextureCache: Total textures: {0}\r\n", NumTextures );
				sb.AppendFormat("TextureCache: Used textures: {0}\r\n", NumUsed );

				// LOD list
				int TextureSize = ( int )Math.Pow( 2.0f, 11.0f - g_SkipMips );
				sb.AppendFormat("Texture LOD: {0} x {1}\r\n", TextureSize, TextureSize );


				int NumVBs = g_pResourceReuseCache.GetNumVBs();
				int NumIBs = g_pResourceReuseCache.GetNumIBs();
				int NumUsedVBs = 0;
				int NumUsedIBs = 0;

				for( int i = 0; i < NumVBs; i++ )
				{
					DEVICE_VERTEX_BUFFER vb = g_pResourceReuseCache.GetVB( i );
					if( vb.bInUse )
						NumUsedVBs ++;
				}
				for( int i = 0; i < NumIBs; i++ )
				{
					DEVICE_INDEX_BUFFER ib = g_pResourceReuseCache.GetIB( i );
					if( ib.bInUse )
						NumUsedIBs ++;
				}

				sb.AppendFormat("BufferCache: Total buffers: {0}\r\n", NumVBs + NumIBs );
				sb.AppendFormat("    VBs: {0}\r\n", NumVBs );
				sb.AppendFormat("    IBs: {0}\r\n", NumIBs );
				sb.AppendFormat("BufferCache: Used buffers: {0}\r\n", NumUsedVBs + NumUsedIBs );
				sb.AppendFormat("    VBs: {0}\r\n", NumUsedVBs );
				sb.AppendFormat("    IBs: {0}\r\n", NumUsedIBs );
			}

			DisplayInfo = sb.ToString();
		}


		#endregion

		#region DestroyAllMeshes(), ClearD3D10State()

		//--------------------------------------------------------------------------------------
		void OnDestroyDevice()
		{
			g_bStartupResourcesLoaded = false;

			// Wait for everything to load
			if (LOAD_TYPE.MULTITHREAD == g_LoadType)
				g_pAsyncLoader.WaitForAllItems();
			g_pAsyncLoader.Dispose();
			g_pAsyncLoader = null;

			g_pResourceReuseCache.OnDestroy();
			g_pResourceReuseCache = null;

			// Destroy the level-item array
			g_LevelItemArray.Clear();
			g_VisibleItemArray.Clear();
			g_LoadedItemArray.Clear();

			g_PackFile.UnloadPackedFile();

			Set(ref device, null);

			// recreate now
			device = DeviceUtil.Create10(DeviceCreationFlags.BgraSupport);
			OnD3D10CreateDevice();
			Reset(W, H);
		}

		#endregion

		// UI properties => OnPropertyChanged

		#region ViewHeight

		public float ViewHeight
		{
			get { return g_fViewHeight; }
			set
			{
				if (value == g_fViewHeight)
					return;
				g_fViewHeight = value;
				OnPropertyChanged("ViewHeight");
			}
		}

		#endregion

		#region LoadingRadius

		public float LoadingRadius
		{
			get { return g_fLoadingRadius; }
			set
			{
				if (value == g_fLoadingRadius)
					return;
				g_fLoadingRadius = value;
				OnPropertyChanged("LoadingRadius");
			}
		}

		#endregion

		#region VisibleRadius

		public float VisibleRadius
		{
			get { return g_fVisibleRadius; }
			set
			{
				if (value == g_fVisibleRadius)
					return;
				g_fVisibleRadius = value;
				OnPropertyChanged("VisibleRadius");

				//??
				//g_fLoadingRadius = value;
			}
		}

		#endregion

		#region LoadType

		public LOAD_TYPE LoadType
		{
			get { return g_LoadType; }
			set
			{
				if (value == g_LoadType)
					return;
				g_LoadType = value;
				OnPropertyChanged("LoadType");
			}
		}

		#endregion

		#region AppState

		public APP_STATE AppState
		{
			get { return g_AppState; }
			set
			{
				if (value == g_AppState)
					return;

				switch (value)
				{
					case APP_STATE.STARTUP:
						// change the state first
						OnDestroyDevice();
						break;
					case APP_STATE.RENDER_SCENE:
						// load the resource before changing the app state!
						LoadStartupResources(RenderTime);
						break;
				}
				g_AppState = value;
				OnPropertyChanged("AppState");
			}
		}

		#endregion

		#region Wireframe

		public bool Wireframe
		{
			get { return g_bWireframe; }
			set
			{
				if (value == g_bWireframe)
					return;
				g_bWireframe = value;
				OnPropertyChanged("Wireframe");
			}
		}

		#endregion

		#region UploadToVRamEveryNthFrame

		public int UploadToVRamEveryNthFrame
		{
			get { return g_UploadToVRamEveryNthFrame; }
			set
			{
				if (value == g_UploadToVRamEveryNthFrame)
					return;
				g_UploadToVRamEveryNthFrame = value;
				OnPropertyChanged("UploadToVRamEveryNthFrame");
			}
		}

		#endregion

		#region NumResourceToLoadPerFrame

		public int NumResourceToLoadPerFrame
		{
			get { return g_NumResourceToLoadPerFrame; }
			set
			{
				if (value == g_NumResourceToLoadPerFrame)
					return;
				g_NumResourceToLoadPerFrame = value;
				OnPropertyChanged("NumResourceToLoadPerFrame");
			}
		}

		#endregion

		#region DisplayInfo

		public string DisplayInfo
		{
			get { return mDisplayInfo; }
			set
			{
				if (value == mDisplayInfo)
					return;
				mDisplayInfo = value;
				OnPropertyChanged("DisplayInfo");
			}
		}
		string mDisplayInfo;

		#endregion
	}
}
