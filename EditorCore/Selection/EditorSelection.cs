using EditorCore.Buffer;
using EditorCore.Cursor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;

namespace EditorCore.Selection
{
    internal class OrderedMaxTreap
    {
        private class Node
        {
            public long Value;
            public long Priority;

            public long LazyAdd = 0;
            public long? LazySet = null;

            public Node? Left, Right, Parent;

            public Node(long val)
            {
                Value = val;
                Priority = new Random().NextInt64();
            }
        }

        private Node? root;
        private List<Node> nodes = [];

        public long this[long index]
        {
            get => Get((int)index);
            set => Set((int)index, value);
        }

        public long Get(int index)
        {
            Node target = nodes[index];
            PushPathFromRoot(target);
            return target.Value;
        }

        public void Set(int index, long newValue)
        {
            Node target = nodes[index];
            RemoveNode(target);
            target.Value = newValue;
            target.LazyAdd = 0;
            target.LazySet = null;
            root = Insert(root, target);
        }

        public void Insert(int pos, long val)
        {
            var newNode = new Node(val);
            nodes.Insert(pos, newNode);
            root = Insert(root, newNode);
        }

        public void Add(long x, long v)
        {
            var (low, hi) = Split(root, x);
            if (hi != null)
            {
                ApplyAdd(hi, v);
            }
            root = Merge(low, hi);
        }

        public void Sub(long x, long v)
        {
            var (low, hi) = Split(root, x);
            var (floor, sub) = Split(hi, x + v);
            if (floor != null) ApplySet(floor, x);
            if (sub != null) ApplyAdd(sub, -v);

            root = Merge(low, Merge(floor, sub));
        }


        private void ApplyAdd(Node? t, long v)
        {
            if (t == null) return;
            if (t.LazySet != null) t.LazySet += v;
            else t.LazyAdd += v;
        }

        private void ApplySet(Node? t, long v)
        {
            if (t == null) return;
            t.LazySet = v;
            t.LazyAdd = 0;
        }

        private void Push(Node? t)
        {
            if (t == null) return;
            else if (t.LazySet != null)
            {
                t.Value = t.LazySet.Value;
                ApplySet(t.Left, t.LazySet.Value);
                ApplySet(t.Right, t.LazySet.Value);
                t.LazySet = null;
            }
            else if (t.LazyAdd != 0)
            {
                t.Value += t.LazyAdd;
                ApplyAdd(t.Left, t.LazyAdd);
                ApplyAdd(t.Right, t.LazyAdd);
                t.LazyAdd = 0;
            }
        }

        private void PushPathFromRoot(Node? t)
        {
            Stack<Node> path = [];
            Node? curr = t;
            while (curr != null) { path.Push(curr); curr = curr.Parent; }
            while (path.Count > 0) Push(path.Pop());
        }

        private (Node?, Node?) Split(Node? t, long key)
        {
            if (t == null) return (null, null);
            Push(t);
            if (t.Value <= key)
            {
                var (l, r) = Split(t.Right, key);
                t.Right = l;
                l?.Parent = t;
                r?.Parent = null;
                return (t, r);
            }
            else
            {
                var (l, r) = Split(t.Left, key);
                t.Left = r;
                r?.Parent = t;
                l?.Parent = null;
                return (l, t);
            }
        }

        private Node? Merge(Node? l, Node? r)
        {
            if (l == null || r == null) return l ?? r;
            Push(l); 
            Push(r);
            if (l.Priority > r.Priority)
            {
                l.Right = Merge(l.Right, r);
                l.Right?.Parent = l;
                return l;
            }
            else
            {
                r.Left = Merge(l, r.Left);
                r.Left?.Parent = r;
                return r;
            }
        }

        private Node? Insert(Node? r, Node node)
        {
            var (l, ri) = Split(r, node.Value);
            node.Left = node.Right = node.Parent = null;
            return Merge(Merge(l, node), ri);
        }

        private void RemoveNode(Node node)
        {
            PushPathFromRoot(node);
            Node? m = Merge(node.Left, node.Right);
            if (node.Parent == null) 
                root = m;
            else if (node.Parent.Left == node) 
                node.Parent.Left = m;
            else 
                node.Parent.Right = m;

            m?.Parent = node.Parent;
            node.Left = node.Right = node.Parent = null;
        }
    }


