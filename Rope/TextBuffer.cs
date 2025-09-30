using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;

namespace TextBuffer
{
    public static class Modification
    {
        public const UInt64 Insert = 0;
        public const UInt64 Delete = 1;
    };

    internal static class CLibrary
    {
        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_create(ref IntPtr ptr);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_destroy(IntPtr ptr);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_moditify_star(IntPtr ptr, UInt64 type, nint pos, nint len, string? text);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_get_size(IntPtr ptr, ref nint length);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_read(IntPtr ptr, nint from, nint length, StringBuilder buffer);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_version_set(IntPtr ptr, nint version);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_version_get(IntPtr ptr, ref nint version);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_version_before(IntPtr ptr, nint version, nint steps, ref nint result);
    }

    public class TextBuffer
    {
        nint handle;

        public TextBuffer()
        {
            CLibrary.buffer_create(ref handle);
        }

        public char this[int index] { 
            get 
            {
                StringBuilder sb = new(1);
                CLibrary.buffer_read(handle, index, 1, sb);
                return sb[0];
            } 
            set
            {
                CLibrary.buffer_moditify_star(handle, Modification.Delete, index, 1, null);
                CLibrary.buffer_moditify_star(handle, Modification.Insert, index, 1, value.ToString());
            }
        }

        public int Length { get {
                nint size = 0;
                CLibrary.buffer_get_size(handle, ref size);
                return (int)size;
            } }


        public string Substring(int pos, int len)
        {
            StringBuilder sb = new(len);
            CLibrary.buffer_read(handle, pos, len, sb);
            return sb.ToString();
        }

        public int IndexOf(char item, int offset)
        {
            for (int i = offset; i < Length; ++i)
            {
                if (this[i] == item)
                {
                    return i;
                }
            }
            return -1;
        }

        public int LastIndexOf(char item, int offset)
        {
            for (int i = offset; i >= 0; --i)
            {
                if (this[i] == item)
                {
                    return i;
                }
            }
            return -1;
        }

        public void Undo()
        {
            nint version = 0;
            nint result = 0;
            CLibrary.buffer_version_current(handle, ref version);
            CLibrary.buffer_version_before(handle, version, 1, ref result);
            CLibrary.buffer_version_set(handle, result);
        }

        public void Redo()
        {

        }

        public void Insert(int index, string item)
        {
            CLibrary.buffer_moditify_star(handle, Modification.Insert, index, item.Length, item);
        }

        public void RemoveAt(int index, int count = 1)
        {
            CLibrary.buffer_moditify_star(handle, Modification.Delete, index, count, null);
        }

        public void Clear() => RemoveAt(0, Length);
    }
}
