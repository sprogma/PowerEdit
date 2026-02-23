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

        public string? clipboard { get; internal set; } = null;

        public void UpdateFromLineOffset()
        {
            long last_newline = Cursor.Buffer.Text.NearestNewlineLeft(End - 1);
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

        public override string ToString()
        {
            return $"Selection[{Begin}:{End}]";
        }

        public void Copy()
        {
            clipboard = Text;
        }

        public void Paste()
        {
            if (clipboard != null)
            {
                InsertText(clipboard);
            }
        }

        public long BeginLine => Cursor.Buffer.GetPositionOffsets(Begin).Item1;
        public long EndLine => Cursor.Buffer.GetPositionOffsets(End).Item1;
        public long MinLine => Cursor.Buffer.GetPositionOffsets(Min).Item1;
        public long MaxLine => Cursor.Buffer.GetPositionOffsets(Max).Item1;

        public long InsertText(string text)
        {
            long res = Cursor.Buffer.InsertString(End, text);
            UpdateFromLineOffset();
            return res;
        }

        public long InsertBytes(byte[] text)
        {
            long res = Cursor.Buffer.InsertBytes(End, text);
            UpdateFromLineOffset();
            return res;
        }

        public void MoveToLineBegin(bool withSelect = false)
        {
            var res = Cursor.Buffer.GetLine(EndLine);
            if (res.Item2 == null)
            {
                return;
            }
            string str = res.Item2;
            long textBegin = res.Item1;
            if (!string.IsNullOrWhiteSpace(str))
            {
                textBegin = res.Item1 + str.TakeWhile(char.IsWhiteSpace).Count();
            }
            if (End == textBegin)
            {
                End = res.Item1;
            }
            else
            {
                End = textBegin;
            }
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

        public void MoveToLineEnd(bool withSelect = false)
        {
            var res = Cursor.Buffer.GetLine(EndLine);
            if (res.Item2 == null)
            {
                return;
            }
            End = res.Item1 + res.Item3 - (res.Item2.EndsWith("\n") ? 1 : 0);
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

        public void MoveHorisontalWord(long offset, bool withSelect = false)
        {
            if (offset > 0)
            {
                for (long i = 0; i < offset; ++i)
                {
                    if (End >= Cursor.Buffer.Text.Length)
                    {
                        End = Cursor.Buffer.Text.Length;
                        break;
                    }
                    if (Cursor.Buffer.Text[End] == '\n')
                    {
                        MoveHorisontal(1, withSelect);
                    }
                    else
                    {
                        long pos = End;
                        bool wasAlpha = char.IsLetterOrDigit(Cursor.Buffer.Text[pos]) || Cursor.Buffer.Text[pos] == '_';
                        while (pos < Cursor.Buffer.Text.Length && 
                               wasAlpha == (char.IsLetterOrDigit(Cursor.Buffer.Text[pos]) || Cursor.Buffer.Text[pos] == '_') &&
                               Cursor.Buffer.Text[pos] != '\n')
                        {
                            pos++;
                        }
                        End = pos;
                    }
                }
            }
            else
            {
                offset = -offset;
                for (long i = 0; i < offset; ++i)
                {
                    if (End <= 0)
                    {
                        End = 0;
                        break;
                    }
                    if (End != 0 && Cursor.Buffer.Text[End - 1] == '\n')
                    {
                        MoveHorisontal(-1, withSelect);
                    }
                    else
                    {
                        long pos = End - 1;
                        bool wasAlpha = char.IsLetterOrDigit(Cursor.Buffer.Text[pos]) || Cursor.Buffer.Text[pos] == '_';
                        while (pos >= 0 &&
                               wasAlpha == (char.IsLetterOrDigit(Cursor.Buffer.Text[pos]) || Cursor.Buffer.Text[pos] == '_') &&
                               Cursor.Buffer.Text[pos] != '\n')
                        {
                            pos--;
                        }
                        End = pos + 1;
                    }
                }
            }
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
            if (withSelect == false)
            {
                Begin = End = (offset < 0 ? Min : Max);
            }
            if (offset < 0)
            {
                offset = -offset;
                for (long i = 0; i < offset; i++)
                {
                    long endOfPrevLine = Cursor.Buffer.Text.NearestNewlineLeft(End - 1);
                    if (endOfPrevLine == -1)
                    {
                        End = 0;
                        goto update_begin_pointer;
                    }
                    long endOfBeforePrevLine = Cursor.Buffer.Text.NearestNewlineLeft(endOfPrevLine - 1);
                    End = Math.Min(endOfPrevLine, endOfBeforePrevLine + 1 + FromLineOffset);
                }
            }
            else
            {
                for (long i = 0; i < offset; i++)
                {
                    long nextLine = Cursor.Buffer.Text.NearestNewlineRight(End);
                    if (nextLine == -1)
                    {
                        End = Cursor.Buffer.Text.Length;
                        goto update_begin_pointer;
                    }
                    long afterNextLine = Cursor.Buffer.Text.NearestNewlineRight(nextLine + 1);
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

        public string Text => (TextLength == 0 ? "" : Cursor.Buffer.Text.Substring(Min, TextLength));
    }
}
