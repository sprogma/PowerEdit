using EditorCore.Cursor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EditorCore.Selection
{
    public class EditorSelection
    {
        public long Begin { get; internal set; }

        public long End { get; internal set; }

        public EditorCursor Cursor { get; internal set; }

        public EditorSelection(EditorCursor cursor)
        {
            Begin = End = 0;
            Cursor = cursor;
        }

        public EditorSelection(EditorCursor cursor, long position)
        {
            Debug.Assert(End >= Begin);
            Begin = End = position;
            Cursor = cursor;
        }

        public EditorSelection(EditorCursor cursor, long begin, long end)
        {
            Begin = begin;
            End = end;
            Cursor = cursor;
        }

        public void SetPosition(long position)
        {
            Begin = End = position;
        }

        public void SetPosition(long begin, long end)
        {
            Debug.Assert(End >= Begin);
            Begin = begin;
            End = end;
        }

        /* declarations for simplicity */

        public void InsertText(string text)
        {
            Cursor.File.InsertString(Begin, text);
        }

        public long Length { get { Debug.Assert(End >= Begin); return End - Begin; } } 

        public Rope.Rope<char>? Text => (Length > 0 ? Cursor?.File?.Text.Slice(Begin, Length) : null);
    }
}
