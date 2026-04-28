using EditorFramework.ApplicationApi;
using EditorFramework.Layout;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorFramework.Widgets
{
    public class SimlpeGameWindow : BaseWindow
    {
        byte[,] field;

        public SimlpeGameWindow(IApplication app, ILayoutManager layout, byte[,] field) : base(app, layout)
        {

        }
    }
}
