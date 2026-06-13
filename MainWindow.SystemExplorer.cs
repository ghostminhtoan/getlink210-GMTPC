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
                Title = "Chọn thư mục lưu truyện (Select Download Folder)"
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
                MessageBox.Show("Vui lòng chọn thư mục lưu trước (Please select a download folder first).", "Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string targetFolder = GetActiveTargetFolder(downloadRoot);
            EnsureFolderExistsForExplorer(targetFolder);

            if (!ShellFolderLauncher.TryOpenFolder(targetFolder, out string openError))
            {
                MessageBox.Show($"Không thể mở thư mục: {openError}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                MessageBox.Show("Chưa xác định được thư mục của truyện này.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(targetFolder))
            {
                MessageBox.Show($"Thư mục không tồn tại:\n{targetFolder}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ShellFolderLauncher.TryOpenFolder(targetFolder, out string error))
            {
                MessageBox.Show($"Không thể mở thư mục: {error}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    }
                }
            });

            return string.IsNullOrEmpty(siteKey)
                ? downloadRoot
                : GetConfiguredDownloadRoot(downloadRoot, siteKey);
        }
    }
}
