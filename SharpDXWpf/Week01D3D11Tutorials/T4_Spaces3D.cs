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

    /// <summary>
    /// DirectX SDK: Tutorial 4: 3D Spaces
    /// http://msdn.microsoft.com/en-us/library/ff729721(v=VS.85).aspx
    /// </summary>
    public class T4_Spaces3D : D3D11
    {
        /// <summary>
        /// 
        /// </summary>
        public T4_Spaces3D()
        {
            using (var dg = new DisposeGroup())
            {
                /// --- init shaders
                ShaderFlags sFlags = ShaderFlags.EnableStrictness;
#if DEBUG
                sFlags |= ShaderFlags.Debug;
#endif
                var pVSBlob = dg.Add(ShaderBytecode.CompileFromFile("T4_Spaces3D.fx", "VS", "vs_4_0", sFlags, EffectFlags.None));
                var inputSignature = dg.Add(ShaderSignature.GetInputSignature(pVSBlob));
                m_pVertexShader = new VertexShader(Device, pVSBlob);

                var pPSBlob = dg.Add(ShaderBytecode.CompileFromFile("T4_Spaces3D.fx", "PS", "ps_4_0", sFlags, EffectFlags.None));
                m_pPixelShader = new PixelShader(Device, pPSBlob);

                /// --- let DX know about the pixels memory layout
                var layout = new InputLayout(Device, inputSignature, new[]{
                    new InputElement("POSITION", 0, Format.R32G32B32_Float, 0),
                    new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0),
                });
                Device.ImmediateContext.InputAssembler.InputLayout = (layout);
                dg.Add(layout);

                /// --- init vertices
                var vertexBuffer = DXUtils.CreateBuffer(Device, new[]{
                    new VectorColor(new Vector3(-1.0f,  1.0f, -1.0f), new Color4(1.0f, 0.0f, 0.0f, 1.0f)),
                    new VectorColor(new Vector3( 1.0f,  1.0f, -1.0f), new Color4(1.0f, 0.0f, 1.0f, 0.0f)),
                    new VectorColor(new Vector3( 1.0f,  1.0f,  1.0f), new Color4(1.0f, 0.0f, 1.0f, 1.0f)),
                    new VectorColor(new Vector3(-1.0f,  1.0f,  1.0f), new Color4(1.0f, 1.0f, 0.0f, 0.0f)),
                    new VectorColor(new Vector3(-1.0f, -1.0f, -1.0f), new Color4(1.0f, 1.0f, 0.0f, 1.0f)),
                    new VectorColor(new Vector3( 1.0f, -1.0f, -1.0f), new Color4(1.0f, 1.0f, 1.0f, 0.0f)),
                    new VectorColor(new Vector3( 1.0f, -1.0f,  1.0f), new Color4(1.0f, 1.0f, 1.0f, 1.0f)),
                    new VectorColor(new Vector3(-1.0f, -1.0f,  1.0f), new Color4(1.0f, 0.0f, 0.0f, 0.0f)),
                });
                dg.Add(vertexBuffer);
                Device.ImmediateContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, VectorColor.SizeInBytes, 0));

                /// --- init indices
                var indicesBuffer = DXUtils.CreateBuffer(Device, new ushort[] {
                    3,1,0,
                    2,1,3,

                    0,5,4,
                    1,5,0,

                    3,4,7,
                    0,4,3,

                    1,6,5,
                    2,6,1,

                    2,7,6,
                    3,7,2,

                    6,4,5,
                    7,4,6,
                });
                dg.Add(indicesBuffer);
                Device.ImmediateContext.InputAssembler.SetIndexBuffer(indicesBuffer, Format.R16_UInt, 0);
                Device.ImmediateContext.InputAssembler.PrimitiveTopology = (PrimitiveTopology.TriangleList);

                /// --- create the constant buffer
                m_pConstantBuffer = new ConstantBuffer<Projections>(Device);
                Device.ImmediateContext.VertexShader.SetConstantBuffer(0, m_pConstantBuffer.Buffer);
            }

            Camera = new FirstPersonCamera();
            Camera.SetProjParams((float)Math.PI / 2, 1, 0.01f, 100.0f);
            Camera.SetViewParams(new Vector3(0.0f, 0.0f, -5.0f), new Vector3(0.0f, 1.0f, 0.0f));
        }

        /// <summary>
        /// 
        /// </summary>
        public override void Reset(int w, int h)
        {
            base.Reset(w, h);
            m_Projection = Matrix.PerspectiveFovLH((float)Math.PI / 2, w / (float)h, 0.01f, 100.0f);
        }

        /// <summary>
        /// 
        /// </summary>
        public override void RenderScene(DrawEventArgs args)
        {
            //
            // Animate the cube
            //
            float t = (float)args.TotalTime.TotalSeconds;
            var g_World = Matrix.RotationY(t);

            //
            // Clear the back buffer
            //
            Device.ImmediateContext.ClearRenderTargetView(this.RenderTargetView, new Color4(1.0f, 0.3f, 0.525f, 0.8f));

            //
            // Update variables
            //
            m_pConstantBuffer.Value = new Projections
            {
                World = Matrix.Transpose(g_World),
                View = Matrix.Transpose(Camera.View),
                Projection = Matrix.Transpose(m_Projection),
            };

            //
            // Renders a triangle
            //
            Device.ImmediateContext.VertexShader.Set(m_pVertexShader);
            Device.ImmediateContext.VertexShader.SetConstantBuffer(0, m_pConstantBuffer.Buffer);
            Device.ImmediateContext.PixelShader.Set(m_pPixelShader);
            Device.ImmediateContext.DrawIndexed(36, 0, 0);        // 36 vertices needed for 12 triangles in a triangle list
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            // NOTE: SharpDX 1.3 requires explicit Dispose() of everything
            Set(ref m_pVertexShader, null);
            Set(ref m_pPixelShader, null);
            Set(ref m_pConstantBuffer, null);
        }

        private VertexShader m_pVertexShader;
        private PixelShader m_pPixelShader;
        private ConstantBuffer<Projections> m_pConstantBuffer;
        private Matrix m_Projection;
    }

}
