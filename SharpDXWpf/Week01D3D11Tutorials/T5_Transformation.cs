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
    /// DirectX SDK: Tutorial 5: Transformation
    /// http://msdn.microsoft.com/en-us/library/ff729722(v=VS.85).aspx
    /// </summary>
    public class T5_Transformation : D3D11
    {
        /// <summary>
        /// 
        /// </summary>
        public T5_Transformation()
        {
            using (var dg = new DisposeGroup())
            {
                // --- init shaders
                ShaderFlags sFlags = ShaderFlags.EnableStrictness;
#if DEBUG
                sFlags |= ShaderFlags.Debug;
#endif
                var pVSBlob = dg.Add(ShaderBytecode.CompileFromFile("T5_Transformation.fx", "VS", "vs_4_0", sFlags, EffectFlags.None));
                var inputSignature = dg.Add(ShaderSignature.GetInputSignature(pVSBlob));
                m_pVertexShader = new VertexShader(Device, pVSBlob);

                var pPSBlob = dg.Add(ShaderBytecode.CompileFromFile("T5_Transformation.fx", "PS", "ps_4_0", sFlags, EffectFlags.None));
                m_pPixelShader = new PixelShader(Device, pPSBlob);

                // --- let DX know about the pixels memory layout
                var layout = dg.Add(new InputLayout(Device, inputSignature, new[]{
                    new InputElement("POSITION", 0, Format.R32G32B32_Float, 0),
                    new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0),
                }));
                Device.ImmediateContext.InputAssembler.InputLayout = (layout);

                // --- init vertices
                var vertexBuffer = dg.Add(DXUtils.CreateBuffer(Device, new[]{
                    new VectorColor(new Vector3(-1.0f,  1.0f, -1.0f), new Color4(1.0f, 0.0f, 0.0f, 1.0f)),
                    new VectorColor(new Vector3( 1.0f,  1.0f, -1.0f), new Color4(1.0f, 0.0f, 1.0f, 0.0f)),
                    new VectorColor(new Vector3( 1.0f,  1.0f,  1.0f), new Color4(1.0f, 0.0f, 1.0f, 1.0f)),
                    new VectorColor(new Vector3(-1.0f,  1.0f,  1.0f), new Color4(1.0f, 1.0f, 0.0f, 0.0f)),
                    new VectorColor(new Vector3(-1.0f, -1.0f, -1.0f), new Color4(1.0f, 1.0f, 0.0f, 1.0f)),
                    new VectorColor(new Vector3( 1.0f, -1.0f, -1.0f), new Color4(1.0f, 1.0f, 1.0f, 0.0f)),
                    new VectorColor(new Vector3( 1.0f, -1.0f,  1.0f), new Color4(1.0f, 1.0f, 1.0f, 1.0f)),
                    new VectorColor(new Vector3(-1.0f, -1.0f,  1.0f), new Color4(1.0f, 0.0f, 0.0f, 0.0f)),
                }));
                Device.ImmediateContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, VectorColor.SizeInBytes, 0));

                // --- init indices
                var indicesBuffer = dg.Add(DXUtils.CreateBuffer(Device, new ushort[] {
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
                }));
                Device.ImmediateContext.InputAssembler.SetIndexBuffer(indicesBuffer, Format.R16_UInt, 0);

                Device.ImmediateContext.InputAssembler.PrimitiveTopology = (PrimitiveTopology.TriangleList);

                // --- create the constant buffer
                Set(ref m_pConstantBuffer, new ConstantBuffer<Projections>(Device));
                Device.ImmediateContext.VertexShader.SetConstantBuffer(0, m_pConstantBuffer.Buffer);
            }

            Camera = new FirstPersonCamera();
            Camera.SetProjParams((float)Math.PI / 2, 1, 0.01f, 100.0f);
            Camera.SetViewParams(new Vector3(0.0f, 0.0f, -5.0f), new Vector3(0.0f, 1.0f, 0.0f));
        }

        /// <summary>
        /// 
        /// </summary>        
        public override void RenderScene(DrawEventArgs args)
        {
            /// --- timer
            float t = (float)args.TotalTime.TotalSeconds;


            /// --- clear 
            Device.ImmediateContext.ClearRenderTargetView(this.RenderTargetView, new Color4(0.5f, 0.5f, 0.99f, 1.0f));//(1.0f, 0.3f, 0.525f, 0.8f)
            Device.ImmediateContext.ClearDepthStencilView(this.DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);


            /// --- 1st Cube: Rotate around the origin
            var matWorld1 = Matrix.RotationY(t);


            /// --- Update variables for the first cube
            m_pConstantBuffer.Value = new Projections
            {
                Projection = Matrix.Transpose(Camera.Projection),
                View = Matrix.Transpose(Camera.View),
                World = Matrix.Transpose(matWorld1),
            };

            /// --- Render the first cube
            Device.ImmediateContext.VertexShader.Set(m_pVertexShader);
            Device.ImmediateContext.VertexShader.SetConstantBuffer(0, m_pConstantBuffer.Buffer);
            Device.ImmediateContext.PixelShader.Set(m_pPixelShader);
            Device.ImmediateContext.DrawIndexed(36, 0, 0);

            /// --- 2nd Cube:  Rotate around origin
            var matSpin = Matrix.RotationZ(-t);
            var matOrbit = Matrix.RotationY(-t * 2.0f);
            /// --- many orbits of different radii, so compute radius fraction here  
            var matTranslate = Matrix.Translation(-2f, 0f, 0f);
            var matScale = Matrix.Scaling(0.3f, 0.3f, 0.3f);
            /// --- directx matrices are multiplied in reverse order as usal (e.g. MATLAB)
            /// --- is this because they are all transposed compared to standard math notation?
            var matWorld2 = matScale * matSpin * matTranslate * matOrbit;

            /// --- Update variables for the second cube
            m_pConstantBuffer.Value = new Projections
            {
                Projection = Matrix.Transpose(Camera.Projection),
                View = Matrix.Transpose(Camera.View),
                World = Matrix.Transpose(matWorld2),
            };
            /// --- ??Device.ImmediateContext.VertexShader.SetConstantBuffer(0, g_pConstantBuffer.Buffer);

            /// --- Render the cube
            Device.ImmediateContext.DrawIndexed(36, 0, 0);

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

    }
}
