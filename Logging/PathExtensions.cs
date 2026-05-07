using System.IO;

namespace Common
{
    public static class PathExtensions
    {
        extension(Path)
        {
            public static string? TryGetFullPath(string? path)
            {
                if (path == null) return null;
                try
                {
                    return Path.GetFullPath(path);
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
