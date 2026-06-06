using System;
using System.Diagnostics;
using System.IO;

namespace get_link_manga
{
    internal static class ShellFolderLauncher
    {
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

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{normalizedPath}\"",
                    UseShellExecute = true
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
