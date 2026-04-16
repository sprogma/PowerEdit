using System.Diagnostics;
using System.Text;

namespace Common
{
    public enum LogLevel 
    { 
        Info, 
        Warning, 
        Error, 
        AppStart 
    }

    public class Logger
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
        private static readonly Lock FileLock = new();

        private static StreamWriter? Writer;

        private static StreamWriter GetWriter()
        {
            if (Writer == null)
            {
                var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096);
                Writer = new StreamWriter(stream, Encoding.UTF8);
            }
            return Writer;
        }

        [Conditional("DEBUG")]
        public static void Log(LogLevel level, string message)
        {
            var record = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {$"[{level}]",10} {message}";

            lock (FileLock)
            {
                GetWriter().WriteLine(record);
            }
        }

        [Conditional("DEBUG")]
        public static void Log(string message) => Log(LogLevel.Info, message);
    }
}
