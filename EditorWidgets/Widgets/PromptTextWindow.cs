using EditorCore.Buffer;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorFramework.Widgets
{
    internal class PromptTextWindow : InputTextWindow
    {
        public PromptTextWindow(EditorBuffer buffer, Rect position) : base(buffer, position)
        {
        }
        public override bool HandleEvent(Event e)
        {
            switch (e.Type)
            {
                case EventType.Quit:
                    Environment.Exit(1);
                    return false;
                case EventType.KeyDown:
                    if (e.Keyboard.Keysym.Scancode == Scancode.Escape)
                    {
                        /* don't apply any actions */
                        OnQuit = null;
                        DeleteSelf();
                        return false;
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.Return && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        DeleteSelf();
                        return false;
                    }
                    break;
            }
            return base.HandleEvent(e);
        }
    }
}
