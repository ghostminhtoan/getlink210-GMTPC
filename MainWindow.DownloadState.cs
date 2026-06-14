using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Windows;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private readonly ConcurrentDictionary<string, byte> _activeTempFolders = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        internal void RegisterTempFolder(string path)
        {
            if (!string.IsNullOrEmpty(path) &&
                path.IndexOf(@"\.tmp\", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _activeTempFolders.TryAdd(path, 0);
            }
        }

        internal void UnregisterTempFolder(string path)
        {
            if (!string.IsNullOrEmpty(path) &&
                path.IndexOf(@"\.tmp\", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _activeTempFolders.TryRemove(path, out _);
            }
        }

        internal void CleanupActiveTempFolders()
        {
            foreach (var path in _activeTempFolders.Keys.ToList())
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(path) &&
                        path.IndexOf(@"\.tmp\", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        System.IO.Directory.Exists(path))
                    {
                        System.IO.Directory.Delete(path, true);
                        Log($"[Cleanup] Đã xóa thư mục tạm tải dở: {path}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"[Cleanup Warning] Không thể xóa thư mục tạm '{path}': {ex.Message}");
                }
                finally
                {
                    _activeTempFolders.TryRemove(path, out _);
                }
            }
        }
    }
}
