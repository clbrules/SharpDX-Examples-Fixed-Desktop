using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.WPF;
using Buffer = SharpDX.Direct3D11.Buffer;
using System.Runtime.InteropServices;
using System;

namespace Week01D3D11Tutorials
{
	// Tutorial 7: Texture Mapping and Constant Buffers
	// http://msdn.microsoft.com/en-us/library/ff729724(v=VS.85).aspx
	public class T7_Texture : D3D11
	{
		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		public struct SimpleVertex
		{
			public SimpleVertex(Vector3 p, Vector2 t)
			{
				Pos = p;
				Tex = t;
			}
			public Vector3 Pos;
			public Vector2 Tex;

			public const int SizeInBytes = (3 + 2) * 4;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		public struct CBChangesEveryFrame
		{
			public Matrix World;
			public Color4 MeshColor;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			// NOTE: SharpDX 1.3 requires explicit dispose of everything
			Set(ref g_pCBChangesEveryFrame, null);
			Set(ref g_pCBChangeOnResize, null);
			Set(ref cbViewPoint, null);
			Set(ref g_pTextureRV, null);
			Set(ref g_pSamplerLinear, null);
		}

		VertexShader g_pVertexShader;
		PixelShader g_pPixelShader;

		ConstantBuffer<CBChangesEveryFrame> g_pCBChangesEveryFrame;
		ConstantBuffer<Matrix> g_pCBChangeOnResize;
		ConstantBuffer<Matrix> cbViewPoint; //g_pCBNeverChanges

		ShaderResourceView g_pTextureRV;
		SamplerState g_pSamplerLinear;

		public T7_Texture()
		{
			using (var dg = new DisposeGroup())
			{
				// --- init shaders
				ShaderFlags sFlags = ShaderFlags.EnableStrictness;
#if DEBUG
				sFlags |= ShaderFlags.Debug;
#endif

				var pVSBlob = dg.Add(ShaderBytecode.CompileFromFile("T7_Texture.fx", "VS", "vs_4_0", sFlags, EffectFlags.None));
				var inputSignature = dg.Add(ShaderSignature.GetInputSignature(pVSBlob));
				Set(ref g_pVertexShader, new VertexShader(Device, pVSBlob));

				var g_pVertexLayout = dg.Add(new InputLayout(Device, inputSignature, new[]{
					new InputElement("POSITION", 0, Format.R32G32B32_Float, 0),
					new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0),
				}));
				Device.ImmediateContext.InputAssembler.InputLayout = (g_pVertexLayout);

				var pPSBlob = dg.Add(ShaderBytecode.CompileFromFile("T7_Texture.fx", "PS", "ps_4_0", sFlags, EffectFlags.None));
				Set(ref g_pPixelShader, new PixelShader(Device, pPSBlob));

				// --- init vertices
				var g_pVertexBuffer = dg.Add(DXUtils.CreateBuffer(Device, new SimpleVertex[]{
					new SimpleVertex(new Vector3( -1.0f, 1.0f, -1.0f ), new Vector2( 0.0f, 0.0f ) ),
					new SimpleVertex(new Vector3( 1.0f, 1.0f, -1.0f ), new Vector2( 1.0f, 0.0f ) ),
					new SimpleVertex(new Vector3( 1.0f, 1.0f, 1.0f ), new Vector2( 1.0f, 1.0f ) ),
					new SimpleVertex(new Vector3( -1.0f, 1.0f, 1.0f ), new Vector2( 0.0f, 1.0f ) ),

					new SimpleVertex(new Vector3( -1.0f, -1.0f, -1.0f ), new Vector2( 0.0f, 0.0f ) ),
					new SimpleVertex(new Vector3( 1.0f, -1.0f, -1.0f ), new Vector2( 1.0f, 0.0f ) ),
					new SimpleVertex(new Vector3( 1.0f, -1.0f, 1.0f ), new Vector2( 1.0f, 1.0f ) ),
					new SimpleVertex(new Vector3( -1.0f, -1.0f, 1.0f ), new Vector2( 0.0f, 1.0f ) ),

					new SimpleVertex(new Vector3( -1.0f, -1.0f, 1.0f ), new Vector2( 0.0f, 0.0f ) ),
					new SimpleVertex(new Vector3( -1.0f, -1.0f, -1.0f ), new Vector2( 1.0f, 0.0f ) ),
					new SimpleVertex(new Vector3( -1.0f, 1.0f, -1.0f ), new Vector2( 1.0f, 1.0f ) ),
					new SimpleVertex(new Vector3( -1.0f, 1.0f, 1.0f ), new Vector2( 0.0f, 1.0f ) ),

					new SimpleVertex(new Vector3( 1.0f, -1.0f, 1.0f ), new Vector2( 0.0f, 0.0f ) ),
					new SimpleVertex(new Vector3( 1.0f, -1.0f, -1.0f ), new Vector2( 1.0f, 0.0f ) ),
					new SimpleVertex(new Vector3( 1.0f, 1.0f, -1.0f ), new Vector2( 1.0f, 1.0f ) ),
					new SimpleVertex(new Vector3( 1.0f, 1.0f, 1.0f ), new Vector2( 0.0f, 1.0f ) ),

					new SimpleVertex(new Vector3( -1.0f, -1.0f, -1.0f ), new Vector2( 0.0f, 0.0f ) ),
					new SimpleVertex(new Vector3( 1.0f, -1.0f, -1.0f ), new Vector2( 1.0f, 0.0f ) ),
					new SimpleVertex(new Vector3( 1.0f, 1.0f, -1.0f ), new Vector2( 1.0f, 1.0f ) ),
					new SimpleVertex(new Vector3( -1.0f, 1.0f, -1.0f ), new Vector2( 0.0f, 1.0f ) ),

					new SimpleVertex(new Vector3( -1.0f, -1.0f, 1.0f ), new Vector2( 0.0f, 0.0f ) ),
					new SimpleVertex(new Vector3( 1.0f, -1.0f, 1.0f ), new Vector2( 1.0f, 0.0f ) ),
					new SimpleVertex(new Vector3( 1.0f, 1.0f, 1.0f ), new Vector2( 1.0f, 1.0f ) ),
					new SimpleVertex(new Vector3( -1.0f, 1.0f, 1.0f ), new Vector2( 0.0f, 1.0f ) ),
				}));
				Device.ImmediateContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(g_pVertexBuffer, SimpleVertex.SizeInBytes, 0));

				// --- init indices
				var g_pIndexBuffer = dg.Add(DXUtils.CreateBuffer(Device, new ushort[] {
					3,1,0,
					2,1,3,

					6,4,5,
					7,4,6,

					11,9,8,
					10,9,11,

					14,12,13,
					15,12,14,

					19,17,16,
					18,17,19,

					22,20,21,
					23,20,22
				}));
				Device.ImmediateContext.InputAssembler.SetIndexBuffer(g_pIndexBuffer, Format.R16_UInt, 0);

				Device.ImmediateContext.InputAssembler.PrimitiveTopology = (PrimitiveTopology.TriangleList);

				// --- create the constant buffers
				g_pCBChangesEveryFrame = new ConstantBuffer<CBChangesEveryFrame>(Device);
				g_pCBChangeOnResize = new ConstantBuffer<Matrix>(Device);
				cbViewPoint = new ConstantBuffer<Matrix>(Device);

				g_pTextureRV = ShaderResourceView.FromFile(Device, "T7_seafloor.dds");

				g_pSamplerLinear = new SamplerState(Device, new SamplerStateDescription
				{ 
					Filter = Filter.MinMagMipLinear,
					AddressU = TextureAddressMode.Wrap,
					AddressV = TextureAddressMode.Wrap,
					AddressW = TextureAddressMode.Wrap,
					ComparisonFunction = Comparison.Never,
					MinimumLod = 0,
					MaximumLod = float.MaxValue,
				});

				Camera = new ModelViewerCamera();
				Camera.SetViewParams(new Vector3(0.0f, 3.0f, -6.0f), new Vector3(0.0f, 1.0f, 0.0f));
				Camera.SetProjParams((float)Math.PI / 4, 1, 0.01f, 100.0f);
			}
		}

