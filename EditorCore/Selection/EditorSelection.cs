using EditorCore.Cursor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace EditorCore.Selection
{
    public class EditorSelection
    {
        public long Begin { get; internal set; }

        public long End { get; internal set; }

        public long FromLineOffset { get; internal set; }

        public EditorCursor Cursor { get; internal set; }

        private void UpdateFromLineOffset()
        {
            long last_newline = Cursor.Buffer.Text.LastIndexOf("\n", End - 1);
            FromLineOffset = End - last_newline - 1;
        }

        public EditorSelection(EditorCursor cursor)
        {
            Begin = End = 0;
            Cursor = cursor;
            UpdateFromLineOffset();
        }

        public EditorSelection(EditorCursor cursor, long position)
        {
            Debug.Assert(position >= 0);
            Begin = End = position;
            Cursor = cursor;
            UpdateFromLineOffset();
        }

        public EditorSelection(EditorCursor cursor, long begin, long end)
        {
            Debug.Assert(begin >= 0);
            Debug.Assert(end >= 0);
            Begin = begin;
            End = end;
            Cursor = cursor;
            UpdateFromLineOffset();
        }

        public void SetPosition(long position)
        {
            Begin = End = position;
        }

        public void SetPosition(long begin, long end)
        {
            Debug.Assert(begin >= 0);
            Debug.Assert(end >= 0);
            Begin = begin;
            End = end;
            UpdateFromLineOffset();
        }

        /* declarations for simplicity */
        public static implicit operator string?(EditorSelection selection)
        {
            return selection.Text?.ToString();
        }

        public void InsertText(string text)
        {
            Cursor.Buffer.InsertString(End, text);
            UpdateFromLineOffset();
        }

        public void MoveHorisontal(long offset, bool withSelect = false)
        {
            End += offset;
            if (End < 0)
            {
                End = 0;
            }
            if (End > Cursor.Buffer.Text.Length)
            {
                End = Cursor.Buffer.Text.Length;
            }
            if (!withSelect)
            {
                Begin = End;
            }
            UpdateFromLineOffset();
        }

        public void MoveVertical(long offset, bool withSelect = false)
        {
            if (offset < 0)
            {
                offset = -offset;
                for (long i = 0; i < offset; i++)
                {
                    long endOfPrevLine = Cursor.Buffer.Text.LastIndexOf('\n', (int)(End - 1));
                    if (endOfPrevLine == -1)
                    {
                        /* uncomment to jump to begin of file */
                        // End = 0;
                        goto update_begin_pointer;
                    }
                    long endOfBeforePrevLine = Cursor.Buffer.Text.LastIndexOf('\n', (int)(endOfPrevLine - 1));
                    End = Math.Min(endOfPrevLine, endOfBeforePrevLine + 1 + FromLineOffset);
                }
            }
            else
            {
                for (long i = 0; i < offset; i++)
                {
                    long nextLine = Cursor.Buffer.Text.IndexOf('\n', (int)End);
                    if (nextLine == -1)
                    {
                        /* uncomment to jump to end of file */
                        // End = Cursor.File.Text.Length;
                        goto update_begin_pointer;
                    }
                    long afterNextLine = Cursor.Buffer.Text.IndexOf('\n', (int)(nextLine + 1));
                    if (afterNextLine == -1)
                    {
                        afterNextLine = Cursor.Buffer.Text.Length;
                    }
                    End = Math.Min(nextLine + 1 + FromLineOffset, afterNextLine);
                }
            }
        update_begin_pointer:
            if (!withSelect)
            {
                Begin = End;
            }
        }

        public long Min => Math.Min(Begin, End);

        public long Max => Math.Max(Begin, End);

        public long Length => End - Begin;

        public long TextLength => Max - Min;

        public Rope.Rope<char>? Text => (TextLength == 0 ? null : Cursor?.Buffer?.Text.Slice(Min, TextLength));
    }
}
