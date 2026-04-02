using System;
using System.Collections.Generic;
using System.Text;

namespace EditorFramework.Layout
{
    public struct Rect
    {
        public long X;
        public long Y;
        public long W;
        public long H;

        public Rect(long x, long y, long w, long h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public Rect(Rect other)
        {
            X = other.X;
            Y = other.Y;
            W = other.W;
            H = other.H;
        }

        public readonly long Ax => X;
        public readonly long Ay => Y;
        public readonly long Bx => X + W;
        public readonly long By => Y + H;
    }
}
