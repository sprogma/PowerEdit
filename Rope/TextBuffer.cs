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
        internal static extern int buffer_create(out IntPtr ptr);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_destroy(IntPtr ptr);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_moditify_star(IntPtr ptr, UInt64 type, long pos, long len, string? text);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_get_size(IntPtr ptr, out long length);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_read(IntPtr ptr, long from, long length, IntPtr buffer);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_version_set(IntPtr ptr, long version);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_version_get(IntPtr ptr, out long version);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_version_before(IntPtr ptr, long version, long steps, out long result);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_read_versions_count(IntPtr ptr, out long result);

        [DllImport("msrope.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int buffer_read_versions(IntPtr ptr, long count, long[] parents);
    }


    public class TextBuffer
    {
        IntPtr handle;
        Stack<long> undos;

        public TextBuffer()
        {
            CLibrary.buffer_create(out handle);
            undos = [];
        }

        public char this[long index] { 
            get
            {
                IntPtr destPtr = Marshal.AllocHGlobal(1);
                CLibrary.buffer_read(handle, index, 1, destPtr);
                string res = Marshal.PtrToStringAnsi(destPtr, 1);
                Marshal.FreeHGlobal(destPtr);
                return res[0];
            } 
            set
            {
                CLibrary.buffer_moditify_star(handle, Modification.Delete, index, 1, null);
                CLibrary.buffer_moditify_star(handle, Modification.Insert, index, 1, value.ToString());
            }
        }

        public int Length { get {
                CLibrary.buffer_get_size(handle, out long size);
                return (int)size;
            } }


        public string Substring(long pos, long len)
        {
            // Console.WriteLine($"Req SUBSTR of len {len}");
            IntPtr destPtr = Marshal.AllocHGlobal((int)(len + 10));
            CLibrary.buffer_read(handle, pos, len, destPtr);
            string res = Marshal.PtrToStringAnsi(destPtr, (int)len);
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

        public long LastIndexOf(string item, long offset)
        {
            for (long i = Math.Min(offset - item.Length + 1, Length - item.Length); i >= 0; --i)
            {
                if (Substring(i, item.Length) == item)
                {
                    return i;
                }
            }
            return -1;
        }

        public long Undo()
        {
            CLibrary.buffer_version_get(handle, out long version);
            CLibrary.buffer_version_before(handle, version, 1, out long result);
            CLibrary.buffer_version_set(handle, result);
            undos.Push(version);
            return result;
        }

        public long? Redo()
        {
            if (undos.TryPop(out long version))
            {
                CLibrary.buffer_version_set(handle, version);
                return version;
            }
            return null;
        }

        public void Insert(long index, string item)
        {
            undos.Clear();
            CLibrary.buffer_moditify_star(handle, Modification.Insert, index, item.Length, item);
        }

        public void RemoveAt(long index, long count = 1)
        {
            undos.Clear();
            CLibrary.buffer_moditify_star(handle, Modification.Delete, index, count, null);
        }

        public void Clear() => RemoveAt(0, Length);

        public void PushHistory()
        {
            // TODO: this
        }

        public void SetVersion(long version)
        {
            CLibrary.buffer_version_set(handle, version);
        }

        public long GetCurrentVersion()
        {
            CLibrary.buffer_version_get(handle, out long version);
            Console.WriteLine($"Get version {version}");
            return version;
        }

        public long[] GetVersionTree()
        {
            CLibrary.buffer_read_versions_count(handle, out long versions_count);
            Console.WriteLine($"Get version count: {versions_count}");
            long[] result = new long[versions_count];
            CLibrary.buffer_read_versions(handle, versions_count, result);
            Console.WriteLine($"Get array!");
            return result;
        }
    }
}
