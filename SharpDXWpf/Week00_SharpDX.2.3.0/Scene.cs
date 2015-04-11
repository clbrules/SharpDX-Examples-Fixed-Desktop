using System;
using SharpDX.WPF;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D10;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D10.Buffer;
using Device = SharpDX.Direct3D10.Device;
using System.ComponentModel;

namespace Week00
{
	public class Scene : SceneBase<D3D10>
    {
		public FPS FPS { get; set; }

        private InputLayout VertexLayout;
        private DataStream VertexStream;
        private Buffer Vertices;
        private Effect SimpleEffect;
        private Color4 OverlayColor = new Color4(1.0f);

		protected override void Attach()
		{
            Device device = Renderer.Device;
            if (device == null)
                return;

            ShaderBytecode shaderBytes = ShaderBytecode.CompileFromFile("Simple.fx", "fx_4_0", ShaderFlags.None, EffectFlags.None, null, null);
            this.SimpleEffect = new Effect(device, shaderBytes);

            EffectTechnique technique = this.SimpleEffect.GetTechniqueByIndex(0); ;
            EffectPass pass = technique.GetPassByIndex(0);

            this.VertexLayout = new InputLayout(device, pass.Description.Signature, new[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 16, 0) 
            });

            this.VertexStream = new DataStream(3 * 32, true, true);
            this.VertexStream.WriteRange(new[] {
                new Vector4(0.0f, 0.5f, 0.5f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
                new Vector4(0.5f, -0.5f, 0.5f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
                new Vector4(-0.5f, -0.5f, 0.5f, 1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)
            });
            this.VertexStream.Position = 0;

            this.Vertices = new Buffer(device, this.VertexStream, new BufferDescription()
                {
                    BindFlags = BindFlags.VertexBuffer,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = ResourceOptionFlags.None,
                    SizeInBytes = 3 * 32,
                    Usage = ResourceUsage.Default
                }
            );

            device.Flush();
		}

		protected override void Detach()
		{
			Vertices.Dispose();
			VertexLayout.Dispose();
			SimpleEffect.Dispose();
			VertexStream.Dispose();
        }

		public override void RenderScene(DrawEventArgs args)
        {
			if (FPS != null)
				FPS.AddFrame(args.TotalTime);

            Device device = Renderer.Device;
            if (device == null)
                return;

            float t = (float) args.TotalTime.Milliseconds * 0.001f;
            this.OverlayColor.Alpha = t;

            device.InputAssembler.SetInputLayout(this.VertexLayout);
            device.InputAssembler.SetPrimitiveTopology(SharpDX.Direct3D.PrimitiveTopology.TriangleList);
            device.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(this.Vertices, 32, 0));

            EffectTechnique technique = this.SimpleEffect.GetTechniqueByIndex(0);
            EffectPass pass = technique.GetPassByIndex(0);

            EffectVectorVariable overlayColor = this.SimpleEffect.GetVariableBySemantic("OverlayColor").AsVector();

            overlayColor.Set(this.OverlayColor);

			device.ClearRenderTargetView(Renderer.RenderTargetView, new Color4(1.0f, 0, 0, 0));
			for (int i = 0; i < technique.Description.PassCount; ++i)
            {
                pass.Apply();
                device.Draw(3, 0);
            }
        }
	}
}
