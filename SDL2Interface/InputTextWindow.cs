using EditorCore.Buffer;
using EditorCore.Cursor;
using EditorCore.Selection;
using Microsoft.ApplicationInsights.Metrics.Extensibility;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SDL2Interface
{
    internal class InputTextWindow : SimpleTextWindow
    {
        internal EditorCursor? cursor;
        bool relativeNumbers = true;
        long enteredLineNumber = 0;
        bool jumpInput = false;

        public InputTextWindow(EditorBuffer buffer, Rect position) : base(buffer, position)
        {
            this.cursor = buffer.Cursor;
        }

        public override void DrawElements()
        {
            SDL.SetRenderDrawColor(renderer, 0, 0, 0, 0);
            SDL.RenderFillRect(renderer, ref position);

            int leftBarSize = 0;

            if (showNumbers)
            {
                if (relativeNumbers && cursor?.Selections.Count == 1)
                {
                    long cursorLine = cursor.Selections[0].EndLine;
                    int maxPower = 4;
                    long dummyValue = 0;
                    /* draw numbers */
                    for (int i = 0; i < H / textRenderer.FontLineStep; ++i)
                    {
                        (long index, string? s) = buffer.GetLine(i);
                        if (s != null)
                        {
                            long num = i;
                            if (num < cursorLine)
                            {
                                num = 100 - (cursorLine - num);
                            }
                            else
                            {
                                num = num - cursorLine;
                            }
                            if (num == 0)
                            {
                                Rect rect = new(position.X + 5, position.Y + i * textRenderer.FontLineStep, position.Width - 10, textRenderer.FontLineStep);
                                SDL.SetRenderDrawColor(renderer, 0, 20, 20, 255);
                                SDL.RenderFillRect(renderer, ref rect);
                            }
                            else
                            {
                                textRenderer.DrawTextLine(position.X + 5, position.Y + i * textRenderer.FontLineStep, num.ToString().PadLeft(maxPower), 0, [], ref dummyValue);
                            }
                        }
                    }
                    leftBarSize = (int)((maxPower + 0.5) * textRenderer.FontStep);
                }
                else
                {
                    SimpleTextWindowDrawSimpleNumbers(ref leftBarSize);
                }
            }

            SimpleTextWindowDrawText(leftBarSize);

            /* draw cursor */
            SDL.SetRenderDrawColor(renderer, 255, 255, 255, 255);
            if (cursor != null)
            {
                foreach (var selection in cursor.Selections)
                {
                    {
                        (long line, long offset) = buffer.GetPositionOffsets(selection.End);
                        Rect r = new(leftBarSize + position.X + 5 + (int)offset * textRenderer.FontStep, position.Y + (int)line * textRenderer.FontLineStep, 5, textRenderer.FontLineStep);
                        SDL.RenderFillRect(renderer, ref r);
                    }
                    int selectionWidth = (int)(5 * textRenderer.currentScale);
                    for (long p = selection.Min; p < selection.Max; ++p)
                    {
                        (long line, long offset) = buffer.GetPositionOffsets(p);
                        Rect r = new(leftBarSize + position.X + 5 + (int)offset * textRenderer.FontStep, position.Y + (int)line * textRenderer.FontLineStep + textRenderer.FontLineStep - selectionWidth, textRenderer.FontStep, selectionWidth);
                        SDL.RenderFillRect(renderer, ref r);
                    }
                }
            }
        }


        [DllImport("user32.dll", EntryPoint = "keybd_event")]
        static extern void WinapiKeybdEvent(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        public override bool HandleEvent(Event e)
        {
            switch (e.Type)
            {
                case EventType.Quit:
                    Environment.Exit(1);
                    return false;
                case EventType.TextInput:
                    if (!jumpInput)
                    {
                        string s = GetTextInputValue(e.Text);
                        /* clear all selection */
                        cursor?.Selections.ForEach(x => x.Cursor.Buffer.DeleteString(x.Min, x.TextLength));
                        cursor?.Selections.ForEach(x => x.InsertText(s));
                        cursor?.Commit();
                    }
                    return false;
                case EventType.KeyUp:
                    if (e.Keyboard.Keysym.Scancode == Scancode.CapsLock)
                    {
                        if (enteredLineNumber != 0)
                        {
                            long offset = enteredLineNumber;
                            if (enteredLineNumber > 50)
                            {
                                offset = enteredLineNumber - 100;
                            }
                            cursor?.Selections.ForEach(x => x.MoveVertical(offset, false));
                            const int KEYEVENTF_EXTENDEDKEY = 0x1;
                            const int KEYEVENTF_KEYUP = 0x2;
                            WinapiKeybdEvent(0x14, 0x45, KEYEVENTF_EXTENDEDKEY, 0);
                            WinapiKeybdEvent(0x14, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
                        }
                        enteredLineNumber = 0;
                        jumpInput = false;
                    }
                    break;
                case EventType.KeyDown:
                    if (e.Keyboard.Keysym.Scancode == Scancode.CapsLock)
                    {
                        jumpInput = true;
                        return false;
                    }
                    if (Scancode.D1 <= e.Keyboard.Keysym.Scancode && e.Keyboard.Keysym.Scancode <= Scancode.D0)
                    {
                        if (((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Caps) != 0)
                        {
                            enteredLineNumber *= 10;
                            enteredLineNumber += (e.Keyboard.Keysym.Scancode == Scancode.D0 ? 0 : e.Keyboard.Keysym.Scancode - Scancode.D1 + 1);
                        }
                        return false;
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.C && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        cursor?.Selections.ForEach(x => x.Copy());
                        return false;
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.X && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        cursor?.Selections.ForEach(x => x.Copy());
                        cursor?.Selections.ForEach(x => x.Cursor.Buffer.DeleteString(x.Min, x.TextLength));
                        cursor?.Commit();
                        return false;
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.V && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        cursor?.Selections.ForEach(x => x.Cursor.Buffer.DeleteString(x.Min, x.TextLength));
                        cursor?.Selections.ForEach(x => x.Paste());
                        cursor?.Commit();
                        return false;
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.Minus && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        textRenderer.Scale(0.9);
                        return false;
                    }
                    if (e.Keyboard.Keysym.Scancode == Scancode.Equals && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        textRenderer.Scale(1.1);
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.D && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        cursor?.Selections.ForEach(x =>
                        {
                            if (x.Text.Length == 0)
                            {
                                var line = x.Cursor.Buffer.GetLine(x.EndLine);
                                if (line.Item2 != null)
                                {
                                    x.Cursor.Buffer.InsertString(line.Item1, line.Item2 + (line.Item2.EndsWith("\n") ? "" : "\n"));
                                    x.UpdateFromLineOffset();
                                }
                            }
                            else
                            {
                                long a = x.Begin, b = x.End;
                                x.Cursor.Buffer.InsertString(x.Min, x.Text);
                                x.UpdateFromLineOffset();
                            }
                        });
                        cursor?.Commit();
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.A && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        if (cursor != null)
                        {
                            cursor.Selections = [new EditorSelection(cursor, 0, cursor.Buffer.Text.Length)];
                        }
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.W && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        cursor?.Selections.ForEach(x => x.SetPosition(x.End, x.Begin));
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Q && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        cursor?.Selections.ForEach(x => x.SetPosition(x.End));
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Z && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        Console.WriteLine("UN");
                        cursor?.Buffer.Undo();
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Y && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        Console.WriteLine("RE");
                        cursor?.Buffer.Redo();
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.F && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        /* v.1 - open powerWindow */
                        if (cursor != null)
                        {
                            Program.OpenWindow(new PowerFindWindow(cursor.Buffer.Server, cursor, position));
                            return false;
                        }
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.E && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0
                                                                      && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0)
                    {
                        /* v.1 - open powerWindow powerEdit */
                        if (cursor != null)
                        {
                            Console.WriteLine("powerEdit!\n");
                            var win = new PowerEditWindow(cursor.Buffer.Server, cursor, position, "powerEdit");
                            Program.OpenWindow(new PowerEditWithPreviewWindow(position, win));
                            return false;
                        }
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.E && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        /* v.1 - open powerWindow edit */
                        if (cursor != null)
                        {
                            var win = new PowerEditWindow(cursor.Buffer.Server, cursor, position, "edit");
                            Program.OpenWindow(new PowerEditWithPreviewWindow(position, win));
                            return false;
                        }
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.R && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        /* v.1 - open powerWindow replace */
                        if (cursor != null)
                        {
                            var win = new PowerEditWindow(cursor.Buffer.Server, cursor, position, "replace");
                            Program.OpenWindow(new PowerEditWithPreviewWindow(position, win));
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
                    else if (e.Keyboard.Keysym.Scancode == Scancode.N && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0)
                    {
                        if (cursor != null)
                        {
                            var lastSelection = cursor.Selections.MaxBy(x => x.Max);
                            if (lastSelection != null && lastSelection.TextLength != 0)
                            {
                                string strToFind = lastSelection.Text;
                                long next = cursor.Buffer.Text.IndexOf(strToFind, lastSelection.Max);
                                if (next != -1)
                                {
                                    cursor.Selections.Add(new EditorSelection(cursor, next, next + lastSelection.TextLength));
                                }
                            }
                            return false;
                        }
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.X && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0)
                    {
                        if (cursor != null)
                        {
                            var lastSelection = cursor.Selections.MaxBy(x => x.Max);
                            if (lastSelection != null && lastSelection.TextLength != 0)
                            {
                                string strToFind = lastSelection.Text;
                                long next = cursor.Buffer.Text.IndexOf(strToFind, lastSelection.Max);
                                if (next != -1)
                                {
                                    lastSelection.SetPosition(next, next + lastSelection.TextLength);
                                }
                            }
                            return false;
                        }
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Home ||
                            (e.Keyboard.Keysym.Scancode == Scancode.J && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0))
                    {
                        cursor?.Selections.ForEach(x => { x.MoveToLineBegin(((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.End ||
                            (e.Keyboard.Keysym.Scancode == Scancode.SemiColon && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0))
                    {
                        cursor?.Selections.ForEach(x => { x.MoveToLineEnd(((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.PageUp)
                    {
                        long step = H / textRenderer.FontLineStep;
                        cursor?.Selections.ForEach(x => { x.MoveVertical(-step, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.PageDown)
                    {
                        long step = H / textRenderer.FontLineStep;
                        cursor?.Selections.ForEach(x => { x.MoveVertical(step, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Down && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0)
                    {
                        if (cursor?.Selections.Count == 1)
                        {
                            cursor?.Selections.ForEach(x => {
                                long MaxLine = x.MaxLine;
                                var after = x.Cursor.Buffer.GetLine(MaxLine + 1);
                                if (after.Item2 == null)
                                {
                                    return;
                                }
                                x.Cursor.Buffer.DeleteString(after.Item1, after.Item2.Length);
                                var before = x.Cursor.Buffer.GetLine(x.MinLine);
                                if (before.Item2 == null)
                                {
                                    return;
                                }
                                x.Cursor.Buffer.InsertString(before.Item1, after.Item2);
                            });
                            cursor?.Commit();
                        }
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Up && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0)
                    {
                        if (cursor?.Selections.Count == 1)
                        {
                            cursor?.Selections.ForEach(x => {
                                long MinLine = x.MinLine;
                                var before = x.Cursor.Buffer.GetLine(MinLine - 1);
                                if (before.Item2 == null)
                                {
                                    return;
                                }
                                x.Cursor.Buffer.DeleteString(before.Item1, before.Item2.Length);
                                var after = x.Cursor.Buffer.GetLine(x.MaxLine + 1);
                                if (after.Item2 == null)
                                {
                                    return;
                                }
                                x.Cursor.Buffer.InsertString(after.Item1, before.Item2);
                            });
                            cursor?.Commit();
                        }
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Down ||
                            (e.Keyboard.Keysym.Scancode == Scancode.K && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0))
                    {
                        cursor?.Selections.ForEach(x => { x.MoveVertical(1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Up ||
                            (e.Keyboard.Keysym.Scancode == Scancode.L && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0))
                    {
                        cursor?.Selections.ForEach(x => { x.MoveVertical(-1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if ((e.Keyboard.Keysym.Scancode == Scancode.Right && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0) ||
                             (e.Keyboard.Keysym.Scancode == Scancode.L && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0))
                    {
                        cursor?.Selections.ForEach(x => { x.MoveHorisontalWord(1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if ((e.Keyboard.Keysym.Scancode == Scancode.Left && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0) ||
                             (e.Keyboard.Keysym.Scancode == Scancode.K && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0))
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
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Right ||
                            (e.Keyboard.Keysym.Scancode == Scancode.SemiColon && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0))
                    {
                        cursor?.Selections.ForEach(x => { x.MoveHorisontal(1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Left ||
                            (e.Keyboard.Keysym.Scancode == Scancode.J && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0))
                    {
                        cursor?.Selections.ForEach(x => { x.MoveHorisontal(-1, ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0); });
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Tab && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Shift) != 0)
                    {
                        cursor?.Selections.ForEach(x =>
                        {
                            long endLine = x.MaxLine;
                            for (long line = x.MinLine; line <= endLine; ++line)
                            {
                                (long pos, string? str) = x.Cursor.Buffer.GetLine(line);
                                if (str != null)
                                {
                                    x.Cursor.Buffer.DeleteString(pos, Math.Min(4, str.TakeWhile(char.IsWhiteSpace).Count()));
                                    x.UpdateFromLineOffset();
                                }
                            }
                        });
                        cursor?.Commit();
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
                                    x.UpdateFromLineOffset();
                                }
                            }
                            else
                            {
                                x.InsertText("    ");
                            }
                        });
                        cursor?.Commit();
                        return false;
                    }
                    else if ((e.Keyboard.Keysym.Scancode == Scancode.I && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0) ||
                             (e.Keyboard.Keysym.Scancode == Scancode.I && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0))
                    {
                        cursor?.Selections.ForEach(x =>
                        {
                            if (x.TextLength == 0)
                            {
                                x.MoveToLineEnd(false);
                                x.MoveVertical(-1, true);
                                x.MoveToLineEnd(true);
                            }
                            x.Cursor.Buffer.DeleteString(x.Min, x.TextLength);
                            x.UpdateFromLineOffset();
                        });
                        cursor?.Commit();
                        return false;
                    }
                    else if ((e.Keyboard.Keysym.Scancode == Scancode.Backspace && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0) ||
                             (e.Keyboard.Keysym.Scancode == Scancode.O && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0) ||
                             (e.Keyboard.Keysym.Scancode == Scancode.O && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0))
                    {
                        cursor?.Selections.ForEach(x =>
                        {
                            if (x.TextLength == 0)
                            {
                                x.MoveHorisontalWord(-1, true);
                            }
                            x.Cursor.Buffer.DeleteString(x.Min, x.TextLength);
                            x.UpdateFromLineOffset();
                        });
                        cursor?.Commit();
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Backspace ||
                            (e.Keyboard.Keysym.Scancode == Scancode.P && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0) ||
                            (e.Keyboard.Keysym.Scancode == Scancode.P && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0))
                    {
                        cursor?.Selections.ForEach(x =>
                        {
                            if (x.TextLength == 0)
                            {
                                if (x.End >= 4 &&
                                    x.Cursor.Buffer.Text.Substring(x.End - 4, 4).All(x => x == ' '))
                                {
                                    x.Cursor.Buffer.DeleteString(x.End - 4, 4);
                                    x.UpdateFromLineOffset();
                                }
                                else
                                {
                                    x.Cursor.Buffer.DeleteString(x.End - 1, 1);
                                    x.UpdateFromLineOffset();
                                }
                            }
                            else
                            {
                                x.Cursor.Buffer.DeleteString(x.Min, x.TextLength);
                                x.UpdateFromLineOffset();
                            }
                        });
                        cursor?.Commit();
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Delete && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0)
                    {
                        cursor?.Selections.ForEach(x =>
                        {
                            if (x.TextLength == 0)
                            {
                                x.MoveHorisontalWord(1, true);
                            }
                            x.Cursor.Buffer.DeleteString(x.Min, x.TextLength);
                        });
                        cursor?.Commit();
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Delete)
                    {
                        cursor?.Selections.ForEach(x =>
                        {
                            if (x.TextLength == 0)
                            {
                                x.Cursor.Buffer.DeleteString(x.End, 1);
                            }
                            else
                            {
                                x.Cursor.Buffer.DeleteString(x.Min, x.TextLength);
                            }
                        });
                        cursor?.Commit();
                        return false;
                    }
                    else if (e.Keyboard.Keysym.Scancode == Scancode.Return)
                    {
                        /* clear all selection */
                        cursor?.Selections.ForEach(x => x.Cursor.Buffer.DeleteString(x.Min, x.TextLength));
                        /* find previous line with text, and use it's indent */
                        if (cursor?.Selections.Count > 1)
                        {
                            cursor?.Selections.ForEach(x => x.InsertText("\n"));
                        }
                        else
                        {
                            cursor?.Selections.ForEach(x =>
                            {
                                long line = x.EndLine;
                                string? content = null;
                                while (line >= 0 && string.IsNullOrWhiteSpace(content = x.Cursor.Buffer.GetLine(line).Item2))
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
                        }
                        cursor?.Commit();
                        return false;
                    }
                    else if ((e.Keyboard.Keysym.Scancode == Scancode.N && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Alt) != 0) ||
                             (e.Keyboard.Keysym.Scancode == Scancode.N && ((int)e.Keyboard.Keysym.Mod & (int)KeyModifier.Ctrl) != 0))
                    {
                        /* clear all selection */
                        cursor?.Selections.ForEach(x => x.Cursor.Buffer.DeleteString(x.Min, x.TextLength));
                        /* find previous line with text, and use it's indent */
                        cursor?.Selections.ForEach(x =>
                        {
                            x.MoveToLineEnd();
                            long line = x.EndLine;
                            string? content = null;
                            while (line >= 0 && string.IsNullOrWhiteSpace(content = x.Cursor.Buffer.GetLine(line).Item2))
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
                        cursor?.Commit();
                        return false;
                    }
                    break;
            }
            return base.HandleEvent(e);
        }
    }
}
