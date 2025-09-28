using EditorCore.Selection;
using RegexTokenizer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace EditorCore.Buffer
{
    public delegate void EditorBufferOnUpdate(EditorBuffer buffer);
    public class EditorBuffer
    {
        public EditorBufferOnUpdate? ActionOnUpdate = null;

        public const long MaxHistorySize = 1024;

        public SortedList<long, long> LinePositions { get; internal set; } = [];
        public LinkedList<(Rope.Rope<char>, (long, long)[])> History { get; internal set; } = [];
        public LinkedList<(Rope.Rope<char>, (long, long)[])> RedoHistory { get; internal set; } = [];
        public Rope.Rope<char> Text { get; internal set; }
        public Server.EditorServer Server { get; internal set; }
        public Cursor.EditorCursor? Cursor { get; internal set; }
        public BaseTokenizer Tokenizer { get; internal set; }
        public List<Token> Tokens { get; internal set; } = [];

        public EditorBuffer(Server.EditorServer server, BaseTokenizer tokenizer)
        {
            Tokenizer = tokenizer;
            Text = "";
            Cursor = new(this);
            Cursor.Selections.Add(new EditorSelection(Cursor, 0));
            Server = server;

            OnUpdate();
        }

        public EditorBuffer(Server.EditorServer server, Rope.Rope<char> content, BaseTokenizer tokenizer)
        {
            Tokenizer = tokenizer;
            Text = content;
            Cursor = new(this);
            Cursor.Selections.Add(new EditorSelection(Cursor, 0));
            Server = server;

            OnUpdate();
        }

        public void OnUpdate(bool pushHistory = true)
        {
            if (Cursor == null)
            {
                return;
            }
            ActionOnUpdate?.Invoke(this);
            Tokens = Tokenizer.ParseContent(Text);
            if (pushHistory)
            {
                History.AddLast((Text, Cursor.Selections.Select(x => (x.Begin, x.End)).ToArray()));
                while (History.Count > MaxHistorySize)
                {
                    History.RemoveFirst();
                }
            }
            RedoHistory.Clear();
        }

        public void OnSimpleUpdate()
        {
        }

        public void Undo()
        {
            if (History.Last == null || Cursor == null)
            {
                return;
            }
            RedoHistory.AddLast(History.Last.Value);
            while (RedoHistory.Count > MaxHistorySize)
            {
                RedoHistory.RemoveFirst();
            }
            (Text, var selections) = History.Last.Value;
            Cursor.Selections = selections.Select(x => new EditorSelection(Cursor, x.Item1, x.Item2)).ToList();
            Tokens = Tokenizer.ParseContent(Text);
            History.RemoveLast();
        }

        public void Redo()
        {
            if (RedoHistory.Last == null || Cursor == null)
            {
                return;
            }
            History.AddLast(RedoHistory.Last.Value);
            while (History.Count > MaxHistorySize)
            {
                History.RemoveFirst();
            }
            (Text, var selections) = RedoHistory.Last.Value;
            Cursor.Selections = selections.Select(x => new EditorSelection(Cursor, x.Item1, x.Item2)).ToList();
            Tokens = Tokenizer.ParseContent(Text);
            RedoHistory.RemoveLast();
        }

        public long InsertString(long position, Rope.Rope<char> data)
        {
            long length = data.Length;
            Text = Text.InsertRange(position, data);
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
            OnSimpleUpdate();
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
            Text = Text.RemoveRange(position, count);
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

        public (long, Rope.Rope<char>?) GetLine(long line)
        {
            Rope.Rope<char> text = Text;

            if (line < 0)
            {
                return (0, null);
            }

            long index = 0;
            for (long i = 0; i < line; ++i)
            {
                if (index >= text.Length)
                {
                    return (0, null);
                }
                index = text.IndexOf('\n', index);
                if (index == -1)
                {
                    return (0, null);
                }
                index++;
            }

            long end = text.IndexOf('\n', index);
            if (end == -1)
            {
                return (index, text.Slice(index));
            }
            return (index, text.Slice(index, end - index + 1));
        }
        public (long, long) GetPositionOffsets(long position)
        {
            Debug.Assert(position >= 0);
            long line = Text.Slice(0, position).Count(x => x == '\n');
            long last_newline = Text.LastIndexOf("\n", position - 1);
            long offset = position - last_newline - 1;
            return (line, offset);
        }
    }
}
