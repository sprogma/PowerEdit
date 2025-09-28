using EditorCore.Buffer;
using EditorCore.Cursor;
using EditorCore.Selection;
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
    internal class PowerEditWindow: InputTextWindow
    {
        public EditorCursor usingCursor;
        string editType;

        public (IEnumerable<string>?, string?) CurrentResult()
        {
            if (editType == "powerEdit")
            {
                var res = buffer.Server.CommandProvider.Execute(buffer.Text.ToString(), usingCursor.Selections.ToArray());
                return (res.Item1?.Select(x => x.ToString()).Where(x => x != null).Cast<string>(), res.Item2);
            }
            else
            {
                var text = usingCursor.SelectionsText.Where(x => x != "").ToArray();
                if (text.Length == 0)
                {
                    text = [usingCursor.Buffer.Text.ToString()];
                }
                var res = buffer.Server.CommandProvider.Execute(buffer.Text.ToString(), text);
                return (res.Item1?.Select(x => x.ToString()).Where(x => x != null).Cast<string>(), res.Item2);
            }
        }

        public PowerEditWindow(EditorServer server, EditorCursor usingCursor, Rect position, string editType) : 
                               base(new EditorBuffer(server, server.CommandProvider.Tokenizer), position)
        {
            (long begin, long end, string text) = server.CommandProvider.ExampleScript(editType);
            buffer.SetText(text);
            cursor?.Selections[0].SetPosition(begin, end);
            this.usingCursor = usingCursor;
            this.editType = editType;
        }

        internal void Apply()
        {
            if (editType == "powerEdit")
            {
                usingCursor.ApplyCommand("powerEdit", buffer.Text.ToString());
            }
            else /* edit or replace events */
            {
                usingCursor.ApplyCommand("edit", buffer.Text.ToString());
            }
            usingCursor.Commit();
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

