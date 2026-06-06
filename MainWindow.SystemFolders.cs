using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private readonly SemaphoreSlim _folderStructureSemaphore = new SemaphoreSlim(1, 1);

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
                int mergedCount = await MergeFoldersInTargetFolderAsync(targetFolder, CancellationToken.None);

                Log("[Merge] Đang tạm ngừng 3 giây để hệ thống ổn định và nhận biết thư mục...");
                await Task.Delay(3000);

                if (mergedCount == 0)
                {
                    Log("[Merge] Không tìm thấy nhóm thư mục nào có tên giống nhau trước dấu gạch nối.");
                    lblStatus.Text = "Merge completed. No folders merged.";
                    MessageBox.Show("Không tìm thấy thư mục nào có tên giống nhau trước dấu gạch nối để gộp (No matching folders found to merge).", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

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
                int splitCount = await SplitFoldersInTargetFolderAsync(targetFolder, CancellationToken.None);

                Log("[Split] Đang tạm ngừng 3 giây để hệ thống ổn định và nhận biết thư mục...");
                await Task.Delay(3000);

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

        internal async Task AutoMergeChapterFolderAsync(string unmergedPath, string mergedPath, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(unmergedPath) ||
                string.IsNullOrWhiteSpace(mergedPath) ||
                string.Equals(unmergedPath, mergedPath, StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(unmergedPath))
            {
                return;
            }

            await _folderStructureSemaphore.WaitAsync(token);
            try
            {
                string mergedParent = Path.GetDirectoryName(mergedPath);
                if (string.IsNullOrEmpty(mergedParent))
                {
                    return;
                }

                Directory.CreateDirectory(mergedParent);
                if (Directory.Exists(mergedPath))
                {
                    MergeDirectoryContents(unmergedPath, mergedPath);
                }
                else
                {
                    Directory.Move(unmergedPath, mergedPath);
                }

                Log($"[Auto Merge] Đã gộp tự động '{Path.GetFileName(unmergedPath)}' -> '{mergedPath}'");
            }
            finally
            {
                _folderStructureSemaphore.Release();
            }
        }

        private async Task<int> MergeFoldersInTargetFolderAsync(string targetFolder, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder))
            {
                return 0;
            }

            await _folderStructureSemaphore.WaitAsync(token);
            try
            {
                var directories = Directory.GetDirectories(targetFolder);
                var groups = directories
                    .Select(dir => new { Path = dir, Name = Path.GetFileName(dir) })
                    .Where(d => !ShouldIgnoreFolderStructureAction(d.Name))
                    .Where(d => d.Name.Contains("-"))
                    .Select(d =>
                    {
                        int index = d.Name.IndexOf('-');
                        string prefix = d.Name.Substring(0, index).Trim();
                        string suffix = d.Name.Substring(index + 1).Trim();
                        return new { d.Path, d.Name, Prefix = prefix, Suffix = suffix };
                    })
                    .GroupBy(d => d.Prefix, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() >= 2)
                    .ToList();

                int mergedCount = 0;
                foreach (var group in groups)
                {
                    string groupPrefix = group.Key;
                    string destParentDir = Path.Combine(targetFolder, groupPrefix);
                    Directory.CreateDirectory(destParentDir);

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

                return mergedCount;
            }
            finally
            {
                _folderStructureSemaphore.Release();
            }
        }

        private async Task<int> SplitFoldersInTargetFolderAsync(string targetFolder, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder))
            {
                return 0;
            }

            await _folderStructureSemaphore.WaitAsync(token);
            try
            {
                var directories = Directory.GetDirectories(targetFolder);
                int splitCount = 0;

                foreach (var dir in directories)
                {
                    string parentName = Path.GetFileName(dir);
                    if (ShouldIgnoreFolderStructureAction(parentName))
                    {
                        continue;
                    }

                    var subDirs = Directory.GetDirectories(dir);
                    if (subDirs.Length < 2)
                    {
                        continue;
                    }

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

                return splitCount;
            }
            finally
            {
                _folderStructureSemaphore.Release();
            }
        }

        private bool ShouldIgnoreFolderStructureAction(string folderName)
        {
            return string.IsNullOrWhiteSpace(folderName) ||
                   folderName.StartsWith(".", StringComparison.OrdinalIgnoreCase) ||
                   folderName.EndsWith("-tmp", StringComparison.OrdinalIgnoreCase);
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
            Dispatcher.Invoke(() =>
            {
                if (tabLeftPanel == null) return;

                if (tabLeftPanel.SelectedIndex == 0)
                {
                    if (tabManga != null && tabManga.SelectedItem is System.Windows.Controls.TabItem selectedMangaTab)
                    {
                        string header = selectedMangaTab.Header?.ToString().ToLower() ?? "";
                        if (header.Contains("truyenqq"))
                            subFolder = "truyenqq";
                        else if (header.Contains("nettruyen"))
                            subFolder = "nettruyen";
                    }
                    else
                    {
                        subFolder = "truyenqq";
                    }
                }
                else if (tabLeftPanel.SelectedIndex == 1)
                {
                    if (tabHentai != null && tabHentai.SelectedItem is System.Windows.Controls.TabItem selectedHentaiTab)
                    {
                        string header = selectedHentaiTab.Header?.ToString().ToLower() ?? "";
                        if (header.Contains("hentaiforce"))
                            subFolder = "hentaiforce.net";
                        else if (header.Contains("nhentai"))
                            subFolder = "nhentai.net";
                        else if (header.Contains("hentaivn"))
                            subFolder = "vi-hentai.pro";
                        else if (header.Contains("hentaiera"))
                            subFolder = "hentaiera.com";
                    }
                }
            });

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
