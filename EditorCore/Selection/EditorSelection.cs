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
using System.Xml.Linq;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;

namespace EditorCore.Selection
{
    internal class OrderedMaxTreap
    {
        private struct Node
        {
            public long Value;
            public long Priority;

            public long LazyAdd = 0;
            public long LazySet = -1;

            public int Left = -1, Right = -1, Parent = -1;

            public Node(long val)
            {
                Value = val;
                Priority = new Random().NextInt64();
            }
        }

        public long size = 0;
        // -1 means null
        private int root = -1;
        private Node[] nodes = new Node[1024];

        public OrderedMaxTreap Copy()
        {
            var copy = new OrderedMaxTreap();
            if (copy.nodes.Length != nodes.Length)
            {
                Array.Resize(ref copy.nodes, nodes.Length);
            }
            Array.Copy(nodes, 0, copy.nodes, 0, (int)size);
            copy.size = size;
            copy.root = root;
            return copy;
        }

        public long this[long index]
        {
            get => Get((int)index);
            set => Set((int)index, value);
        }

        public long Get(int index)
        {
            PushPathFromRoot(index);
            return nodes[index].Value;
        }

        public void Set(int index, long newValue)
        {
            ref var target = ref nodes[index];
            RemoveNode(index);
            target.Value = newValue;
            target.LazyAdd = 0;
            target.LazySet = -1;
            root = InsertNode(root, index);
        }

        public void Insert(int pos, long val)
        {
            EnsureCapacity(size + 1);

            if (size - pos > 0)
            {
                for (int i = 0; i < size; i++)
                {
                    ref var n = ref nodes[i];
                    if (n.Left >= pos) n.Left++;
                    if (n.Right >= pos) n.Right++;
                    if (n.Parent >= pos) n.Parent++;
                }
                if (root >= pos) root++;
            }

            Array.Copy(nodes, pos, nodes, pos + 1, size - pos);

            nodes[pos] = new Node(val);
            size++;

            root = InsertNode(root, pos);
        }

        private void EnsureCapacity(long newCount)
        {
            if (newCount >= nodes.Length)
            {
                Array.Resize(ref nodes, nodes.Length * 2);
            }
        }

        public void Add(long x, long v)
        {
            var (low, hi) = Split(root, x);
            if (hi != -1)
            {
                ApplyAdd(hi, v);
            }
            root = Merge(low, hi);
        }

        // with saturation
        public void AddSat(long x, long v)
        {
            var (low, hi) = Split(root, x);
            var (add, ceil) = Split(low, x - v);
            ApplySet(ceil, x);
            ApplyAdd(add, v);

            root = Merge(Merge(add, ceil), hi);
        }

        // with saturation
        public void SubSat(long x, long v)
        {
            var (low, hi) = Split(root, x);
            var (floor, sub) = Split(hi, x + v);
            ApplySet(floor, x);
            ApplyAdd(sub, -v);

            root = Merge(low, Merge(floor, sub));
        }


        private void ApplyAdd(int t, long v)
        {
            if (t == -1) return;
            ref var node = ref nodes[t];
            if (node.LazySet != -1) node.LazySet += v;
            else node.LazyAdd += v;
        }

        private void ApplySet(int t, long v)
        {
            if (t == -1) return;
            ref var node = ref nodes[t];
            node.LazySet = v;
            node.LazyAdd = 0;
        }

        private void Push(int t)
        {
            if (t == -1) return;
            ref var node = ref nodes[t];
            if (node.LazySet != -1)
            {
                node.Value = node.LazySet;
                ApplySet(node.Left, node.LazySet);
                ApplySet(node.Right, node.LazySet);
                node.LazySet = -1;
            }
            else if (node.LazyAdd != 0)
            {
                node.Value += node.LazyAdd;
                ApplyAdd(node.Left, node.LazyAdd);
                ApplyAdd(node.Right, node.LazyAdd);
                node.LazyAdd = 0;
            }
        }

        private void PushPathFromRoot(int t)
        {
            Stack<int> path = [];
            int curr = t;
            while (curr != -1) { path.Push(curr); curr = nodes[curr].Parent; }
            while (path.Count > 0) Push(path.Pop());
        }

        private (int, int) Split(int t, long key)
        {
            if (t == -1) return (-1, -1);
            Push(t);
            ref var node = ref nodes[t];
            if (node.Value <= key)
            {
                var (l, r) = Split(node.Right, key);
                node.Right = l;
                if (l != -1) nodes[l].Parent = t;
                if (r != -1) nodes[r].Parent = -1;
                return (t, r);
            }
            else
            {
                var (l, r) = Split(node.Left, key);
                node.Left = r;
                if (l != -1) nodes[l].Parent = -1;
                if (r != -1) nodes[r].Parent = t;
                return (l, t);
            }
        }

        private int Merge(int l, int r)
        {
            if (l == -1 || r == -1) return Math.Max(l, r);
            Push(l); 
            Push(r);
            ref var nl = ref nodes[l];
            ref var nr = ref nodes[r];
            if (nl.Priority > nr.Priority)
            {
                nl.Right = Merge(nl.Right, r);
                if (nl.Right != -1) nodes[nl.Right].Parent = l;
                return l;
            }
            else
            {
                nr.Left = Merge(l, nr.Left);
                if (nr.Left != -1) nodes[nr.Left].Parent = r;
                return r;
            }
        }

        private int InsertNode(int t, int node)
        {
            var (l, ri) = Split(t, nodes[node].Value);
            nodes[node].Left = nodes[node].Right = nodes[node].Parent = -1;
            return Merge(Merge(l, node), ri);
        }

        private void RemoveNode(int t)
        {
            PushPathFromRoot(t);
            ref var node = ref nodes[t];
            int m = Merge(node.Left, node.Right);
            if (node.Parent == -1) 
                root = m;
            else if (nodes[node.Parent].Left == t) 
                nodes[node.Parent].Left = m;
            else 
                nodes[node.Parent].Right = m;

            if (m != -1) nodes[m].Parent = node.Parent;
            node.Left = node.Right = node.Parent = -1;
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
                long end = End[i];
                long last_newline = Cursor.Buffer.Text.NearestNewlineLeft(end - 1);
                FromLineOffset[i] = end - last_newline - 1;
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

        public void MoveHorisontal(long offset, bool withSelect)
        {
            if (offset > 0)
            {
                End.AddSat(Cursor.Buffer.Text.Length, offset);
            }
            else
            {
                End.SubSat(0, -offset);
            }
            if (!withSelect)
            {
                UpdateBeginToEnd();
            }
            UpdateFromOffset();
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

        public IEnumerable<string> GetPaste()
        {
            for (int i = 0; i < size; ++i)
            {
                string? value = Clipboards[i];
                if (value != null)
                {
                    yield return value;
                }
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

        public void UpdateBeginToEnd()
        {
            Begin = End.Copy();
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
            End.SubSat(position, length);
            Begin.SubSat(position, length);
            FromLineOffset.SubSat(position, length);
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
        public string? GetPaste()
        {
            return Clipboard;
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
