using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace get_link_manga
{
    internal static class ShellFolderLauncher
    {
        private static readonly object _sync = new object();
        private static string _lastOpenedPath;
        private static DateTime _lastOpenedAt = DateTime.MinValue;
        private static readonly TimeSpan OpenThrottle = TimeSpan.FromSeconds(2);

        internal static bool TryOpenFolder(string path, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Đường dẫn thư mục trống.";
                return false;
            }

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(path.Trim());
            }
            catch (Exception ex)
            {
                error = $"Đường dẫn không hợp lệ: {ex.Message}";
                return false;
            }

            lock (_sync)
            {
                if (string.Equals(_lastOpenedPath, normalizedPath, StringComparison.OrdinalIgnoreCase) &&
                    DateTime.Now - _lastOpenedAt < OpenThrottle)
                {
                    return true;
                }

                _lastOpenedPath = normalizedPath;
                _lastOpenedAt = DateTime.Now;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = normalizedPath,
                    WorkingDirectory = normalizedPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
