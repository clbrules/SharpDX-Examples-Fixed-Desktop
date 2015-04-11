using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using SharpDX;

namespace Week01D3D11Tutorials
{
    /// <summary>
    /// 
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct VectorColor
    {
        public VectorColor(Vector3 p, Color4 c)
        {
            Point = p;
            Color = c;
        }

        public Vector3 Point;

        public Color4 Color;

        public const int SizeInBytes = (3 + 4) * 4;
    }

    /// <summary>
    /// 
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Projections
    {
        public Matrix World;

        public Matrix View;

        public Matrix Projection;
    }

}
