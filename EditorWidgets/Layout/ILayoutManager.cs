using EditorFramework.Widgets;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorFramework.Layout
{
    public interface ILayoutManager
    {
        public void Resize(BaseWindow window, Rect NewSize);
        public Rect Position { get; }
    }
}