    public class EditorSelectionList : IEnumerable<EditorSelection>
    {
        OrderedMaxTreap End;
        OrderedMaxTreap Begin;
        OrderedMaxTreap FromLineOffset;
        List<string?> Clipboards;
        long size;
        
        EditorCursor Cursor;

        public EditorSelectionList(EditorCursor cursor)
        {
            End = new();
            Begin = new();
            FromLineOffset = new();
            Clipboards = [];
            size = 0;
            Cursor = cursor;
        }

        public EditorSelectionList(EditorCursor cursor, IEnumerable<EditorSelection> data)
        {
            End = new();
            Begin = new();
            FromLineOffset = new();
            Clipboards = [];
            size = 0;
            Cursor = cursor;
            foreach (var x in data)
            {
                End.Insert((int)size, x.End);
                Begin.Insert((int)size, x.Begin);
                FromLineOffset.Insert((int)size, x.FromLineOffset);
                Clipboards.Insert((int)size, x.Clipboard);
                size++;
            }
        }

        public void Insert(long index, EditorSelection selection)
        {
            size++;
            End.Insert((int)index, selection.End);
            Begin.Insert((int)index, selection.Begin);
            FromLineOffset.Insert((int)index, selection.FromLineOffset);
            Clipboards.Insert((int)index, selection.Clipboard);
        }

        public long Count => size;

