using EditorCore.Buffer;
using EditorCore.Cursor;
using EditorCore.Selection;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL2Interface
{
    internal abstract class BaseTextWindow : BaseWindow
    {
        internal EditorBuffer buffer;
        internal EditorCursor cursor;

        protected BaseTextWindow(EditorBuffer buffer, Rect position) : base(position)
        {
            this.buffer = buffer;
            this.cursor = buffer.CreateCursor();
            this.cursor.Selections.Add(new EditorSelection(this.cursor, 0));
        }

        public override void DrawElements()
        {
            SDL.SetRenderDrawColor(renderer, 0, 0, 0, 0);
            SDL.RenderClear(renderer);

            /* draw text */
            for (int i = 0; i < H / textRenderer.fontLineStep; ++i)
            {
                Rope.Rope<char>? s = buffer.GetLine(i);
                if (s != null)
                {
                    textRenderer.DrawTextLine(5, i * textRenderer.fontLineStep, s.Value);
                }
            }

            /* draw cursor */
            SDL.SetRenderDrawColor(renderer, 255, 255, 255, 255);
            foreach (var selection in cursor.Selections)
            {
                {
                    (long line, long offset) = buffer.GetPositionOffsets(selection.End);
                    Rect r = new((int)offset * textRenderer.fontStep, (int)line * textRenderer.fontLineStep, 5, textRenderer.fontLineStep);
                    SDL.RenderFillRect(renderer, ref r);
                }
                for (long p = selection.Min; p < selection.Max; ++p)
                {
                    (long line, long offset) = buffer.GetPositionOffsets(p);
                    Rect r = new((int)offset * textRenderer.fontStep, (int)line * textRenderer.fontLineStep + textRenderer.fontLineStep - 5, textRenderer.fontStep, 5);
                    SDL.RenderFillRect(renderer, ref r);
                }
            }
        }

        public override bool HandleEvent(Event e)
        {
            switch (e.Type)
            {
                case EventType.Quit:
                    Environment.Exit(1);
                    return false;
                case EventType.TextInput:
                    string s = GetTextInputValue(e.Text);
                    /* clear all selection */
                    cursor?.Selections.ForEach(x => x.Cursor.Buffer.DeleteString(x.Min, x.TextLength));
                    cursor?.Selections.ForEach(x => x.InsertText(s));
                    return false;
                case EventType.KeyDown:
                    if (e.Keyboard.Keysym.Scancode == Scancode.F && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        /* v.1 - open powerWindow */
                        if (cursor != null)
                        {
                            Program.OpenWindow(new PowerFindWindow(cursor.Buffer.Server, cursor, position));
                            return false;
                        }
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.E && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        /* v.1 - open powerWindow */
                        if (cursor != null)
                        {
                            Program.OpenWindow(new PowerEditWindow(cursor.Buffer.Server, cursor, position));
                            return false;
                        }
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.C && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0)
                    {
                        if (cursor != null)
                        {
                            if (cursor.Selections.Count > 1)
                            {
                                cursor.Selections = [cursor.Selections[0]];
                            }
                            return false;
                        }
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.Right)
                    {
                        cursor?.Selections.ForEach(x => { x.MoveHorisontal(1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.Left)
                    {
                        cursor?.Selections.ForEach(x => { x.MoveHorisontal(-1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.Down)
                    {
                        cursor?.Selections.ForEach(x => { x.MoveVertical(1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.Up)
                    {
                        cursor?.Selections.ForEach(x => { x.MoveVertical(-1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.Tab)
                    {
                        cursor?.Selections.ForEach(x =>
                        {
                            if (x.TextLength > 0)
                            {
                            }
                            else
                            {
                                x.InsertText("    ");
                            }
                        });
                        return false;
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.Backspace)
                    {
                        cursor?.Selections.ForEach(x =>
                        {
                            if (x.TextLength == 0)
                            {
                                x.Cursor.Buffer.DeleteString(x.End - 1, 1);
                            }
                            else
                            {
                                x.Cursor.Buffer.DeleteString(x.Min, x.TextLength);
                            }
                        });
                        return false;
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.Return)
                    {
                        cursor?.Selections.ForEach(x => x.InsertText("\n"));
                        return false;
                    }
                    break;
            }
            return base.HandleEvent(e);
        }
    }
}
