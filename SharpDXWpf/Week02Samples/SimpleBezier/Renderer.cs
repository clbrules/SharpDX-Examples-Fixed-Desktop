using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpDX.WPF;
using SharpDX.Direct3D;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Runtime.InteropServices;
using SharpDX;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace Week02Samples.SimpleBezier
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct CB_PER_FRAME_CONSTANTS
	{
		public Matrix mViewProjection;
		public Vector3 vCameraPosWorld;
		public float fTessellationFactor;
	}

	public enum PartitionMode
	{
		Integer,
		FractionalEven,
		FractionOdd,
	}

	public class Renderer : D3D11
	{
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			// NOTE: SharpDX 1.3 requires explicit Dispose() of everything
			Set(ref g_pVertexShader, null);
			Set(ref g_pHullShaderInteger, null);
			Set(ref g_pHullShaderFracEven, null);
			Set(ref g_pHullShaderFracOdd, null);
			Set(ref g_pDomainShader, null);
			Set(ref g_pPixelShader, null);
			Set(ref g_pSolidColorPS, null);

			Set(ref g_pPatchLayout, null);
			Set(ref g_pcbPerFrame, null);

			Set(ref g_pRasterizerStateSolid, null);
			Set(ref g_pRasterizerStateWireframe, null);

			Set(ref g_pControlPointVB, null);

			Set(ref depthStencil, null);
			Set(ref depthStencilView, null);
		}

		VertexShader g_pVertexShader;
		HullShader g_pHullShaderInteger;
		HullShader g_pHullShaderFracEven;
		HullShader g_pHullShaderFracOdd;
		DomainShader g_pDomainShader;
		PixelShader g_pPixelShader;
		PixelShader g_pSolidColorPS;

		InputLayout g_pPatchLayout;

		ConstantBuffer<CB_PER_FRAME_CONSTANTS> g_pcbPerFrame;

		RasterizerState g_pRasterizerStateSolid;
		RasterizerState g_pRasterizerStateWireframe;

		Buffer g_pControlPointVB;

		public Renderer()
			: base(SharpDX.Direct3D.FeatureLevel.Level_11_0)
		{
			using (var dg = new DisposeGroup())
			{
				var sFlags = ShaderFlags.EnableStrictness;
#if DEBUG
				sFlags |= ShaderFlags.Debug;
#endif

				// This macro is used to compile the hull shader with different partition modes
				// Please see the partitioning mode attribute for the hull shader for more information
				var integerPartitioning = new[] { new ShaderMacro("BEZIER_HS_PARTITION", "\"integer\"") };
				var fracEvenPartitioning = new[] { new ShaderMacro("BEZIER_HS_PARTITION", "\"fractional_even\"") };
				var fracOddPartitioning = new[] { new ShaderMacro("BEZIER_HS_PARTITION", "\"fractional_odd\"") };

				var pBlobVS = dg.Add(ShaderBytecode.CompileFromFile("SimpleBezier\\SimpleBezier11.hlsl", "BezierVS", "vs_5_0", sFlags, EffectFlags.None, null, null));
				var pBlobHSInt = dg.Add(ShaderBytecode.CompileFromFile("SimpleBezier\\SimpleBezier11.hlsl", "BezierHS", "hs_5_0", sFlags, EffectFlags.None, integerPartitioning, null));
				var pBlobHSFracEven = dg.Add(ShaderBytecode.CompileFromFile("SimpleBezier\\SimpleBezier11.hlsl", "BezierHS", "hs_5_0", sFlags, EffectFlags.None, fracEvenPartitioning, null));
				var pBlobHSFracOdd = dg.Add(ShaderBytecode.CompileFromFile("SimpleBezier\\SimpleBezier11.hlsl", "BezierHS", "hs_5_0", sFlags, EffectFlags.None, fracOddPartitioning, null));
				var pBlobDS = dg.Add(ShaderBytecode.CompileFromFile("SimpleBezier\\SimpleBezier11.hlsl", "BezierDS", "ds_5_0", sFlags, EffectFlags.None, null, null));
				var pBlobPS = dg.Add(ShaderBytecode.CompileFromFile("SimpleBezier\\SimpleBezier11.hlsl", "BezierPS", "ps_5_0", sFlags, EffectFlags.None, null, null));
				var pBlobPSSolid = dg.Add(ShaderBytecode.CompileFromFile("SimpleBezier\\SimpleBezier11.hlsl", "SolidColorPS", "ps_5_0", sFlags, EffectFlags.None, null, null));

				// create shaders
				g_pVertexShader = new VertexShader(Device, pBlobVS);
				g_pHullShaderInteger = new HullShader(Device, pBlobHSInt);
				g_pHullShaderFracEven = new HullShader(Device, pBlobHSFracEven);
				g_pHullShaderFracOdd = new HullShader(Device, pBlobHSFracOdd);
				g_pDomainShader = new DomainShader(Device, pBlobDS);
				g_pPixelShader = new PixelShader(Device, pBlobPS);
				g_pSolidColorPS = new PixelShader(Device, pBlobPSSolid);

				// Create our vertex input layout - this matches the BEZIER_CONTROL_POINT structure
				var inputsignature = dg.Add(ShaderSignature.GetInputSignature(pBlobVS));
				g_pPatchLayout = new InputLayout(Device, inputsignature, new[]{
				    new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerInstanceData, 0),
				});

				// Create constant buffers
				g_pcbPerFrame = new ConstantBuffer<CB_PER_FRAME_CONSTANTS>(Device);

				// Create solid and wireframe rasterizer state objects
				var RasterDesc = new RasterizerStateDescription
				{
					FillMode = FillMode.Solid,
					CullMode = CullMode.None,
					IsDepthClipEnabled = true,
				};
				g_pRasterizerStateSolid = new RasterizerState(Device, RasterDesc);

				RasterDesc.FillMode = FillMode.Wireframe;
				g_pRasterizerStateWireframe = new RasterizerState(Device, RasterDesc);

				var vbDesc = new BufferDescription
				{
					Usage = ResourceUsage.Default,
					BindFlags = BindFlags.VertexBuffer,
					SizeInBytes = Vector3.SizeInBytes * MobiusStrip.Points.Length,
					StructureByteStride = Vector3.SizeInBytes,
				};
                var dataStream = new DataStream(MobiusStrip.Points.Length, false, false);
                var vbInitData = dg.Add(dataStream);
				g_pControlPointVB = new Buffer(Device, vbInitData, vbDesc);
			}

			Camera = new ModelViewerCamera();
			Camera.SetProjParams((float)Math.PI / 4, 1, 0.1f, 20.0f);
			Camera.SetViewParams(new Vector3(1.0f, 1.5f, -3.5f), new Vector3(0.0f, 0.0f, 1.0f));
		}
        

		public override void RenderScene(DrawEventArgs args)
		{
			// WVP
			var mWorld = ((ModelViewerCamera)Camera).World;
			var mView = Camera.View;
			var pPos = mWorld.TranslationVector;
			pPos.X = 0;
			pPos.Y = 0;
			mWorld.TranslationVector = pPos;

			g_pcbPerFrame.Value = new CB_PER_FRAME_CONSTANTS
			{
				fTessellationFactor = PatchDivision,
				mViewProjection = Matrix.Transpose(mWorld * mView * Camera.Projection),
				vCameraPosWorld = mWorld.TranslationVector,
			};

			// Clear the render target and depth stencil
			Device.ImmediateContext.ClearRenderTargetView(RenderTargetView, new Color4(0.8f, 0.05f, 0.05f, 0.05f));
			Device.ImmediateContext.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

			// Set state for solid rendering
			Device.ImmediateContext.Rasterizer.State = g_pRasterizerStateSolid;

			// Render the meshes
			// Bind all of the CBs
			Device.ImmediateContext.VertexShader.SetConstantBuffer(0, g_pcbPerFrame.Buffer);
			Device.ImmediateContext.HullShader.SetConstantBuffer(0, g_pcbPerFrame.Buffer);
			Device.ImmediateContext.DomainShader.SetConstantBuffer(0, g_pcbPerFrame.Buffer);
			Device.ImmediateContext.PixelShader.SetConstantBuffer(0, g_pcbPerFrame.Buffer);

			// Set the shaders
			Device.ImmediateContext.VertexShader.Set(g_pVertexShader);

			// For this sample, choose either the "integer", "fractional_even",
			// or "fractional_odd" hull shader
			switch (PMode)
			{
				case PartitionMode.Integer:
					Device.ImmediateContext.HullShader.Set(g_pHullShaderInteger);
					break;
				case PartitionMode.FractionalEven:
					Device.ImmediateContext.HullShader.Set(g_pHullShaderFracEven);
					break;
				case PartitionMode.FractionOdd:
					Device.ImmediateContext.HullShader.Set(g_pHullShaderFracOdd);
					break;
			}

			Device.ImmediateContext.DomainShader.Set(g_pDomainShader);
			Device.ImmediateContext.GeometryShader.Set(null);
			Device.ImmediateContext.PixelShader.Set(g_pPixelShader);

			// Optionally draw the wireframe
			if(ToggleWire)
			{
				Device.ImmediateContext.PixelShader.Set(g_pSolidColorPS);
				Device.ImmediateContext.Rasterizer.State = g_pRasterizerStateWireframe; 
			}

			// Set the input assembler
			// This sample uses patches with 16 control points each
			// Although the Mobius strip only needs to use a vertex buffer,
			// you can use an index buffer as well by calling IASetIndexBuffer().
			Device.ImmediateContext.InputAssembler.InputLayout = (g_pPatchLayout);
			Device.ImmediateContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(g_pControlPointVB, Vector3.SizeInBytes, 0));
			Device.ImmediateContext.InputAssembler.PrimitiveTopology = (PrimitiveTopology.PatchListWith16ControlPoints);

			// Draw the mesh
			Device.ImmediateContext.Draw(MobiusStrip.Points.Length, 0);

			Device.ImmediateContext.Rasterizer.State = g_pRasterizerStateSolid;

			//RenderText();
		}

		#region ToggleWire

		public bool ToggleWire
		{
			get { return mToggleWire; }
			set
			{
				if (value == mToggleWire)
					return;
				mToggleWire = value;
				OnPropertyChanged("ToggleWire");
			}
		}
		bool mToggleWire = false;

		#endregion

		#region PMode

		public PartitionMode[] PModes { get { return new[] { PartitionMode.Integer, PartitionMode.FractionalEven, PartitionMode.FractionOdd }; } }
		public PartitionMode PMode
		{
			get { return mPMode; }
			set
			{
				if (value == mPMode)
					return;
				mPMode = value;
				OnPropertyChanged("PMode");
			}
		}
		PartitionMode mPMode = PartitionMode.Integer;

		#endregion

		#region PatchDivision

		public float PatchDivision
		{
			get { return mPatchDivision; }
			set
			{
				if (value == mPatchDivision)
					return;
				mPatchDivision = value;
				OnPropertyChanged("PatchDivision");
			}
		}
		float mPatchDivision = 8;

		#endregion
	}
}