		public override void Reset(int w, int h)
		{
			base.Reset(w, h);
			g_pCBChangeOnResize.Value = Matrix.Transpose(Camera.Projection);
		}

		public override void RenderScene(DrawEventArgs args)
		{
			float t = (float)args.TotalTime.TotalSeconds;
			var g_World = Matrix.RotationY(t);
			g_pCBChangesEveryFrame.Value = new CBChangesEveryFrame()
			{
				World = Matrix.Transpose(g_World),
				MeshColor = new Color4(new Vector3(
						(1 + (float)Math.Sin(t)) * 0.5f,
						(1 + (float)Math.Cos(t * 3)) * 0.5f,
						(1 + (float)Math.Sin(t * 5)) * 0.5f
					), 1.0f),
			};
			cbViewPoint.Value = Matrix.Transpose(Camera.View);

			// Clear the back buffer
			// Clear the depth buffer to 1.0 (max depth)
			Device.ImmediateContext.ClearRenderTargetView(this.RenderTargetView, new Color4(1.0f, 0.3f, 0.525f, 0.8f));
			Device.ImmediateContext.ClearDepthStencilView(this.DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

			//
			// Render the cube
			//
			Device.ImmediateContext.VertexShader.Set(g_pVertexShader);
			Device.ImmediateContext.VertexShader.SetConstantBuffer(0, cbViewPoint.Buffer);
			Device.ImmediateContext.VertexShader.SetConstantBuffer(1, g_pCBChangeOnResize.Buffer);
			Device.ImmediateContext.VertexShader.SetConstantBuffer(2, g_pCBChangesEveryFrame.Buffer);
			Device.ImmediateContext.PixelShader.Set(g_pPixelShader);
			Device.ImmediateContext.PixelShader.SetConstantBuffer(2, g_pCBChangesEveryFrame.Buffer);
			Device.ImmediateContext.PixelShader.SetShaderResource(0, g_pTextureRV);
			Device.ImmediateContext.PixelShader.SetSampler(0, g_pSamplerLinear);
			Device.ImmediateContext.DrawIndexed(36, 0, 0);
		}
	}
}
