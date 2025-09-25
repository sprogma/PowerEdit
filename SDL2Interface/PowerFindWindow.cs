using EditorCore.Buffer;
using EditorCore.Cursor;
using EditorCore.Server;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL2Interface
{
    internal class PowerFindWindow : BaseInputTextWindow
    {
        public EditorCursor usingCursor;


        public PowerFindWindow(EditorServer server, EditorCursor usingCursor, Rect position) : 
                               base(new EditorBuffer(server, usingCursor.Buffer.Tokenizer), position)
        {
            buffer.SetText("");
            this.usingCursor = usingCursor;
        }

        private void Apply()
        {
            usingCursor.ApplyCommand("find", buffer.Text.ToString());
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
                        DeleteSelf();
                        return false;
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.Return && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        Apply();
                        DeleteSelf();
                        return false;
                    }
                    break;
            }
            return base.HandleEvent(e);
        }
    }
}

