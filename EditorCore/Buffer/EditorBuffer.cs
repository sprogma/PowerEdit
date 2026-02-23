using EditorCore.Selection;
using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public TextBuffer.TextBuffer Text { get; internal set; }
        public Server.EditorServer Server { get; internal set; }
        public Cursor.EditorCursor? Cursor { get; internal set; }
        public BaseTokenizer Tokenizer { get; internal set; }
        public List<Token> Tokens { get; internal set; } = [];

        public EditorBuffer(Server.EditorServer server, BaseTokenizer tokenizer)
        {
            Tokenizer = tokenizer;
            Text = new();
            Cursor = new(this);
            Cursor.Selections.Add(new EditorSelection(Cursor, 0));
            Server = server;
            SaveCursorState();

            OnUpdate();
        }

        public EditorBuffer(Server.EditorServer server, string content, BaseTokenizer tokenizer)
        {
            Tokenizer = tokenizer;
            Text = new();
            Cursor = new(this);
            Cursor.Selections.Add(new EditorSelection(Cursor, 0));
            Server = server;
            SaveCursorState();

            Text.Insert(0, content);
            SaveCursorState();

            OnUpdate();
        }

        public void SaveCursorState()
        {
            if (Cursor == null)
            {
                return;
            }
            Text.SaveCursors(Text.GetCurrentVersion(), Cursor.Selections.Select(x => new MarshalingCursor(x.Begin, x.End)).ToArray());
        }

        public void LoadCursorState()
        {
            if (Cursor == null)
            {
                return;
            }
            var ver = Text.GetCurrentVersion();
            var cursors = Text.GetCursors(ver);
            Cursor.Selections = cursors.Select(x => new EditorSelection(Cursor, x.Begin, x.End)).ToList();
        }

        public void OnUpdate(bool pushHistory = true)
        {
            if (Cursor == null)
            {
                return;
            }
            ActionOnUpdate?.Invoke(this);
            Tokens = Tokenizer.ParseContent(Text.Substring(0));
            if (pushHistory)
            {
                Text.PushHistory();
            }
        }

        public void OnSimpleUpdate()
        {
        }

        public void Undo()
        {
            if (Cursor == null)
            {
                return;
            }
            SaveCursorState();
            Text.Undo();
            LoadCursorState();

            //Cursor.Selections = selections.Select(x => new EditorSelection(Cursor, x.Item1, x.Item2)).ToList();
            Tokens = Tokenizer.ParseContent(Text.Substring(0));
        }

        public void Redo()
        {
            if (Cursor == null)
            {
                return;
            }
            if (Text.Redo())
            {
                return;
            }
            LoadCursorState();

            //(Text, var selections) = RedoHistory.Last.Value;
            //Cursor.Selections = selections.Select(x => new EditorSelection(Cursor, x.Item1, x.Item2)).ToList();
            Tokens = Tokenizer.ParseContent(Text.Substring(0));
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
            long length = Text.Insert(position, data);
            MoveCursors(position, length);
            return position + length;
        }

        public long InsertBytes(long position, byte[] data)
        {
            long length = Text.InsertBytes(position, data);
            MoveCursors(position, length);
            return position + length;
        }

        public void DeleteString(long position, long count)
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
            Text.RemoveAt(position, count);
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

        /* declarations for simplicity */
        public long SetText(string data)
        {
            DeleteString(0, Text.Length);
            long res = InsertString(0, data);
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
