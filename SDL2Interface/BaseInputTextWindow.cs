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
    internal abstract class BaseInputTextWindow : SimpleTextWindow
    {
        internal EditorCursor cursor;

        protected BaseInputTextWindow(EditorBuffer buffer, Rect position) : base(buffer, position)
        {
            this.cursor = buffer.CreateCursor();
            this.cursor.Selections.Add(new EditorSelection(this.cursor, 0));
        }

        public override void DrawElements()
        {
            base.DrawElements();

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
                    else if (e.Keyboard.Keysym.Scancode == Scancode.E && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        /* v.1 - open powerWindow */
                        if (cursor != null)
                        {
                            var win = new PowerEditWindow(cursor.Buffer.Server, cursor, position);
                            Program.OpenWindow(new PowerEditPreviewWindow(position, win));
                            return false;
                        }
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.C && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0)
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
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Home)
                    {
                        cursor?.Selections.ForEach(x => { x.MoveToLineBegin(((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.End)
                    {
                        cursor?.Selections.ForEach(x => { x.MoveToLineEnd(((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.PageUp)
                    {
                        long step = H / textRenderer.fontLineStep;
                        cursor?.Selections.ForEach(x => { x.MoveVertical(-step, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.PageDown)
                    {
                        long step = H / textRenderer.fontLineStep;
                        cursor?.Selections.ForEach(x => { x.MoveVertical(step, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Right && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        cursor?.Selections.ForEach(x => { x.MoveHorisontalWord(1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Left && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        cursor?.Selections.ForEach(x => { x.MoveHorisontalWord(-1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Down && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        cursor?.Selections.ForEach(x => { x.MoveVertical(10, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Up && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        cursor?.Selections.ForEach(x => { x.MoveVertical(-10, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Right)
                    {
                        cursor?.Selections.ForEach(x => { x.MoveHorisontal(1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Left)
                    {
                        cursor?.Selections.ForEach(x => { x.MoveHorisontal(-1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Down)
                    {
                        cursor?.Selections.ForEach(x => { x.MoveVertical(1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Up)
                    {
                        cursor?.Selections.ForEach(x => { x.MoveVertical(-1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Tab && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0)
                    {
                        cursor?.Selections.ForEach(x =>
                        {
                            long endLine = x.MaxLine;
                            for (long line = x.MinLine; line <= endLine; ++line)
                            {
                                (long pos, Rope.Rope<char>? str) = x.Cursor.Buffer.GetLine(line);
                                if (str != null)
                                {
                                    x.Cursor.Buffer.DeleteString(pos, Math.Min(4, str.Value.ToString().TakeWhile(char.IsWhiteSpace).Count()));
                                }
                            }
                        });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Tab)
                    {
                        cursor?.Selections.ForEach(x =>
                        {
                            if (x.TextLength > 0)
                            {
                                long endLine = x.MaxLine;
                                for (long line = x.MinLine; line <= endLine; ++line)
                                {
                                    x.Cursor.Buffer.InsertString(x.Cursor.Buffer.GetLine(line).Item1, "    ");
                                }
                            }
                            else
                            {
                                x.InsertText("    ");
                            }
                        });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Backspace && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        cursor?.Selections.ForEach(x =>
                        {
                            if (x.TextLength == 0)
                            {
                                x.MoveHorisontalWord(-1, true);
                            }
                            x.Cursor.Buffer.DeleteString(x.Min, x.TextLength);
                        });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Backspace)
                    {
                        cursor?.Selections.ForEach(x =>
                        {
                            if (x.TextLength == 0)
                            {
                                if (x.End >= 4 &&
                                    x.Cursor.Buffer.Text.Slice(x.End - 4, 4).All(x => x == ' '))
                                {
                                    x.Cursor.Buffer.DeleteString(x.End - 4, 4);
                                }
                                else
                                {
                                    x.Cursor.Buffer.DeleteString(x.End - 1, 1);
                                }
                            }
                            else
                            {
                                x.Cursor.Buffer.DeleteString(x.Min, x.TextLength);
                            }
                        });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Return)
                    {
                        /* clear all selection */
                        cursor?.Selections.ForEach(x => x.Cursor.Buffer.DeleteString(x.Min, x.TextLength));
                        /* find previous line with text, and use it's indent */
                        cursor?.Selections.ForEach(x => {
                            long line = x.EndLine;
                            string? content = null;
                            while (line >= 0 && string.IsNullOrEmpty(content = x.Cursor.Buffer.GetLine(line).Item2.ToString()))
                            {
                                line--;
                            }
                            line++;
                            if (content != null)
                            {
                                int indent = content.TakeWhile(char.IsWhiteSpace).Count();
                                x.InsertText("\n" + new string(' ', indent));
                            }
                        });
                        return false;
                    }
                    break;
            }
            return base.HandleEvent(e);
        }
    }
}
