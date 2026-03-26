using EditorCore.Buffer;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorFramework.Widgets
{
    internal class PromptTextWindow : InputTextWindow
    {
        public PromptTextWindow(IApplication app, ILayoutManager layout, EditorBuffer buffer) : base(app, layout, buffer)
        {
        }
        public override bool HandleEvent(EventBase e)
        {
            switch (e)
            {
                case QuitEvent:
                    Environment.Exit(1);
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Escape):
                    /* don't apply any actions */
                    OnQuit = null;
                    DeleteSelf();
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Enter, KeyMode.Ctrl):
                    DeleteSelf();
                    return false;
            }
            return base.HandleEvent(e);
        }
    }
}
