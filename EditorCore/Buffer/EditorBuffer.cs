using EditorCore.File;
using EditorCore.Selection;
using Lsp;
using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;

namespace EditorCore.Buffer
{
    public struct ErrorMark(string message, long position)
    {
        public string message = message;
        public long position = position;
    }

    public delegate void EditorBufferOnUpdate(EditorBuffer buffer);
    public delegate bool EditorBufferOnTextInput(EditorBuffer buffer);
    public class EditorBuffer
    {
        public EditorBufferOnUpdate? ActionOnUpdate = null;
        public EditorBufferOnTextInput? ActionOnTextInput = null;

        public const long MaxHistorySize = 1024;

        public ITextBuffer Text { get; internal set; }
        public Server.EditorServer Server { get; internal set; }
        public Cursor.EditorCursor? Cursor { get; internal set; }
        public BaseTokenizer Tokenizer { get; internal set; }
        public List<Token> Tokens { get; internal set; } = [];
        public List<ErrorMark> ErrorMarks { get; internal set; } = [];
        public LspClient? Client { get; internal set; }
        public string? FilePath { get; internal set; }

        private IntPtr? last_saved_version = null;
        private bool dirty_was_changed = false;

        public bool WasChanged
        {
            get
            {
                if (Text is IUndoTextBuffer undoText)
                {
                    return last_saved_version == null || undoText.GetCurrentVersion() != undoText.ResolveVersion(last_saved_version.Value);
                }
                return dirty_was_changed;
            }
            set
            {
                dirty_was_changed = value;
                if (Text is IUndoTextBuffer undoText)
                {
                    last_saved_version = (value ? null : undoText.GetCurrentVersion());
                }
            }
        }

        public EditorBuffer(Server.EditorServer server, BaseTokenizer tokenizer, LspClient? client, string? filepath, ITextBuffer buffer)
        {
            Tokenizer = tokenizer;
            Text = buffer;
            FilePath = filepath;
            Client = client;
            Cursor = new(this);
            Cursor.Selections.Add(new EditorSelection(Cursor, 0));
            Server = server;
            SaveCursorState();

            ActionOnUpdate += server.ActionOnBufferUpdate;
            ActionOnTextInput += server.ActionOnBufferTextInput;

            OnUpdate();
        }

        public EditorBuffer(Server.EditorServer server, string content, BaseTokenizer tokenizer, LspClient? client, string? filepath, ITextBuffer buffer)
        {
            Tokenizer = tokenizer;
            Text = buffer;
            FilePath = filepath;
            Client = client;
            Cursor = new(this);
            Cursor.Selections.Add(new EditorSelection(Cursor, 0));
            Server = server;
            SaveCursorState();

            SetText(content);
        }

        public void SaveCursorState()
        {
            if (Text is INavigatableTextBuffer navText && Text is IUndoTextBuffer undoText)
            {
                if (Cursor == null)
                {
                    return;
                }
                navText.SaveCursors(undoText.GetCurrentVersion(), Cursor.Selections.Select(x => new MarshalingCursor(x.Begin, x.End)).ToArray());
            }
        }

        public void LoadCursorState()
        {
            if (Text is INavigatableTextBuffer navText && Text is IUndoTextBuffer undoText)
            {
                if (Cursor == null)
                {
                    return;
                }
                var ver = undoText.GetCurrentVersion();
                var cursors = navText.GetCursors(ver);
                Cursor.Selections = new(Cursor, cursors.Select(x => new EditorSelection(Cursor, x.Begin, x.End)).ToArray());
            }
        }

        internal void OnUpdate(bool pushHistory = true)
        {
            if (Cursor == null)
            {
                return;
            }
            ActionOnUpdate?.Invoke(this);
            _ = Task.Run(() => Client?.ChangeFileAsync("aboba/aboba", Text.Substring(0)));
            _ = Task.Run(() => Tokens = Tokenizer.ParseContent(Text.Substring(0)));
            dirty_was_changed = true;
        }

        public void Undo()
        {
            if (Text is IUndoTextBuffer undoText)
            {
                if (Cursor == null)
                {
                    return;
                }
                SaveCursorState();
                undoText.Undo();
                LoadCursorState();

                OnUpdate();
            }
        }

        public void Redo()
        {
            if (Text is IUndoTextBuffer undoText)
            {
                if (Cursor == null)
                {
                    return;
                }
                if (!undoText.Redo())
                {
                    return;
                }
                LoadCursorState();

                OnUpdate();
            }
        }


        private void MoveCursorsInsert(long position, long length)
        {
            /* move all cursors */
            if (Cursor != null)
            {
                Cursor.Selections.MoveInsert(position, length);
            }
            lock (ErrorMarks)
            {
                Span<ErrorMark> span = CollectionsMarshal.AsSpan(ErrorMarks);
                for (int i = 0; i < span.Length; i++)
                {
                    ref var err = ref span[i];
                    if (err.position >= position)
                    {
                        err.position += length;
                    }
                }
            }
        }

        internal long InsertString(long position, string data)
        {
            if (Text is IEditableTextBuffer editableText)
            {
                long length = editableText.Insert(position, data);
                MoveCursorsInsert(position, length);
                return length;
            }
            return 0;
        }

        internal long InsertBytes(long position, byte[] data)
        {
            if (Text is IEditableTextBuffer editableText)
            {
                long length = editableText.Insert(position, data);
                MoveCursorsInsert(position, length);
                return length;
            }
            return 0;
        }

        internal void DeleteString(long position, long count)
        {
            if (Text is IEditableTextBuffer editableText)
            {
                if (position + count <= 0)
                {
                    return;
                }
                if (position < 0)
                {
                    count += position;
                    position = 0;
                }
                editableText.RemoveAt(position, count);
                MoveCursorsDelete(position, count);
            }
        }

        private void MoveCursorsDelete(long position, long length)
        {

            /* move all cursors */
            if (Cursor != null)
            {
                Cursor.Selections.MoveDelete(position, length);
            }
            lock (ErrorMarks)
            {
                Span<ErrorMark> span = CollectionsMarshal.AsSpan(ErrorMarks);
                for (int i = 0; i < span.Length; i++)
                {
                    ref var err = ref span[i];
                    if (err.position >= position + length)
                    {
                        err.position -= length;
                    }
                    else if (err.position >= position)
                    {
                        err.position = position;
                    }
                }
            }
        }

        public long SetText(string data)
        {
            return Text.SetText(data);
        }

        public (long offset, string? value, long length) GetLine(long line)
        {
            return Text.GetLine(line);
        }

        public (long line, long offset) GetPositionOffsets(long position)
        {
            Debug.Assert(position >= 0);
            return Text.GetPositionOffsets(position);
        }

        public long GetPosition(long line, long col)
        {
            return Text.GetPosition(line, col);
        }

        public (long begin, long length) GetLineOffsets(long line)
        {
            return Text.GetLineOffsets(line);
        }

        public void Commit()
        {
            if (Text is IEditableTextBuffer editableText)
            {
                editableText.Commit();
            }
            SaveCursorState();
            OnUpdate();
        }

        internal void Fork()
        {
            if (Text is IEditableTextBuffer editableText)
            {
                editableText.Fork();
            }
        }

        public void SetVersion(nint id)
        {
            if (Text is IUndoTextBuffer undoText)
            {
                undoText.SetVersion(id);
                LoadCursorState();
                OnUpdate();
            }
        }
    }
}
