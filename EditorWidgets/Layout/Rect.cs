using System;
using System.Collections.Generic;
using System.Text;

namespace EditorFramework.Layout
{
    public struct Rect(long x, long y, long w, long h)
    {
        public long X = x;
        public long Y = y;
        public long W = w;
        public long H = h;

        public readonly long Ax => X;
        public readonly long Ay => Y;
        public readonly long Bx => X + W;
        public readonly long By => Y + H;
    }
}
