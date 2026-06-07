using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private readonly SemaphoreSlim _folderStructureSemaphore = new SemaphoreSlim(1, 1);

        private async void BtnCompressBooks_Click(object sender, RoutedEventArgs e)
        {
            if (IsArchiveOperationBlocked())
            {
                MessageBox.Show("Hãy dừng toàn bộ download trước khi nén sách.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string sourceFolder = PortablePaths.DefaultDownloadRoot;
            string archivePath = PortablePaths.PortableArchivePath;

            if (!Directory.Exists(sourceFolder))
            {
                MessageBox.Show($"Không tìm thấy folder để nén:\n{sourceFolder}", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!EnsureSevenZipReady())
            {
                MessageBox.Show("Không thể khởi tạo 7-Zip portable.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            lblStatus.Text = "Compressing books...";

            try
            {
                if (File.Exists(archivePath))
                {
                    File.Delete(archivePath);
                }

                string arguments = $"a -t7z -mx=9 -y \"{Path.GetFileName(archivePath)}\" \"{Path.GetFileName(sourceFolder)}\"";
                string output = await RunSevenZipAsync(arguments);

                if (!File.Exists(archivePath))
                {
                    throw new InvalidOperationException("7z không tạo ra file nén.");
                }

                Directory.Delete(sourceFolder, true);

                Log($"[Archive] Đã nén portable folder thành công: {archivePath}");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    Log($"[Archive] 7z: {output}");
                }

                lblStatus.Text = "Books compressed successfully.";
                MessageBox.Show($"Đã nén xong thành file:\n{archivePath}\n\nFolder gốc đã được xóa.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"[Archive Error] Không thể nén sách: {ex.Message}");
                lblStatus.Text = "Compress failed.";
                MessageBox.Show($"Lỗi khi nén sách: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnExtractBooks_Click(object sender, RoutedEventArgs e)
        {
            if (IsArchiveOperationBlocked())
            {
                MessageBox.Show("Hãy dừng toàn bộ download trước khi giải nén sách.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string sourceFolder = PortablePaths.DefaultDownloadRoot;
            string archivePath = PortablePaths.PortableArchivePath;

            if (!File.Exists(archivePath))
            {
                MessageBox.Show($"Không tìm thấy file nén:\n{archivePath}", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!EnsureSevenZipReady())
            {
                MessageBox.Show("Không thể khởi tạo 7-Zip portable.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            lblStatus.Text = "Extracting books...";

            try
            {
                if (Directory.Exists(sourceFolder))
                {
                    Directory.Delete(sourceFolder, true);
                }

                string arguments = $"x -y \"{Path.GetFileName(archivePath)}\" -o\"{PortablePaths.AppRoot}\"";
                string output = await RunSevenZipAsync(arguments);

                if (!Directory.Exists(sourceFolder))
                {
                    throw new InvalidOperationException("7z đã chạy nhưng không bung ra folder sách.");
                }

                File.Delete(archivePath);

                Log($"[Archive] Đã giải nén portable folder thành công: {sourceFolder}");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    Log($"[Archive] 7z: {output}");
                }

                lblStatus.Text = "Books extracted successfully.";
                MessageBox.Show($"Đã giải nén xong folder:\n{sourceFolder}\n\nFile nén đã được xóa.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"[Archive Error] Không thể giải nén sách: {ex.Message}");
                lblStatus.Text = "Extract failed.";
                MessageBox.Show($"Lỗi khi giải nén sách: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

        private bool IsArchiveOperationBlocked()
        {
            if (_downloadCts != null)
            {
                return true;
            }

            return _scrapedItems.Any(item =>
                string.Equals(item.Status, "Downloading", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Status, "Queued", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Status, "Paused", StringComparison.OrdinalIgnoreCase));
        }

        private bool EnsureSevenZipReady()
        {
            PortableArchiveBootstrap.EnsurePortableSevenZip();
            return File.Exists(PortablePaths.SevenZipExePath);
        }

        private async Task<string> RunSevenZipAsync(string arguments)
        {
            return await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = PortablePaths.SevenZipExePath,
                    Arguments = arguments,
                    WorkingDirectory = PortablePaths.AppRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        throw new InvalidOperationException("Không thể khởi chạy 7z.exe.");
                    }

                    string standardOutput = process.StandardOutput.ReadToEnd();
                    string standardError = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        string errorText = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
                        throw new InvalidOperationException($"7z exit code {process.ExitCode}. {errorText}".Trim());
                    }

                    string mergedOutput = string.Join(Environment.NewLine,
                        new[] { standardOutput, standardError }.Where(text => !string.IsNullOrWhiteSpace(text)).Select(text => text.Trim()));

                    return CompactSingleLine(mergedOutput);
                }
            });
        }

        private static string CompactSingleLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            bool lastWasWhitespace = false;
            foreach (char ch in text)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!lastWasWhitespace)
                    {
                        builder.Append(' ');
                        lastWasWhitespace = true;
                    }

                    continue;
                }

                builder.Append(ch);
                lastWasWhitespace = false;
            }

            return builder.ToString().Trim();
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
