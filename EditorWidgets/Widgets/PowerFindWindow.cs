using EditorCore.Buffer;
using EditorCore.Cursor;
using EditorCore.Server;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;

namespace EditorFramework.Widgets
{
    internal class PowerFindWindow : InputTextWindow
    {
        public EditorCursor usingCursor;


        public PowerFindWindow(IApplication app, ILayoutManager layout, EditorServer server, EditorCursor usingCursor) : 
                               base(app, layout, new EditorBuffer(server, usingCursor.Buffer.Tokenizer, null, "", new PersistentCTextBuffer()))
        {
            buffer.SetText("");
            this.usingCursor = usingCursor;
        }

        private void Apply()
        {
            usingCursor.ApplyCommand("find", buffer.Text.Substring(0));
        }

        public override bool HandleEvent(EventBase e)
        {
            switch (e)
            {
                case QuitEvent:
                    Environment.Exit(1);
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Escape):
                    DeleteSelf();
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Enter, KeyMode.Ctrl):
                    Apply();
                    DeleteSelf();
                    return false;
            }
            return base.HandleEvent(e);
        }
    }
}

