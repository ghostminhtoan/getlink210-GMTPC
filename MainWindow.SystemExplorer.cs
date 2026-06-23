using System;
using System.IO;
using System.Windows;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowser
            {
                Title = _isVietnameseUi ? "Chọn thư mục lưu truyện" : "Select download folder",
                InitialFolder = PortablePaths.PortableDataRoot
            };

            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (dialog.ShowDialog(hwnd))
            {
                txtDownloadPath.Text = dialog.SelectedPath;
                Log($"Download path updated to: {dialog.SelectedPath}");
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string downloadRoot = txtDownloadPath.Text.Trim();
            if (string.IsNullOrEmpty(downloadRoot))
            {
                ShowLocalizedMessageBox(
                    "Please select a download folder first.",
                    "Vui lòng chọn thư mục lưu trước.",
                    "Information",
                    "Thông tin",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string targetFolder = GetActiveTargetFolder(downloadRoot);
            EnsureFolderExistsForExplorer(targetFolder);

            if (!ShellFolderLauncher.TryOpenFolder(targetFolder, out string openError))
            {
                ShowLocalizedMessageBox(
                    $"Cannot open folder: {openError}",
                    $"Không thể mở thư mục: {openError}",
                    "Error",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnClearTemp_Click(object sender, RoutedEventArgs e)
        {
            string downloadRoot = txtDownloadPath?.Text?.Trim() ?? string.Empty;
            ClearTempRootFolder(Path.Combine(PortablePaths.AppRoot, ".tmp"));
            if (string.IsNullOrWhiteSpace(downloadRoot))
            {
                ShowLocalizedMessageBox(
                    "Please select a download folder first.",
                    "Vui lòng chọn thư mục tải trước.",
                    "Information",
                    "Thông tin",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string tempRoot = Path.Combine(downloadRoot, ".tmp");
            if (!Directory.Exists(tempRoot))
            {
                Log($"Temp root not found: {tempRoot}");
                lblStatus.Text = _isVietnameseUi ? "Temp đã sạch." : "Temp is already clean.";
                return;
            }

            ClearTempRootFolder(tempRoot);
            Log($"Cleared temp root: {tempRoot}");
            lblStatus.Text = _isVietnameseUi ? "Temp đã được xóa." : "Temp cleared.";
        }

        private void BtnOpenFolderInRow_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.DataContext as GalleryItem;
            if (item == null)
            {
                return;
            }

            string targetFolder = ResolveBestFolderForGalleryItem(item);
            if (string.IsNullOrWhiteSpace(targetFolder))
            {
                ShowLocalizedMessageBox(
                    "Cannot determine this book folder.",
                    "Chưa xác định được thư mục của truyện này.",
                    "Warning",
                    "Cảnh báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(targetFolder))
            {
                ShowLocalizedMessageBox(
                    $"Folder does not exist:\n{targetFolder}",
                    $"Thư mục không tồn tại:\n{targetFolder}",
                    "Warning",
                    "Cảnh báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!ShellFolderLauncher.TryOpenFolder(targetFolder, out string error))
            {
                ShowLocalizedMessageBox(
                    $"Cannot open folder: {error}",
                    $"Không thể mở thư mục: {error}",
                    "Warning",
                    "Cảnh báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void EnsureFolderExistsForExplorer(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path))
            {
                return;
            }

            Directory.CreateDirectory(path);
        }

        private void ClearTempRootFolder(string tempRoot)
        {
            try
            {
                foreach (string file in Directory.GetFiles(tempRoot))
                {
                    TryDeletePath(file);
                }

                foreach (string directory in Directory.GetDirectories(tempRoot))
                {
                    TryDeletePath(directory);
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to clear temp root '{tempRoot}': {ex.Message}");
            }
        }

        private void TryDeletePath(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                Log($"Temp cleanup skip '{path}': {ex.Message}");
            }
        }

        private string ResolveBestFolderForGalleryItem(GalleryItem item)
        {
            string downloadRoot = item.DownloadPath;
            if (string.IsNullOrWhiteSpace(downloadRoot))
            {
                downloadRoot = txtDownloadPath != null ? txtDownloadPath.Text.Trim() : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(downloadRoot))
            {
                return null;
            }

            string siteFolder = GetDownloadSiteKey(item);
            string scopedRoot = GetConfiguredDownloadRoot(downloadRoot, siteFolder);
            string safeTitle = GetSafePathName(item.Name);
            string[] candidates =
            {
                Path.Combine(scopedRoot, safeTitle),
                scopedRoot,
                Path.Combine(downloadRoot, siteFolder, safeTitle),
                Path.Combine(downloadRoot, siteFolder),
                Path.Combine(downloadRoot, "hentaiera", safeTitle),
                Path.Combine(downloadRoot, "hentaiera"),
                downloadRoot
            };

            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return candidates[0];
        }

        private string GetActiveTargetFolder(string downloadRoot)
        {
            string siteKey = "";
            Dispatcher.Invoke(() =>
            {
                if (tabLeftPanel == null) return;

                if (tabLeftPanel.SelectedIndex == 0)
                {
                    if (tabManga != null && tabManga.SelectedItem is System.Windows.Controls.TabItem selectedMangaTab)
                    {
                        string header = selectedMangaTab.Header?.ToString().ToLower() ?? "";
                        if (header.Contains("truyenqq"))
                            siteKey = "truyenqq";
                        else if (header.Contains("nettruyen"))
                            siteKey = "nettruyen";
                        else if (header.Contains("daomeoden"))
                            siteKey = "daomeoden.net";
                        else if (header.Contains("truyengg"))
                            siteKey = "truyenggvn";
                    }
                    else
                    {
                        siteKey = "truyenqq";
                    }
                }
                else if (tabLeftPanel.SelectedIndex == 1)
                {
                    if (tabHentai != null && tabHentai.SelectedItem is System.Windows.Controls.TabItem selectedHentaiTab)
                    {
                        string header = selectedHentaiTab.Header?.ToString().ToLower() ?? "";
                        if (header.Contains("hentaiforce"))
                            siteKey = "hentaiforce.net";
                        else if (header.Contains("nhentai"))
                            siteKey = "nhentai.xxx";
                        else if (header.Contains("hentaivn"))
                            siteKey = "vi-hentai.pro";
                        else if (header.Contains("hentaiera"))
                            siteKey = "hentaiera.com";
                        else if (header.Contains("hentai2read"))
                            siteKey = "hentai2read.com";
                        else if (header.Contains("daomeoden"))
                            siteKey = "daomeoden.net";
                    }
                }
                else if (tabLeftPanel.SelectedIndex == 2)
                {
                    if (tabLightNovel != null && tabLightNovel.SelectedItem is System.Windows.Controls.TabItem selectedLightNovelTab)
                    {
                        string header = selectedLightNovelTab.Header?.ToString().ToLower() ?? "";
                        if (header.Contains("hako"))
                            siteKey = "ln.hako.vn";
                    }
                }
            });

            return string.IsNullOrEmpty(siteKey)
                ? downloadRoot
                : GetConfiguredDownloadRoot(downloadRoot, siteKey);
        }
    }
}
