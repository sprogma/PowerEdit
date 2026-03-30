using LoggingLogLevel;
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

    internal static class CLibrary
    {
        // Logger
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogDelegate(LogLevel level, [MarshalAs(UnmanagedType.LPStr)] string message);

        [DllImport("msrope.dll")]
        public static extern void SetLogger(LogDelegate callback);

        // Strings

        [DllImport("msrope.dll")]
        internal static extern IntPtr project_create();

        [DllImport("msrope.dll")]
        internal static extern void project_destroy(IntPtr project);

        [DllImport("msrope.dll")]
        internal static extern IntPtr project_new_state(IntPtr project);

        [DllImport("msrope.dll")]
        internal static extern IntPtr project_open_file(IntPtr project, [MarshalAs(UnmanagedType.LPUTF8Str)] string filename);

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
        internal static extern void state_read(IntPtr state, long position, long length, [Out] byte[] buffer);

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
