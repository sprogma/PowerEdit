using Common;
using System.Runtime.InteropServices;
using System.Text;

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

    internal static partial class CLibrary
    {
        // Logger
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogDelegate(LogLevel level, [MarshalAs(UnmanagedType.LPStr)] string message);

        [LibraryImport("msrope.dll")]
        public static partial void SetLogger(LogDelegate callback);

        // Strings

        [LibraryImport("msrope.dll")]
        internal static partial IntPtr project_create();

        [LibraryImport("msrope.dll")]
        internal static partial void project_destroy(IntPtr project);

        [LibraryImport("msrope.dll")]
        internal static partial IntPtr project_new_state(IntPtr project);

        [LibraryImport("msrope.dll")]
        internal static partial IntPtr project_open_file(IntPtr project, [MarshalAs(UnmanagedType.LPUTF8Str)] string filename);

        [LibraryImport("msrope.dll")]
        internal static partial int project_save_file(IntPtr project, IntPtr curr_state, [MarshalAs(UnmanagedType.LPUTF8Str)] string tempFile);

        [LibraryImport("msrope.dll")]
        internal static partial IntPtr state_create_dup(IntPtr project, IntPtr state);

        [LibraryImport("msrope.dll")]
        internal static partial int state_moditify(IntPtr project, IntPtr state, long pos, UInt64 type, long len, byte[]? text);

        [LibraryImport("msrope.dll")]
        internal static partial void state_commit(IntPtr project, IntPtr state);

        [LibraryImport("msrope.dll")]
        internal static partial long state_get_size(IntPtr state);

        [LibraryImport("msrope.dll")]
        internal static partial void state_read(IntPtr state, long position, long length, IntPtr buffer);

        [LibraryImport("msrope.dll")]
        internal static partial void state_read(IntPtr state, long position, long length, [Out] byte[] buffer);

        [LibraryImport("msrope.dll")]
        internal static partial IntPtr state_version_before(IntPtr state, long steps);

        [LibraryImport("msrope.dll")]
        internal static partial void project_get_states_len(IntPtr project, out long states_count, out long links_count);

        [LibraryImport("msrope.dll")]
        internal static partial void project_get_states(IntPtr project, long states_count, [Out] IntPtr[] states, long links_count, [Out] MarshalingLink[] links);

        [LibraryImport("msrope.dll")]
        internal static partial IntPtr state_resolve(IntPtr state);

        [LibraryImport("msrope.dll")]
        internal static partial void state_set_cursors(IntPtr state, long count, [In] MarshalingCursor[] cursors);

        [LibraryImport("msrope.dll")]
        internal static partial long state_get_cursors_count(IntPtr state);

        [LibraryImport("msrope.dll")]
        internal static partial void state_get_cursors(IntPtr state, long count, [Out] MarshalingCursor[] cursors);

        [LibraryImport("msrope.dll")]
        internal static partial void state_get_offsets(IntPtr state, long position, out long line, out long column);

        [LibraryImport("msrope.dll")]
        internal static partial long state_nearest_left(IntPtr state, long position);

        [LibraryImport("msrope.dll")]
        internal static partial long state_nearest_right(IntPtr state, long position);

        [LibraryImport("msrope.dll")]
        internal static partial long state_line_number(IntPtr state, long position);

        [LibraryImport("msrope.dll")]
        internal static partial long state_nth_newline(IntPtr state, long position);


        private static LogDelegate? LogCallback;

        public static bool WasInitializated { get; private set; } = false;

        static public void Init()
        {
            LogCallback = (level, message) => {
                Logger.Log(level, $"[C] {message}");
            };
            SetLogger(LogCallback);

            WasInitializated = true;
        }
    }
}
