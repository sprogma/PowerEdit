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

        public static Rect? Intersect(Rect a, Rect b)
        {
            long x1 = Math.Max(a.Ax, b.Ax);
            long y1 = Math.Max(a.Ay, b.Ay);
            long x2 = Math.Min(a.Bx, b.Bx);
            long y2 = Math.Min(a.By, b.By);

            long width = x2 - x1;
            long height = y2 - y1;

            if (width > 0 && height > 0)
            {
                return new Rect(x1, y1, width, height);
            }

            return null;
        }


        public readonly long Ax => X;
        public readonly long Ay => Y;
        public readonly long Bx => X + W;
        public readonly long By => Y + H;
    }
}
