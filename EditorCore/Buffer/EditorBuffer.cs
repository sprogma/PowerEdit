using EditorCore.Selection;
using Lsp;
using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TextBuffer;

namespace EditorCore.Buffer
{
    public delegate void EditorBufferOnUpdate(EditorBuffer buffer);
    public class EditorBuffer
    {
        public EditorBufferOnUpdate? ActionOnUpdate = null;

        public const long MaxHistorySize = 1024;

        public ITextBuffer Text { get; internal set; }
        public Server.EditorServer Server { get; internal set; }
        public Cursor.EditorCursor? Cursor { get; internal set; }
        public BaseTokenizer Tokenizer { get; internal set; }
        public List<Token> Tokens { get; internal set; } = [];
        public LspClient? Client { get; internal set; }
        public string FilePath { get; internal set; }

        public EditorBuffer(Server.EditorServer server, BaseTokenizer tokenizer, LspClient? client, string filepath, ITextBuffer buffer)
        {
            Tokenizer = tokenizer;
            Text = buffer;
            FilePath = filepath;
            Client = client;
            Cursor = new(this);
            Cursor.Selections.Add(new EditorSelection(Cursor, 0));
            Server = server;
            SaveCursorState();

            OnUpdate();
        }

        public EditorBuffer(Server.EditorServer server, string content, BaseTokenizer tokenizer, LspClient? client, string filepath, ITextBuffer buffer)
        {
            Tokenizer = tokenizer;
            Text = buffer;
            FilePath = filepath;
            Client = client;
            Cursor = new(this);
            Cursor.Selections.Add(new EditorSelection(Cursor, 0));
            Server = server;
            SaveCursorState();

            Text.SetText(content);
            SaveCursorState();

            OnUpdate();
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
                Cursor.Selections = cursors.Select(x => new EditorSelection(Cursor, x.Begin, x.End)).ToList();
            }
        }

        public void OnUpdate(bool pushHistory = true)
        {
            if (Cursor == null)
            {
                return;
            }
            ActionOnUpdate?.Invoke(this);
            _ = Task.Run(() => Client?.ChangeFileAsync("aboba/aboba", Text.Substring(0)));
            Tokens = Tokenizer.ParseContent(Text.Substring(0));
        }

        public void OnSimpleUpdate()
        {
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

                //Cursor.Selections = selections.Select(x => new EditorSelection(Cursor, x.Item1, x.Item2)).ToList();
                Tokens = Tokenizer.ParseContent(Text.Substring(0));
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
                if (undoText.Redo())
                {
                    return;
                }
                LoadCursorState();

                //(Text, var selections) = RedoHistory.Last.Value;
                //Cursor.Selections = selections.Select(x => new EditorSelection(Cursor, x.Item1, x.Item2)).ToList();
                Tokens = Tokenizer.ParseContent(Text.Substring(0));
            }
        }


        public void MoveCursors(long position, long length)
        {
            /* move all cursors */
            if (Cursor != null)
            {
                foreach (var selection in Cursor.Selections)
                {
                    if (selection.Begin >= position)
                    {
                        selection.Begin += length;
                    }
                    if (selection.End >= position)
                    {
                        selection.End += length;
                    }
                }
            }
            SaveCursorState();
            OnSimpleUpdate();
        }

        public long InsertString(long position, string data)
        {
            if (Text is IEditableTextBuffer editableText)
            {
                long length = editableText.Insert(position, data);
                MoveCursors(position, length);
                return position + length;
            }
            return position;
        }

        public long InsertBytes(long position, byte[] data)
        {
            if (Text is IEditableTextBuffer editableText)
            {
                long length = editableText.Insert(position, data);
                MoveCursors(position, length);
                return position + length;
            }
            return position;
        }

        public void DeleteString(long position, long count)
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
                /* move all cursors */
                if (Cursor != null)
                {
                    foreach (var selection in Cursor.Selections)
                    {
                        if (selection.Begin >= position + count)
                        {
                            selection.Begin -= count;
                        }
                        else if (selection.Begin >= position)
                        {
                            selection.Begin = position;
                        }
                        if (selection.End >= position + count)
                        {
                            selection.End -= count;
                        }
                        else if (selection.End >= position)
                        {
                            selection.End = position;
                        }
                    }
                }
                SaveCursorState();
                OnSimpleUpdate();
            }
        }

        public long SetText(string data)
        {
            long res = Text.SetText(data);
            OnUpdate();
            return res;
        }

        public (long offset, string? value, long length) GetLine(long line)
        {
            return Text.GetLine(line);
        }

        public (long, long) GetPositionOffsets(long position)
        {
            Debug.Assert(position >= 0);
            return Text.GetPositionOffsets(position);
        }
    }
}
