using EditorCore.Buffer;
using EditorCore.Cursor;
using EditorCore.Selection;
using EditorFramework.ApplicationApi;
using EditorFramework.Events;
using EditorFramework.Layout;
using LoggingLogLevel;
using Microsoft.ApplicationInsights.Metrics.Extensibility;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;
using static System.Collections.Specialized.BitVector32;

namespace EditorFramework.Widgets
{
    public class InputTextWindow : SimpleTextWindow
    {
        public EditorCursor? cursor;
        public bool relativeNumbers = true;
        public long enteredLineNumber = 0;
        public bool jumpInput = false;

        public InputTextWindow(IApplication app, ILayoutManager layout, EditorBuffer buffer) : base(app, layout, buffer)
        {
            this.cursor = buffer.Cursor;
        }


        [DllImport("user32.dll", EntryPoint = "keybd_event")]
        static extern void WinapiKeybdEvent(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        public override bool HandleEvent(EventBase e)
        {
            switch (e)
            {
                case QuitEvent:
                    Environment.Exit(1);
                    return false;
                case TextInputEvent input:
                    if (!jumpInput)
                    {
                        Logger.Log($"Input text: {input.Text}");
                        /* clear all selection */
                        cursor?.Fork();
                        cursor?.Selections.DeleteString();
                        cursor?.Selections.InsertBytes(input.Text);
                        cursor?.Commit();
                    }
                    return false;
                case PasteEvent paste:
                    if (cursor != null)
                    {
                        cursor.Fork();
                        cursor.Selections.DeleteString();
                        /* check: if there is same string as if after copy -> paste using internal buffers */
                        Logger.Log("Got paste event");
                        if (IsInternalInsert(paste.Text, cursor.Selections.GetPaste()))
                        {
                            Logger.Log("internal");
                            cursor.Selections.Paste();
                        }
                        else
                        {
                            Logger.Log("external");
                            cursor.Selections.InsertString(paste.Text);
                        }
                        cursor.Commit();
                    }
                    return false;
                case KeyChordEvent key when key.LastKey.Key == KeyCode.CapsLock:
                    if (enteredLineNumber != 0)
                    {
                        long offset = enteredLineNumber;
                        if (enteredLineNumber > 50)
                        {
                            offset = enteredLineNumber - 100;
                        }
                        cursor?.Selections.MoveVertical(offset, false);
                        const int KEYEVENTF_EXTENDEDKEY = 0x1;
                        const int KEYEVENTF_KEYUP = 0x2;
                        WinapiKeybdEvent(0x14, 0x45, KEYEVENTF_EXTENDEDKEY, 0);
                        WinapiKeybdEvent(0x14, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
                    }
                    enteredLineNumber = 0;
                    jumpInput = false;
                    return false;
                case KeyChordEvent key when key.LastKey.Key == KeyCode.CapsLock:
                    jumpInput = true;
                    return false;
                case KeyChordEvent k when KeyCode.D0 <= k.LastKey.Key && k.LastKey.Key <= KeyCode.D9 && k.LastKey.Mode.HasFlag(KeyMode.CapsLock):
                    enteredLineNumber *= 10;
                    enteredLineNumber += (int)k.LastKey.Key & 0xF;
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.C, KeyMode.Ctrl) || key.Is(KeyCode.C, KeyMode.Ctrl | KeyMode.Shift):
                    if (cursor != null)
                    {
                        cursor.Selections.Copy();
                        App.SetClipboard(string.Join(Environment.NewLine, cursor.Selections.GetPaste()));
                    }
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.X, KeyMode.Ctrl) || key.Is(KeyCode.X, KeyMode.Ctrl | KeyMode.Shift):
                    cursor?.Fork();
                    cursor?.Selections.Copy();
                    cursor?.Selections.DeleteString();
                    cursor?.Commit();
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.V, KeyMode.Ctrl) || key.Is(KeyCode.V, KeyMode.Ctrl | KeyMode.Shift):
                    cursor?.Fork();
                    cursor?.Selections.DeleteString();
                    cursor?.Selections.Paste();
                    cursor?.Commit();
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Minus, KeyMode.Ctrl):
                    Layout.UpdateScale(this, 0.9);
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Equal, KeyMode.Ctrl):
                    Layout.UpdateScale(this, 1.1);
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.D, KeyMode.Ctrl):
                    if (cursor != null)
                    {
                        cursor.Fork();
                        long id = 0;
                        foreach (var x in cursor.Selections)
                        {
                            if (x.Text.Length == 0)
                            {
                                var line = x.Cursor.Buffer.GetLine(x.EndLine);
                                if (line.value != null)
                                {
                                    x.InsertString(line.offset, line.value + (line.value.EndsWith('\n') ? "" : "\n"));
                                    x.UpdateFromLineOffset();
                                }
                            }
                            else
                            {
                                long a = x.Begin, b = x.End;
                                x.InsertString(x.Min, x.Text);
                                x.UpdateFromLineOffset();
                            }
                            cursor.Selections[id] = x;
                            id++;
                        }
                        cursor.Commit();
                        return false;
                    }
                    break;
                case KeyChordEvent key when key.Is(KeyCode.A, KeyMode.Ctrl):
                    cursor?.Selections = new(cursor, [new EditorSelection(cursor, 0, cursor.Buffer.Text.Length)]);
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.W, KeyMode.Ctrl):
                    cursor?.Selections.SwapBeginEnd();
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Q, KeyMode.Ctrl):
                    cursor?.Selections.UpdateBeginToEnd();
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Z, KeyMode.Ctrl | KeyMode.Shift):
                    if (buffer.Text is IUndoTextBuffer undoText)
                    {
                        TreeWalkWindow treeWin = new (App, GetLayout<TreeWalkWindow>.Value, buffer, undoText);
                        OpenPopup(new TreeWalkWithPreviewWindow(App, GetLayout<TreeWalkWithPreviewWindow>.Value, treeWin));
                    }
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Z, KeyMode.Ctrl):
                    cursor?.Buffer.Undo();
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Y, KeyMode.Ctrl):
                    cursor?.Buffer.Redo();
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.F, KeyMode.Ctrl):
                    if (cursor != null)
                    {
                        OpenPopup(new PowerFindWindow(App, GetLayout<PowerFindWindow>.Value, cursor.Buffer.Server, cursor));
                        return false;
                    }
                    break;
                case KeyChordEvent key when key.Is(KeyCode.E, KeyMode.Ctrl | KeyMode.Shift):
                    if (cursor != null)
                    {
                        var win = new PowerEditWindow(App, GetLayout<PowerEditWindow>.Value, cursor.Buffer.Server, cursor, "powerEdit");
                        OpenPopup(new PowerEditWithPreviewWindow(App, GetLayout<PowerEditWithPreviewWindow>.Value, win));
                        return false;
                    }
                    break;
                case KeyChordEvent key when key.Is(KeyCode.E, KeyMode.Ctrl):
                    if (cursor != null)
                    {
                        var win = new PowerEditWindow(App, GetLayout<PowerEditWindow>.Value, cursor.Buffer.Server, cursor, "edit");
                        OpenPopup(new PowerEditWithPreviewWindow(App, GetLayout<PowerEditWithPreviewWindow>.Value, win));
                        return false;
                    }
                    break;
                case KeyChordEvent key when key.Is(KeyCode.R, KeyMode.Ctrl):
                    /* v.1 - open powerWindow replace */
                    if (cursor != null)
                    {
                        var win = new PowerEditWindow(App, GetLayout<PowerEditWindow>.Value, cursor.Buffer.Server, cursor, "replace");
                        OpenPopup(new PowerEditWithPreviewWindow(App, GetLayout<PowerEditWithPreviewWindow>.Value, win));
                        return false;
                    }
                    break;
                case KeyChordEvent key when key.Is(KeyCode.C, KeyMode.Alt):
                    if (cursor != null)
                    {
                        if (cursor.Selections.Count > 1)
                        {
                            cursor.Selections = new(cursor, [cursor.Selections[0]]);
                        }
                        return false;
                    }
                    break;
                case KeyChordEvent key when key.Is(KeyCode.N, KeyMode.Alt):
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
                    break;
                case KeyChordEvent key when key.Is(KeyCode.X, KeyMode.Alt):
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
                    break;
                case KeyChordEvent key when key.IsNoShift(KeyCode.Home) || key.IsNoShift(KeyCode.J, KeyMode.Alt):
                    cursor?.Selections.MoveToLineBegin(key.LastKey.Mode.HasFlag(KeyMode.Shift));
                    return false;
                case KeyChordEvent key when key.IsNoShift(KeyCode.End) || key.IsNoShift(KeyCode.Semicolon, KeyMode.Alt):
                    cursor?.Selections.MoveToLineEnd(key.LastKey.Mode.HasFlag(KeyMode.Shift));
                    return false;
                case KeyChordEvent key when key.IsNoShift(KeyCode.PageUp):
                    {
                        long? step = Layout.PageStepSize;
                        if (step != null)
                        {
                            cursor?.Selections.MoveVertical(-step.Value, key.LastKey.Mode.HasFlag(KeyMode.Shift));
                            return false;
                        }
                    }
                    break;
                case KeyChordEvent key when key.IsNoShift(KeyCode.PageDown):
                    {
                        long? step = Layout.PageStepSize;
                        if (step != null)
                        {
                            cursor?.Selections.MoveVertical(step.Value, key.LastKey.Mode.HasFlag(KeyMode.Shift));
                            return false;
                        }
                    }
                    break;
                case KeyChordEvent key when key.Is(KeyCode.Down, KeyMode.Alt):
                    if (cursor != null && cursor.Selections.Count == 1)
                    {
                        cursor.Fork();
                        long id = 0;
                        foreach (var x in cursor.Selections)
                        {
                            long MaxLine = x.MaxLine;
                            var after = x.Cursor.Buffer.GetLine(MaxLine + 1);
                            if (after.value == null)
                            {
                                id++;
                                continue;
                            }
                            x.DeleteString(after.offset, after.value.Length);
                            var before = x.Cursor.Buffer.GetLine(x.MinLine);
                            if (before.value == null)
                            {
                                id++;
                                continue;
                            }
                            x.InsertString(before.offset, after.value);
                            cursor.Selections[id] = x;
                            id++;
                        }
                        cursor.Commit();
                    }
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Up, KeyMode.Alt):
                    if (cursor != null && cursor.Selections.Count == 1)
                    {
                        cursor.Fork();
                        long id = 0;
                        foreach (var x in cursor.Selections)
                        {
                            long MinLine = x.MinLine;
                            var before = x.Cursor.Buffer.GetLine(MinLine - 1);
                            if (before.value == null)
                            {
                                id++;
                                continue;
                            }
                            x.DeleteString(before.offset, before.value.Length);
                            var after = x.Cursor.Buffer.GetLine(x.MaxLine + 1);
                            if (after.value == null)
                            {
                                id++;
                                continue;
                            }
                            x.InsertString(after.offset, before.value);
                            cursor.Selections[id] = x;
                            id++;
                        }
                        cursor.Commit();
                    }
                    return false;
                case KeyChordEvent key when key.IsNoShift(KeyCode.Down) || key.IsNoShift(KeyCode.K, KeyMode.Ctrl):
                    cursor?.Selections.MoveVertical(1, key.LastKey.Mode.HasFlag(KeyMode.Shift));
                    return false;
                case KeyChordEvent key when key.IsNoShift(KeyCode.Up) || key.IsNoShift(KeyCode.L, KeyMode.Ctrl):
                    cursor?.Selections.MoveVertical(-1, key.LastKey.Mode.HasFlag(KeyMode.Shift));
                    return false;
                case KeyChordEvent key when key.IsNoShift(KeyCode.Right, KeyMode.Ctrl) || key.IsNoShift(KeyCode.L, KeyMode.Alt):
                    cursor?.Selections.MoveHorisontalWord(1, key.LastKey.Mode.HasFlag(KeyMode.Shift));
                    return false;
                case KeyChordEvent key when key.IsNoShift(KeyCode.Left, KeyMode.Ctrl) || key.IsNoShift(KeyCode.K, KeyMode.Alt):
                    cursor?.Selections.MoveHorisontalWord(-1, key.LastKey.Mode.HasFlag(KeyMode.Shift));
                    return false;
                case KeyChordEvent key when key.IsNoShift(KeyCode.Down, KeyMode.Ctrl):
                    cursor?.Selections.MoveVertical(10, key.LastKey.Mode.HasFlag(KeyMode.Shift));
                    return false;
                case KeyChordEvent key when key.IsNoShift(KeyCode.Up, KeyMode.Ctrl):
                    cursor?.Selections.MoveVertical(-10, key.LastKey.Mode.HasFlag(KeyMode.Shift));
                    return false;
                case KeyChordEvent key when key.IsNoShift(KeyCode.Right) || key.IsNoShift(KeyCode.Semicolon, KeyMode.Ctrl):
                    cursor?.Selections.MoveHorisontal(1, key.LastKey.Mode.HasFlag(KeyMode.Shift));
                    return false;
                case KeyChordEvent key when key.IsNoShift(KeyCode.Left) || key.IsNoShift(KeyCode.J, KeyMode.Ctrl):
                    cursor?.Selections.MoveHorisontal(-1, key.LastKey.Mode.HasFlag(KeyMode.Shift));
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Tab, KeyMode.Shift):
                    if (cursor != null)
                    {
                        cursor.Fork();
                        long id = 0;
                        foreach (var x in cursor.Selections)
                        {
                            long endLine = x.MaxLine;
                            for (long line = x.MinLine; line <= endLine; ++line)
                            {
                                (long pos, string? str, _) = x.Cursor.Buffer.GetLine(line);
                                if (str != null)
                                {
                                    x.DeleteString(pos, Math.Min(4, str.TakeWhile(char.IsWhiteSpace).Count()));
                                    x.UpdateFromLineOffset();
                                }
                            }
                            cursor.Selections[id] = x;
                            id++;
                        }
                        cursor.Commit();
                    }
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Tab):
                    if (cursor != null)
                    {
                        cursor.Fork();
                        long id = 0;
                        foreach (var x in cursor.Selections)
                        {
                            if (x.TextLength > 0)
                            {
                                long endLine = x.MaxLine;
                                for (long line = x.MinLine; line <= endLine; ++line)
                                {
                                    x.InsertString(x.Cursor.Buffer.GetLine(line).offset, "    ");
                                    x.UpdateFromLineOffset();
                                }
                            }
                            else
                            {
                                x.InsertString("    ");
                            }
                            cursor.Selections[id] = x;
                            id++;
                        }
                        cursor.Commit();
                    }
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.I, KeyMode.Alt) || key.Is(KeyCode.I, KeyMode.Ctrl):
                    if (cursor != null)
                    {
                        cursor.Fork();
                        long id = 0;
                        foreach (var x in cursor.Selections)
                        {
                            if (x.TextLength == 0)
                            {
                                x.MoveToLineEnd(false);
                                x.MoveVertical(-1, true);
                                x.MoveToLineEnd(true);
                            }
                            x.DeleteString(x.Min, x.TextLength);
                            x.UpdateFromLineOffset();
                            cursor.Selections[id] = x;
                            id++;
                        }
                        cursor.Commit();
                    }
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Backspace, KeyMode.Ctrl) || key.Is(KeyCode.O, KeyMode.Alt) || key.Is(KeyCode.O, KeyMode.Ctrl):
                    if (cursor != null)
                    {
                        cursor.Fork();
                        long id = 0;
                        foreach (var x in cursor.Selections)
                        {
                            if (x.TextLength == 0)
                            {
                                x.MoveHorisontalWord(-1, true);
                            }
                            x.DeleteString(x.Min, x.TextLength);
                            x.UpdateFromLineOffset();
                            cursor.Selections[id] = x;
                            id++;
                        }
                        cursor.Commit();
                    }
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Backspace) || key.Is(KeyCode.P, KeyMode.Alt) || key.Is(KeyCode.P, KeyMode.Ctrl):
                    if (cursor != null)
                    {
                        cursor.Fork();
                        long id = 0;
                        foreach (var x in cursor.Selections)
                        {
                            if (x.TextLength == 0)
                            {
                                if (x.End >= 4 &&
                                    x.Cursor.Buffer.Text.Substring(x.End - 4, 4).All(x => x == ' '))
                                {
                                    x.DeleteString(x.End - 4, 4);
                                    x.UpdateFromLineOffset();
                                }
                                else if (x.End >= 1)
                                {
                                    x.DeleteString(x.End - 1, 1);
                                    x.UpdateFromLineOffset();
                                }
                            }
                            else
                            {
                                x.DeleteString(x.Min, x.TextLength);
                                x.UpdateFromLineOffset();
                            }
                            cursor.Selections[id] = x;
                            id++;
                        }
                        cursor.Commit();
                    }
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Delete, KeyMode.Ctrl):
                    if (cursor != null)
                    {
                        cursor.Fork();
                        long id = 0;
                        foreach (var x in cursor.Selections)
                        {
                            if (x.TextLength == 0)
                            {
                                x.MoveHorisontalWord(1, true);
                            }
                            x.DeleteString(x.Min, x.TextLength);
                            cursor.Selections[id] = x;
                            id++;
                        }
                        cursor.Commit();
                    }
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Delete):
                    if (cursor != null)
                    {
                        cursor.Fork();
                        long id = 0;
                        long textLength = cursor.Buffer.Text.Length;
                        foreach (var x in cursor.Selections)
                        {
                            if (x.TextLength == 0 && x.End < textLength)
                            {
                                x.DeleteString(x.End, 1);
                            }
                            else
                            {
                                x.DeleteString(x.Min, x.TextLength);
                            }
                            cursor.Selections[id] = x;
                            id++;
                        }
                        cursor.Commit();
                    }
                    return false;
                case KeyChordEvent key when key.Is(new KeyBindingItem(KeyCode.Enter, ModeMask:KeyMode.None)):
                    cursor?.Fork();
                    /* clear all selection */
                    cursor?.Selections.DeleteString();
                    /* find previous line with text, and use it's indent */
                    if (cursor?.Selections.Count > 1)
                    {
                        cursor?.Selections.InsertString("\n");
                    }
                    else
                    {
                        if (cursor != null)
                        {
                            long id = 0;
                            foreach (var x in cursor.Selections)
                            {
                                long line = x.EndLine;
                                string? content = null;
                                while (line >= 0 && string.IsNullOrWhiteSpace(content = x.Cursor.Buffer.GetLine(line).value))
                                {
                                    line--;
                                }
                                line++;
                                if (content != null)
                                {
                                    int indent = content.TakeWhile(char.IsWhiteSpace).Count();
                                    x.InsertString("\n" + new string(' ', indent));
                                }
                                cursor.Selections[id] = x;
                                id++;
                            }
                        }
                    }
                    cursor?.Commit();
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.N, KeyMode.Alt) || key.Is(KeyCode.N, KeyMode.Ctrl):
                    cursor?.Fork();
                    /* clear all selection */
                    cursor?.Selections.DeleteString();
                    /* find previous line with text, and use it's indent */
                    if (cursor != null)
                    {
                        long id = 0;
                        foreach (var x in cursor.Selections)
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
                                x.InsertString("\n" + new string(' ', indent));
                            }
                            cursor.Selections[id] = x;
                            id++;
                        }
                    }
                    cursor?.Commit();
                    return false;
                case KeyChordEvent key when key.Is(KeyCode.Q, KeyMode.Ctrl):
                    DeleteSelf();
                    return false;
            }
            return base.HandleEvent(e);
        }

        public static bool IsInternalInsert(string paste_data, IEnumerable<string> clip)
        {
            if (paste_data == null) return false;
            ReadOnlySpan<char> systemSpan = paste_data.AsSpan();
            int currentOffset = 0;
            string lastSeparator = Environment.NewLine;
            using var enumerator = clip.GetEnumerator();
            bool hasNext = enumerator.MoveNext();
            while (hasNext)
            {
                string currentClip = enumerator.Current ?? string.Empty;
                ReadOnlySpan<char> clipSpan = currentClip.AsSpan();
                if (currentOffset + clipSpan.Length > systemSpan.Length ||
                    !systemSpan.Slice(currentOffset, clipSpan.Length).SequenceEqual(clipSpan))
                {
                    return false;
                }
                currentOffset += clipSpan.Length;
                hasNext = enumerator.MoveNext();
                // check separator
                if (hasNext)
                {
                    if (currentOffset + lastSeparator.Length > systemSpan.Length) return false;

                    if (!systemSpan.Slice(currentOffset, lastSeparator.Length).SequenceEqual(lastSeparator.AsSpan()))
                    {
                        return false;
                    }

                    currentOffset += lastSeparator.Length;
                }
            }
            return currentOffset == systemSpan.Length;
        }

    }
}