        public IEnumerator<EditorSelection> GetEnumerator()
        {
            for (long i = 0; i < size; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Clear()
        {
            End = new();
            Begin = new();
            FromLineOffset = new();
            Clipboards = [];
            size = 0;
        }

        public void UpdateFromOffset()
        {
            for (int i = 0; i < size; ++i)
            {
                var tmp = this[i];
                tmp.UpdateFromLineOffset();
                this[i] = tmp;
            }
        }

        public void MoveVertical(long offset, bool v)
        {
            for (int i = 0; i < size; ++i)
            {
                var tmp = this[i];
                tmp.MoveVertical(offset, v);
                this[i] = tmp;
            }
        }

        public void MoveHorisontal(long offset, bool v)
        {
            for (int i = 0; i < size; ++i)
            {
                var tmp = this[i];
                tmp.MoveHorisontal(offset, v);
                this[i] = tmp;
            }
        }

        public void MoveHorisontalWord(long offset, bool v)
        {
            for (int i = 0; i < size; ++i)
            {
                var tmp = this[i];
                tmp.MoveHorisontalWord(offset, v);
                this[i] = tmp;
            }
        }

        public long InsertString(string text)
        {
            long res = 0;
            for (int i = 0; i < size; ++i)
            {
                res = Cursor.Buffer.InsertString(End[i], text);
            }
            UpdateFromOffset();
            return res;
        }

        public long InsertBytes(byte[] text)
        {
            long res = 0;
            for (int i = 0; i < size; ++i)
            {
                res = Cursor.Buffer.InsertBytes(End[i], text);
            }
            UpdateFromOffset();
            return res;
        }

        public long InsertString(long position, string text) => Cursor.Buffer.InsertString(position, text);
        public long InsertBytes(long position, byte[] text) => Cursor.Buffer.InsertBytes(position, text);

        public void Copy()
        {
            for (int i = 0; i < size; ++i)
            {
                long end = End[i];
                long begin = Begin[i];
                Clipboards[i] = Math.Abs(end - begin) == 0 ? "" : Cursor.Buffer.Text.Substring(Math.Min(begin, end), Math.Abs(end - begin)); 
            }
        }

        public void Paste()
        {
            for (int i = 0; i < size; ++i)
            {
                string? value = Clipboards[i];
                if (value != null)
                {
                    Cursor.Buffer.InsertString(End[i], value);
                }
            }
            UpdateFromOffset();
        }

        public void DeleteString()
        {
            for (int i = 0; i < size; ++i)
            {
                long end = End[i];
                long begin = Begin[i];
                if (end != begin)
                {
                    Cursor.Buffer.DeleteString(Math.Min(begin, end), Math.Abs(end - begin));
                }
            }
            UpdateFromOffset();
        }

        public void DeleteString(long min, long textLength)
        {
            Cursor.Buffer.DeleteString(min, textLength);
            UpdateFromOffset();
        }

        public void Add(EditorSelection selection) => Insert(Count, selection);

        public void SwapBeginEnd()
        {
            (Begin, End) = (End, Begin);
        }

        public void SelectEnd()
        {
            for (int i = 0; i < size; ++i)
            {
                Begin[i] = End[i];
            }
        }

        public void MoveToLineBegin(bool v)
        {
            for (int i = 0; i < size; ++i)
            {
                var tmp = this[i];
                tmp.MoveToLineBegin(v);
                this[i] = tmp;
            }
        }

        public void MoveToLineEnd(bool v)
        {
            for (int i = 0; i < size; ++i)
            {
                var tmp = this[i];
                tmp.MoveToLineEnd(v);
                this[i] = tmp;
            }
        }

        internal void MoveInsert(long position, long length)
        {
            End.Add(position - 1, length);
            Begin.Add(position - 1, length);
            FromLineOffset.Add(position - 1, length);
        }

        internal void MoveDelete(long position, long length)
        {
            End.Sub(position, length);
            Begin.Sub(position, length);
            FromLineOffset.Sub(position, length);
        }

        public EditorSelection this[long index]
        {
            get
            {
                return new EditorSelection(Cursor, Begin[index], End[index], FromLineOffset[index]);
            }
            set
            {
                End[index] = value.End;
                Begin[index] = value.Begin;
                FromLineOffset[index] = value.FromLineOffset;
                Clipboards[(int)index] = value.Clipboard;
            }
        }

    }

    public class EditorSelection
    {
        public long Begin;
        public long End;
        public long FromLineOffset;
        public string? Clipboard = null;
        public EditorCursor Cursor;

        public void UpdateFromLineOffset()
        {
            long last_newline = Cursor.Buffer.Text.NearestNewlineLeft(End - 1);
            if (last_newline == -1) { last_newline = 0; }
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

        public EditorSelection(EditorCursor cursor, long begin, long end, long lineOffset)
        {
            Cursor = cursor;
            Begin = begin;
            End = end;
            FromLineOffset = lineOffset;
        }


        private void MoveCursorsInsert(long position, long length)
        {
            if (Begin >= position)
            {
                Begin += length;
            }
            if (End >= position)
            {
                End += length;
            }
            UpdateFromLineOffset();
        }


        private void MoveCursorsDelete(long position, long length)
        {
            if (Begin >= position + length)
            {
                Begin -= length;
            }
            else if (Begin >= position)
            {
                Begin = position;
            }
            if (End >= position + length)
            {
                End -= length;
            }
            else if (End >= position)
            {
                End = position;
            }
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
            Clipboard = Text;
        }

        public void Paste()
        {
            if (Clipboard != null)
            {
                InsertString(Clipboard);
            }
        }

        public long BeginLine => Cursor.Buffer.GetPositionOffsets(Begin).line;
        public long EndLine => Cursor.Buffer.GetPositionOffsets(End).line;
        public long MinLine => Cursor.Buffer.GetPositionOffsets(Min).line;
        public long MaxLine => Cursor.Buffer.GetPositionOffsets(Max).line;

        public long InsertString(string text)
        {
            long res = Cursor.Buffer.InsertString(End, text);
            MoveCursorsInsert(End, res);
            return res;
        }

        public long InsertBytes(byte[] text)
        {
            long res = Cursor.Buffer.InsertBytes(End, text);
            MoveCursorsInsert(End, res);
            return res;
        }

        public long InsertString(long position, string text)
        {
            long res = Cursor.Buffer.InsertString(position, text);
            MoveCursorsInsert(position, res);
            return res;
        }

        public long InsertBytes(long position, byte[] text)
        {
            long res = Cursor.Buffer.InsertBytes(position, text);
            MoveCursorsInsert(position, res);
            return res;
        }

        public void DeleteString(long min, long textLength)
        {
            Cursor.Buffer.DeleteString(min, textLength);
            MoveCursorsDelete(min, textLength);
        }

        public void MoveToLineBegin(bool withSelect = false)
        {
            var res = Cursor.Buffer.GetLine(EndLine);
            if (res.value == null)
            {
                return;
            }
            string str = res.value;
            long textBegin = res.offset;
            if (!string.IsNullOrWhiteSpace(str))
            {
                textBegin = res.offset + str.TakeWhile(char.IsWhiteSpace).Count();
            }
            if (End == textBegin)
            {
                End = res.offset;
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
            if (res.value == null)
            {
                return;
            }
            End = res.offset + res.length - (res.value.EndsWith("\n") ? 1 : 0);
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
