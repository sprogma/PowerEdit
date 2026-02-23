using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TextBuffer
{
    public static class Modification
    {
        public const UInt64 Insert = 1;
        public const UInt64 Delete = 2;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct MarshalingCursor(long begin, long end)
    {
        public long Begin = begin, End = end;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct MarshalingLink(IntPtr parent, IntPtr child)
    {
        public IntPtr Parent = parent, Child = child;

        public void Deconstruct(out IntPtr parent, out IntPtr child)
        {
            parent = Parent;
            child = Child;
        }
    }

    internal static class CLibrary
    {
        [DllImport("msrope.dll")]
        internal static extern IntPtr project_create();

        [DllImport("msrope.dll")]
        internal static extern void project_destroy(IntPtr project);

        [DllImport("msrope.dll")]
        internal static extern IntPtr project_new_state(IntPtr project);

        [DllImport("msrope.dll")]
        internal static extern IntPtr state_create_dup(IntPtr project, IntPtr state);

        [DllImport("msrope.dll")]
        internal static extern int state_moditify(IntPtr project, IntPtr state, long pos, UInt64 type, long len, byte[]? text);

        [DllImport("msrope.dll")]
        internal static extern void state_commit(IntPtr project, IntPtr state);

        [DllImport("msrope.dll")]
        internal static extern long state_get_size(IntPtr state);

        [DllImport("msrope.dll")]
        internal static extern void state_read(IntPtr state, long position, long length, IntPtr buffer);

        [DllImport("msrope.dll")]
        internal static extern IntPtr state_version_before(IntPtr state, long steps);

        [DllImport("msrope.dll")]
        internal static extern void project_get_states_len(IntPtr project, out long states_count, out long links_count);

        [DllImport("msrope.dll")]
        internal static extern void project_get_states(IntPtr project, long states_count, [Out] IntPtr[] states, long links_count, [Out] MarshalingLink[] links);

        [DllImport("msrope.dll")]
        internal static extern IntPtr state_resolve(IntPtr state);

        [DllImport("msrope.dll")]
        internal static extern void state_set_cursors(IntPtr state, long count, [In] MarshalingCursor[] cursors);

        [DllImport("msrope.dll")]
        internal static extern long state_get_cursors_count(IntPtr state);

        [DllImport("msrope.dll")]
        internal static extern void state_get_cursors(IntPtr state, long count, [Out] MarshalingCursor[] cursors);

        [DllImport("msrope.dll")]
        internal static extern void state_get_offsets(IntPtr state, long position, out long line, out long column);

        [DllImport("msrope.dll")]
        internal static extern long state_nearest_left(IntPtr state, long position);

        [DllImport("msrope.dll")]
        internal static extern long state_nearest_right(IntPtr state, long position);

        [DllImport("msrope.dll")]
        internal static extern long state_line_number(IntPtr state, long position);

        [DllImport("msrope.dll")]
        internal static extern long state_nth_newline(IntPtr state, long position);
    }


    public class TextBuffer
    {
        IntPtr project;
        IntPtr curr_state;
        Stack<IntPtr> undos;

        public TextBuffer()
        {
            project = CLibrary.project_create();
            curr_state = CLibrary.project_new_state(project);
            undos = [];
        }

        ~TextBuffer()
        {
            CLibrary.project_destroy(project);
        }

        public char this[long index] { 
            get
            {
                IntPtr destPtr = Marshal.AllocHGlobal(1);
                CLibrary.state_read(curr_state, index, 1, destPtr);
                string res = Marshal.PtrToStringAnsi(destPtr, 1);
                Marshal.FreeHGlobal(destPtr);
                return res[0];
            } 
            set
            {
                curr_state = CLibrary.state_create_dup(project, curr_state);
                CLibrary.state_moditify(project, curr_state, index, Modification.Delete, 1, null);
                byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(value.ToString());
                CLibrary.state_moditify(project, curr_state, index, Modification.Insert, 1, utf8Bytes);
                CLibrary.state_commit(project, curr_state);
            }
        }

        public int Length => (int)CLibrary.state_get_size(curr_state);
        public long LengthEx(IntPtr state) => CLibrary.state_get_size(state);

        public string SubstringEx(IntPtr state, long pos, long len)
        {
            IntPtr destPtr = Marshal.AllocHGlobal((int)(len + 10));
            CLibrary.state_read(state, pos, len, destPtr);
            string res = Marshal.PtrToStringAnsi(destPtr, (int)len);
            Marshal.FreeHGlobal(destPtr);
            return res;
        }
        
        public string SubstringEx(IntPtr state, long pos) => SubstringEx(state, pos, LengthEx(state) - pos);

        public string Substring(long pos, long len)
        {
            // Console.WriteLine($"Req SUBSTR of len {len}");
            IntPtr destPtr = Marshal.AllocHGlobal((int)(len + 10));

            CLibrary.state_read(curr_state, pos, len, destPtr);
            string res = Marshal.PtrToStringUTF8(destPtr, (int)len);
            Marshal.FreeHGlobal(destPtr);
            return res;
        }

        public string Substring(long pos) => Substring(pos, Length - pos);

        public long IndexOf(char item, long offset)
        {
            for (long i = offset; i < Length; ++i)
            {
                if (this[i] == item)
                {
                    return i;
                }
            }
            return -1;
        }

        public long LastIndexOf(char item, long offset)
        {
            for (long i = offset; i >= 0; --i)
            {
                if (this[i] == item)
                {
                    return i;
                }
            }
            return -1;
        }

        public long IndexOf(string item, long offset)
        {
            long l = Length;
            for (long i = Math.Max(offset, 0); i + item.Length <= l; ++i)
            {
                if (Substring(i, item.Length) == item)
                {
                    return i;
                }
            }
            return -1;
        }

        public long NearestNewlineLeft(long offset)
        {
            return CLibrary.state_nearest_left(curr_state, offset);
        }

        public long NearestNewlineRight(long offset)
        {
            return CLibrary.state_nearest_right(curr_state, offset);
        }

        public void Undo()
        {
            undos.Push(curr_state);
            curr_state = CLibrary.state_version_before(curr_state, 1);
        }

        public bool Redo()
        {
            if (undos.TryPop(out IntPtr version))
            {
                curr_state = version;
                return true;
            }
            return false;
        }

        public long Insert(long index, string item)
        {
            undos.Clear();
            curr_state = CLibrary.state_create_dup(project, curr_state);
            byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(item);
            CLibrary.state_moditify(project, curr_state, index, Modification.Insert, item.Length, utf8Bytes);
            CLibrary.state_commit(project, curr_state);
            return utf8Bytes.Length;
        }

        public long InsertBytes(long index, byte[] item)
        {
            undos.Clear();
            curr_state = CLibrary.state_create_dup(project, curr_state);
            CLibrary.state_moditify(project, curr_state, index, Modification.Insert, item.Length, item);
            CLibrary.state_commit(project, curr_state);
            return item.Length;
        }

        public void RemoveAt(long index, long count = 1)
        {
            if (count == 0) return;
            if (index + count > Length) return;
            undos.Clear();
            curr_state = CLibrary.state_create_dup(project, curr_state);
            CLibrary.state_moditify(project, curr_state, index, Modification.Delete, count, null);
            CLibrary.state_commit(project, curr_state);
        }

        public void Clear() => RemoveAt(0, Length);

        public void PushHistory()
        {
            // TODO: this
        }

        public void SetVersion(IntPtr version)
        {
            curr_state = version;
        }

        public IntPtr GetCurrentVersion()
        {
            return curr_state;
        }

        public (IntPtr[] states, MarshalingLink[] links) GetVersionTree()
        {
            curr_state = CLibrary.state_resolve(curr_state);
            CLibrary.project_get_states_len(project, out long versions_count, out long links_count);
            IntPtr[] states = new IntPtr[versions_count];
            MarshalingLink[] links = new MarshalingLink[links_count];
            CLibrary.project_get_states(project, versions_count, states, links_count, links);
            return (states, links);
        }

        public void SaveToFile(string filename)
        {
            File.WriteAllText(filename, Substring(0));
        }

        public void SaveCursors(IntPtr state, MarshalingCursor[] cursors)
        {
            CLibrary.state_set_cursors(state, cursors.LongLength, cursors);
        }

        public MarshalingCursor[] GetCursors(IntPtr state)
        {
            long cursors_count = CLibrary.state_get_cursors_count(state);
            MarshalingCursor[] cursors = new MarshalingCursor[cursors_count];
            CLibrary.state_get_cursors(state, cursors_count, cursors);
            return cursors;
        }

        public (long, long) GetPositionOffsets(long position)
        {
            CLibrary.state_get_offsets(curr_state, position, out long line, out long column);
            return (line, column);
        }

        public (long index, string? text, long length) GetLine(long line)
        {
            if (line < 0) return (0, null, 0);

            long startPos = 0;
            if (line > 0)
            {
                long prevNewline = CLibrary.state_nth_newline(curr_state, line - 1);
                if (prevNewline == -1) return (0, null, 0);
                startPos = prevNewline + 1;
            }

            if (startPos >= Length)
            {
                return (0, null, 0);
            }

            long nextNewline = CLibrary.state_nth_newline(curr_state, line), len;
            string resultText;
            if (nextNewline == -1)
            {
                len = Length - startPos;
            }
            else
            {
                len = (nextNewline - startPos) + 1;
            }
            resultText = Substring(startPos, len);
            return (startPos, resultText, len);
        }
    }
}
