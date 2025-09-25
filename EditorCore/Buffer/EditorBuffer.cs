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
    public class EditorBuffer
    {
        public Rope.Rope<char> Text { get; internal set; }
        public Server.EditorServer Server { get; internal set; }
        public List<Cursor.EditorCursor> Cursors { get; internal set; }
        public BaseTokenizer Tokenizer { get; internal set; }
        public List<Token> Tokens { get; internal set; }

        public EditorBuffer(Server.EditorServer server, BaseTokenizer tokenizer)
        {
            Tokens = [];
            Tokenizer = tokenizer;
            Text = "";
            Cursors = [];
            Server = server;
        }

        public EditorBuffer(Server.EditorServer server, Rope.Rope<char> content, BaseTokenizer tokenizer)
        {
            Tokens = [];
            Tokenizer = tokenizer;
            Text = content;
            Cursors = [];
            Server = server;
        }

        public Cursor.EditorCursor CreateCursor()
        {
            Cursor.EditorCursor new_cursor = new Cursor.EditorCursor(this);
            Cursors.Add(new_cursor);
            return new_cursor;
        }

        public void OnUpdate()
        {
            Tokens = Tokenizer.ParseContent(Text);
            Console.WriteLine("Updated");
        }

        public long InsertString(long position, Rope.Rope<char> data)
        {
            long length = data.Length;
            Text = Text.InsertRange(position, data);
            /* move all cursors */
            foreach (var cursor in Cursors)
            {
                foreach (var selection in cursor.Selections)
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
            OnUpdate();
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
            foreach (var cursor in Cursors)
            {
                foreach (var selection in cursor.Selections)
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
            OnUpdate();
        }

        /* declarations for simplicity */
        public long SetText(string data)
        {
            DeleteString(0, Text.Length);
            long res = InsertString(0, data);
            return res;
        }

        public (long, Rope.Rope<char>?) GetLine(long line)
        {
            if (line < 0)
            {
                return (0, null);
            }

            long index = 0;
            for (long i = 0; i < line; ++i)
            {
                index = Text.IndexOf('\n', index);
                if (index == -1)
                {
                    return (0, null);
                }
                index++;
            }

            long end = Text.IndexOf('\n', index);
            if (end == -1)
            {
                return (index, Text.Slice(index));
            }
            return (index, Text.Slice(index, end - index + 1));
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
