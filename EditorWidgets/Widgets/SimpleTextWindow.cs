using EditorCore.Buffer;
using EditorCore.Cursor;
using EditorCore.Selection;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace EditorFramework.Widgets
{
    public class SimpleTextWindow : BaseWindow
    {
        public EditorBuffer buffer;
        public long viewOffset = 0;
        public bool showNumbers = true;

        public SimpleTextWindow(IApplication App, ILayoutManager layout, EditorBuffer buffer) : base(App, layout)
        {
            this.buffer = buffer;
        }

        public override bool HandleEvent(EventBase e)
        {
            switch (e)
            {
                case QuitEvent:
                    Environment.Exit(1);
                    return false;
            }
            return base.HandleEvent(e);
        }
    }
}
