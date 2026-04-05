namespace Common
{
    public enum LogLevel : int
    {
        Info,
        Warning,
        Error,
        AppStart,
    };

    public class Logger
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
        private static readonly Lock FileLock = new();

        public static void Log(LogLevel level, string message)
        {
            var record = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {$"[{level}]",10} {message}{Environment.NewLine}";

            lock (FileLock)
            {
                File.AppendAllText(LogPath, record);
            }
        }

        public static void Log(string message) => Log(LogLevel.Info, message);
    }
}
