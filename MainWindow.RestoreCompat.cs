using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private bool? _lastLatestChapterSortDescending;
        private string _lastNhentaiResolvedHtml;
        private const string DefaultListFileName = "gallery-list-default.txt";
        private const string AutosaveListFileName = "gallery-list-autosave.txt";

        private sealed class GallerySnapshot
        {
            public bool IsChecked { get; set; }
            public string Link { get; set; }
            public string Name { get; set; }
            public int OriginalIndex { get; set; }
            public string LinkCount { get; set; }
            public string SourceDomain { get; set; }
            public bool IsDuplicate { get; set; }
            public bool HasNoChapters { get; set; }
            public int NhentaiTotalPagesHint { get; set; }
        }

        private string DefaultListPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultListFileName);
        private string AutosaveListPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AutosaveListFileName);

        private void InitializeGalleryListAutosave()
        {
            TryLoadGalleryListFile(AutosaveListPath, showMessage: false);
        }

        private void SaveActiveGalleryListSnapshot()
        {
            try
            {
                SaveGalleryListToFile(AutosaveListPath);
            }
            catch (Exception ex)
            {
                Log($"Autosave failed: {ex.Message}");
            }
        }

        private void BtnNewList_Click(object sender, RoutedEventArgs e)
        {
            _scrapedItems.Clear();
            if (dgResults != null)
            {
                dgResults.Items.Refresh();
            }
            if (lblLinkCount != null)
            {
                lblLinkCount.Text = "0";
            }
            if (chkSelectAll != null)
            {
                chkSelectAll.IsChecked = false;
            }
            lblStatus.Text = _isVietnameseUi ? "Đã tạo danh sách mới." : "Started a new list.";
            RecalculateDuplicates();
        }

        private void BtnSaveCustom_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "List files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = "gallery-list.txt"
            };

            if (dialog.ShowDialog(this) == true)
            {
                SaveGalleryListToFile(dialog.FileName);
                lblStatus.Text = _isVietnameseUi ? "Đã lưu danh sách." : "List saved.";
            }
        }

        private void BtnLoadCustom_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "List files (*.txt)|*.txt|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) == true)
            {
                TryLoadGalleryListFile(dialog.FileName, showMessage: true);
            }
        }

        private void BtnOpenHomepage_Click(object sender, RoutedEventArgs e)
        {
            string tag = (sender as FrameworkElement)?.Tag?.ToString()?.Trim().ToLowerInvariant();
            string url = null;

            switch (tag)
            {
                case "truyenqq":
                    url = "https://truyenqqko.com/";
                    break;
                case "nettruyen":
                    url = "https://nettruyenrr.com/";
                    break;
                case "daomeoden":
                    url = "https://daomeoden.net/";
                    break;
                case "vihentai":
                    url = "https://vi-hentai.pro/";
                    break;
                case "sayhentai":
                    url = "https://sayhentai.cx/";
                    break;
                case "hentaiforce":
                    url = "https://hentaiforce.net/";
                    break;
                case "nhentai":
                    url = "https://nhentai.net/";
                    break;
                case "hentaiera":
                    url = "https://hentaiera.com/";
                    break;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở trang chủ: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task PasteDirectLinksFromClipboardAsync(
            Func<List<string>, System.Threading.Tasks.Task> importFunc,
            string emptyMessage)
        {
            try
            {
                string text = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
                var links = text
                    .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (links.Count == 0)
                {
                    MessageBox.Show(emptyMessage, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                await importFunc(links);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể dán link trực tiếp: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<GalleryItem> OrderItemsByDisplayOrder(IEnumerable<GalleryItem> items)
        {
            var itemSet = new HashSet<GalleryItem>(items);
            var viewItems = ResultsView?.Cast<object>()
                .OfType<GalleryItem>()
                .Where(itemSet.Contains)
                .ToList();

            if (viewItems != null && viewItems.Count == itemSet.Count)
            {
                return viewItems;
            }

            return items.OrderBy(item => item.OriginalIndex).ToList();
        }

        private void BtnSortByLatestChapter_Click(object sender, RoutedEventArgs e)
        {
            var view = ResultsView;
            if (view == null)
            {
                return;
            }

            bool descending = _lastLatestChapterSortDescending != true;
            _lastLatestChapterSortDescending = descending;

            var sorted = _scrapedItems
                .OrderByDescending(item => descending ? ExtractNumber(item.LinkCount) : -ExtractNumber(item.LinkCount))
                .ThenBy(item => item.OriginalIndex)
                .ToList();

            _scrapedItems.Clear();
            foreach (var item in sorted)
            {
                _scrapedItems.Add(item);
            }

            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription("OriginalIndex", ListSortDirection.Ascending));
            UpdateLatestChapterButtonLabel();
            Log(descending ? "Sorted by latest chapter descending." : "Sorted by latest chapter ascending.");
        }

        private void MenuInvertSelectedRows_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in dgResults.SelectedItems.Cast<GalleryItem>())
            {
                item.IsChecked = !item.IsChecked;
            }
            Log("Inverted checked state for selected rows.");
        }

        private void BtnSortByStatus_Click(object sender, RoutedEventArgs e)
        {
            var view = ResultsView;
            if (view == null)
            {
                return;
            }

            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription("Status", ListSortDirection.Ascending));
            view.SortDescriptions.Add(new SortDescription("OriginalIndex", ListSortDirection.Ascending));
            Log("Sorted by status.");
        }

        private void BtnSortByProcess_Click(object sender, RoutedEventArgs e)
        {
            var view = ResultsView;
            if (view == null)
            {
                return;
            }

            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription("CurrentProcess", ListSortDirection.Ascending));
            view.SortDescriptions.Add(new SortDescription("OriginalIndex", ListSortDirection.Ascending));
            Log("Sorted by process.");
        }

        private void BtnArchivePause_Click(object sender, RoutedEventArgs e)
        {
            _isDownloadPaused = !_isDownloadPaused;
            if (btnArchivePause != null)
            {
                btnArchivePause.Content = _isDownloadPaused ? "RESUME" : "PAUSE";
            }
            lblStatus.Text = _isDownloadPaused
                ? (_isVietnameseUi ? "Đã tạm dừng thao tác lưu trữ." : "Archive task paused.")
                : (_isVietnameseUi ? "Đã tiếp tục thao tác lưu trữ." : "Archive task resumed.");
        }

        private void BtnArchiveStop_Click(object sender, RoutedEventArgs e)
        {
            _isDownloadPaused = false;
            if (archiveProgressPanel != null)
            {
                archiveProgressPanel.Visibility = Visibility.Collapsed;
            }
            if (btnArchivePause != null)
            {
                btnArchivePause.Content = "PAUSE";
            }
            lblStatus.Text = _isVietnameseUi ? "Đã dừng thao tác lưu trữ." : "Archive task stopped.";
        }

        private async void BtnRetryErrors_Click(object sender, RoutedEventArgs e)
        {
            var erroredItems = _scrapedItems
                .Where(item => item.GetUniqueErrorCount() > 0)
                .ToList();

            if (erroredItems.Count == 0)
            {
                MessageBox.Show(_isVietnameseUi ? "Không có lỗi để thử lại." : "No errors to retry.",
                    "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in erroredItems)
            {
                await RetryDownloadQueueItemErrorsAsync(item, showMessageBox: false);
            }

            MessageBox.Show(
                _isVietnameseUi ? "Đã thử tải lại toàn bộ mục bị lỗi." : "Retried all errored items.",
                "Retry", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRetryErrorLog_Click(object sender, RoutedEventArgs e)
        {
            var erroredItems = _scrapedItems
                .Where(item => item.GetUniqueErrorCount() > 0)
                .ToList();

            if (erroredItems.Count == 0)
            {
                MessageBox.Show(_isVietnameseUi ? "Không có lỗi để hiển thị." : "No error logs available.",
                    "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var summary = string.Join(Environment.NewLine,
                erroredItems.Select(item => $"{item.Name}: {item.GetUniqueErrorCount()} error(s)"));
            MessageBox.Show(summary, "Error Log", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnOpenFolderInRow_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.DataContext as GalleryItem;
            if (item == null || string.IsNullOrWhiteSpace(item.DownloadPath))
            {
                return;
            }

            if (!Directory.Exists(item.DownloadPath))
            {
                MessageBox.Show($"Thư mục không tồn tại:\n{item.DownloadPath}", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ShellFolderLauncher.TryOpenFolder(item.DownloadPath, out string error))
            {
                MessageBox.Show($"Không thể mở thư mục: {error}", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveGalleryListToFile(string path)
        {
            var lines = _scrapedItems.Select(item => string.Join("\t", new[]
            {
                item.IsChecked ? "1" : "0",
                EncodeCell(item.Name),
                EncodeCell(item.Link),
                item.OriginalIndex.ToString(),
                EncodeCell(item.LinkCount),
                EncodeCell(item.SourceDomain),
                item.HasNoChapters ? "1" : "0",
                item.NhentaiTotalPagesHint.ToString()
            }));

            File.WriteAllLines(path, lines, Encoding.UTF8);
        }

        private void TryLoadGalleryListFile(string path, bool showMessage)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                var loaded = File.ReadAllLines(path, Encoding.UTF8)
                    .Select(ParseGallerySnapshot)
                    .Where(item => item != null)
                    .ToList();

                if (loaded.Count == 0)
                {
                    return;
                }

                _scrapedItems.Clear();
                foreach (var item in loaded)
                {
                    _scrapedItems.Add(item);
                }

                lblLinkCount.Text = _scrapedItems.Count.ToString();
                RecalculateDuplicates();

                if (showMessage)
                {
                    MessageBox.Show(
                        _isVietnameseUi ? "Đã tải danh sách thành công." : "List loaded successfully.",
                        "Load", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải danh sách: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private GalleryItem ParseGallerySnapshot(string line)
        {
            var parts = line.Split('\t');
            if (parts.Length < 8)
            {
                return null;
            }

            int.TryParse(parts[3], out int originalIndex);
            int.TryParse(parts[7], out int nhentaiPagesHint);

            return new GalleryItem
            {
                IsChecked = parts[0] == "1",
                Name = DecodeCell(parts[1]),
                Link = DecodeCell(parts[2]),
                OriginalIndex = originalIndex,
                LinkCount = DecodeCell(parts[4]),
                SourceDomain = DecodeCell(parts[5]),
                HasNoChapters = parts[6] == "1",
                NhentaiTotalPagesHint = nhentaiPagesHint
            };
        }

        private static string EncodeCell(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string DecodeCell(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty));
            }
            catch
            {
                return value ?? string.Empty;
            }
        }
    }
}
