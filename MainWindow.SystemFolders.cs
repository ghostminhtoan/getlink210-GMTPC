using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private async void BtnMergeFolders_Click(object sender, RoutedEventArgs e)
        {
            string downloadRoot = txtDownloadPath.Text.Trim();
            if (string.IsNullOrEmpty(downloadRoot))
            {
                MessageBox.Show("Vui lòng chọn thư mục lưu trước (Please select a download folder first).", "Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(downloadRoot))
            {
                MessageBox.Show("Thư mục lưu không tồn tại (Download folder does not exist).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string targetFolder = GetActiveTargetFolder(downloadRoot);

            Log($"[Merge] Bắt đầu gộp thư mục tại: {targetFolder}");
            lblStatus.Text = "Merging folders...";

            try
            {
                var directories = Directory.GetDirectories(targetFolder);
                var groups = directories
                    .Select(dir => new { Path = dir, Name = Path.GetFileName(dir) })
                    .Where(d => d.Name.Contains("-"))
                    .Select(d =>
                    {
                        int index = d.Name.IndexOf('-');
                        string prefix = d.Name.Substring(0, index).Trim();
                        string suffix = d.Name.Substring(index + 1).Trim();
                        return new { d.Path, d.Name, Prefix = prefix, Suffix = suffix };
                    })
                    .GroupBy(d => d.Prefix, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() >= 2) // Merge if there are 2 or more folders with the same prefix
                    .ToList();

                if (!groups.Any())
                {
                    Log("[Merge] Không tìm thấy nhóm thư mục nào có tên giống nhau trước dấu gạch nối.");
                    lblStatus.Text = "Merge completed. No folders merged.";
                    MessageBox.Show("Không tìm thấy thư mục nào có tên giống nhau trước dấu gạch nối để gộp (No matching folders found to merge).", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                int mergedCount = 0;
                foreach (var group in groups)
                {
                    string groupPrefix = group.Key;
                    string destParentDir = Path.Combine(targetFolder, groupPrefix);

                    // Create the parent directory for the group (e.g. "Tên Truyện")
                    if (!Directory.Exists(destParentDir))
                    {
                        Directory.CreateDirectory(destParentDir);
                    }

                    foreach (var item in group)
                    {
                        string destDir = Path.Combine(destParentDir, item.Suffix);

                        try
                        {
                            if (!Directory.Exists(destDir))
                            {
                                Directory.Move(item.Path, destDir);
                            }
                            else
                            {
                                // Merge contents recursively if destination already exists
                                MergeDirectoryContents(item.Path, destDir);
                            }
                            mergedCount++;
                            Log($"[Merge] Đã gộp '{item.Name}' -> '{groupPrefix}\\{item.Suffix}'");
                        }
                        catch (Exception ex)
                        {
                            Log($"[Merge Error] Không thể gộp thư mục '{item.Name}': {ex.Message}");
                        }
                    }
                }

                Log("[Merge] Đang tạm ngừng 3 giây để hệ thống ổn định và nhận biết thư mục...");
                await System.Threading.Tasks.Task.Delay(3000);

                Log($"[Merge] Hoàn tất gộp {mergedCount} thư mục.");
                lblStatus.Text = $"Merge completed. Merged {mergedCount} folders.";
                MessageBox.Show($"Đã gộp thành công {mergedCount} thư mục!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"[Merge Error] Lỗi nghiêm trọng khi gộp thư mục: {ex.Message}");
                lblStatus.Text = "Merge failed.";
                MessageBox.Show($"Lỗi khi gộp thư mục: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnSplitFolders_Click(object sender, RoutedEventArgs e)
        {
            string downloadRoot = txtDownloadPath.Text.Trim();
            if (string.IsNullOrEmpty(downloadRoot))
            {
                MessageBox.Show("Vui lòng chọn thư mục lưu trước (Please select a download folder first).", "Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(downloadRoot))
            {
                MessageBox.Show("Thư mục lưu không tồn tại (Download folder does not exist).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string targetFolder = GetActiveTargetFolder(downloadRoot);

            Log($"[Split] Bắt đầu tách thư mục tại: {targetFolder}");
            lblStatus.Text = "Splitting folders...";

            try
            {
                var directories = Directory.GetDirectories(targetFolder);
                int splitCount = 0;

                foreach (var dir in directories)
                {
                    string parentName = Path.GetFileName(dir);
                    var subDirs = Directory.GetDirectories(dir);

                    // Split if the folder contains 2 or more subfolders
                    if (subDirs.Length >= 2)
                    {
                        foreach (var subDir in subDirs)
                        {
                            string subName = Path.GetFileName(subDir);
                            string destName = $"{parentName}-{subName}";
                            string destPath = Path.Combine(targetFolder, destName);

                            try
                            {
                                if (!Directory.Exists(destPath))
                                {
                                    Directory.Move(subDir, destPath);
                                }
                                else
                                {
                                    // Merge contents recursively if destination already exists
                                    MergeDirectoryContents(subDir, destPath);
                                }
                                splitCount++;
                                Log($"[Split] Đã tách '{parentName}\\{subName}' -> '{destName}'");
                            }
                            catch (Exception ex)
                            {
                                Log($"[Split Error] Không thể tách thư mục '{parentName}\\{subName}': {ex.Message}");
                            }
                        }

                        // If parent directory is now empty, delete it
                        try
                        {
                            if (!Directory.EnumerateFileSystemEntries(dir).Any())
                            {
                                Directory.Delete(dir, false);
                                Log($"[Split] Đã xóa thư mục cha trống: '{parentName}'");
                            }
                            else
                            {
                                Log($"[Split] Thư mục cha '{parentName}' vẫn còn tệp/thư mục khác nên không xóa.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"[Split Warning] Không thể xóa thư mục cha '{parentName}': {ex.Message}");
                        }
                    }
                }

                Log("[Split] Đang tạm ngừng 3 giây để hệ thống ổn định và nhận biết thư mục...");
                await System.Threading.Tasks.Task.Delay(3000);

                Log($"[Split] Hoàn tất tách {splitCount} thư mục.");
                lblStatus.Text = $"Split completed. Split {splitCount} folders.";
                MessageBox.Show($"Đã tách thành công {splitCount} thư mục!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"[Split Error] Lỗi nghiêm trọng khi tách thư mục: {ex.Message}");
                lblStatus.Text = "Split failed.";
                MessageBox.Show($"Lỗi khi tách thư mục: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MergeDirectoryContents(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
            {
                string destFile = Path.Combine(dest, Path.GetFileName(file));
                if (File.Exists(destFile))
                {
                    File.Delete(file);
                }
                else
                {
                    File.Move(file, destFile);
                }
            }
            foreach (var dir in Directory.GetDirectories(source))
            {
                string destDir = Path.Combine(dest, Path.GetFileName(dir));
                MergeDirectoryContents(dir, destDir);
            }
            Directory.Delete(source, true);
        }

        private string GetActiveTargetFolder(string downloadRoot)
        {
            string subFolder = "";
            int tabIndex = 0;
            Dispatcher.Invoke(() =>
            {
                if (tabLeftPanel != null)
                {
                    tabIndex = tabLeftPanel.SelectedIndex;
                }
            });

            switch (tabIndex)
            {
                case 0:
                    subFolder = "hentaiforce.net";
                    break;
                case 1:
                    subFolder = "nhentai.net";
                    break;
                case 2:
                    subFolder = "vi-hentai.pro";
                    break;
                case 3:
                    subFolder = "truyenqq";
                    break;
            }

            string targetFolder = string.IsNullOrEmpty(subFolder) 
                ? downloadRoot 
                : Path.Combine(downloadRoot, subFolder);

            if (!Directory.Exists(targetFolder))
            {
                targetFolder = downloadRoot;
            }
            return targetFolder;
        }
    }
}
