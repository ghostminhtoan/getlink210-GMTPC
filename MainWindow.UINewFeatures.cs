using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace get_link_manga
{
    /// <summary>
    /// Partial class for new UI features: Bookmarks, History, Download Queue,
    /// Open Link/Download buttons, and Chapter Selection.
    /// </summary>
    public partial class MainWindow : Window
    {
        // ===== BOOKMARK & HISTORY =====

        private void BtnOpenBookmarks_Click(object sender, RoutedEventArgs e)
        {
            OpenBookmarkHistoryWindow(1); // Bookmarks is index 1
        }

        private void BtnOpenHistory_Click(object sender, RoutedEventArgs e)
        {
            OpenBookmarkHistoryWindow(0); // History is index 0
        }

        private void OpenBookmarkHistoryWindow(int selectedTab = 0)
        {
            if (_bookmarkHistoryWindowInstance != null)
            {
                if (_bookmarkHistoryWindowInstance.WindowState == WindowState.Minimized)
                    _bookmarkHistoryWindowInstance.WindowState = WindowState.Normal;
                _bookmarkHistoryWindowInstance.SelectTab(selectedTab);
                _bookmarkHistoryWindowInstance.Activate();
            }
            else
            {
                _bookmarkHistoryWindowInstance = new BookmarkHistoryWindow
                {
                    Owner = this
                };
                _bookmarkHistoryWindowInstance.Closed += (s, args) => { _bookmarkHistoryWindowInstance = null; };
                _bookmarkHistoryWindowInstance.SelectTab(selectedTab);
                _bookmarkHistoryWindowInstance.Show();
            }
        }

        private void MenuBookmarkSelected_Click(object sender, RoutedEventArgs e)
        {
            if (dgResults.SelectedItems.Count == 0) return;

            int count = 0;
            foreach (var item in dgResults.SelectedItems.Cast<GalleryItem>())
            {
                string domain = "";
                try { domain = new Uri(item.Link).Host; } catch { }

                _bookmarkManager.AddBookmark(new BookmarkEntry
                {
                    Name = item.Name,
                    Url = item.Link,
                    SourceDomain = domain,
                    BookmarkedAt = DateTime.Now
                });
                count++;
            }

            Log($"Đã bookmark {count} truyện.");

            // Refresh bookmark window if open
            if (_bookmarkHistoryWindowInstance != null)
            {
                _bookmarkHistoryWindowInstance.RefreshBookmarks();
            }
        }

        private void MenuOpenInBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (dgResults.SelectedItem is GalleryItem item && !string.IsNullOrEmpty(item.Link))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = item.Link,
                        UseShellExecute = true
                    });
                    Log($"🌐 Opened: {item.Link}");
                }
                catch (Exception ex)
                {
                    Log($"Failed to open link: {ex.Message}");
                }
            }
        }

        // ===== ROW ACTION BUTTONS (🌐 Open Link, ⬇️ Download Single) =====

        private void BtnOpenLinkInRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string url && !string.IsNullOrEmpty(url))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                    Log($"🌐 Opened: {url}");
                }
                catch (Exception ex)
                {
                    Log($"Failed to open link: {ex.Message}");
                }
            }
        }

        private async void BtnDownloadSingleInRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // Get the DataContext (GalleryItem) from the button
                var item = btn.DataContext as GalleryItem;
                if (item == null) return;

                string downloadRoot = txtDownloadPath.Text.Trim();
                if (string.IsNullOrEmpty(downloadRoot))
                {
                    MessageBox.Show("Vui lòng chọn thư mục lưu trước.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await StartDownloadProcessAsync(new List<GalleryItem> { item }, preserveExistingState: true);
            }
        }

        // ===== DOWNLOAD QUEUE =====

        private void BtnShowErrors_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GalleryItem item)
            {
                if (item.ErrorCount > 0)
                {
                    var errorWindow = new ErrorReportWindow(item, this)
                    {
                        Owner = this
                    };
                    errorWindow.Show();
                }
            }
        }

        private async void BtnRowResume_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GalleryItem item)
            {
                if (_downloadCts == null && (item.IsPaused || string.Equals(item.Status, "Paused", StringComparison.OrdinalIgnoreCase) || string.Equals(item.Status, "Downloading", StringComparison.OrdinalIgnoreCase)))
                {
                    Log($"[Local Actions] Restarting saved download for '{item.Name}'");
                    await StartDownloadProcessAsync(new System.Collections.Generic.List<GalleryItem> { item }, preserveExistingState: true);
                    return;
                }

                item.IsPaused = false;
                item.Status = "Downloading";
                Log($"[Local Actions] Resumed download for '{item.Name}'");
            }
        }

        private void BtnRowPause_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GalleryItem item)
            {
                item.IsPaused = true;
                item.Status = "Paused";
                Log($"[Local Actions] Paused download for '{item.Name}'");
            }
        }

        private void BtnRowStop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GalleryItem item)
            {
                item.IsStopped = true;
                item.Status = "Cancelled";
                Log($"[Local Actions] Stopped download for '{item.Name}'");
            }
        }

        internal void UpdateQueueErrorLabel()
        {
            // No-op since label was removed from UI
        }

        /// <summary>
        /// Add a gallery to the download history after successful download.
        /// </summary>
        internal void AddToHistory(GalleryItem item, int chaptersDownloaded, string downloadPath)
        {
            string domain = "";
            try { domain = new Uri(item.Link).Host; } catch { }

            _bookmarkManager.AddHistory(new HistoryEntry
            {
                Name = item.Name,
                Url = item.Link,
                SourceDomain = domain,
                DownloadedAt = DateTime.Now,
                ChaptersDownloaded = chaptersDownloaded,
                DownloadPath = downloadPath
            });

            // Refresh history window if open
            if (_bookmarkHistoryWindowInstance != null)
            {
                Dispatcher.Invoke(() => _bookmarkHistoryWindowInstance.RefreshHistory());
            }
        }

        /// <summary>
        /// Parse the chapter selection textbox and return the filter HashSet.
        /// Returns null if empty (download all). Shows error message if invalid.
        /// </summary>
        internal ChapterFilter GetChapterSelectionFilter()
        {
            string input = "";
            Dispatcher.Invoke(() => { input = txtChapterSelection?.Text?.Trim() ?? ""; });

            if (string.IsNullOrEmpty(input))
                return null; // Download all

            if (ChapterRangeParser.TryParse(input, out var result, out string errorMsg))
            {
                if (result != null)
                {
                    string display = ChapterRangeParser.ToDisplayString(result);
                    Log($"Chapter filter applied: {display}");
                }
                return result;
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Cú pháp chọn chapter không hợp lệ:\n{errorMsg}\n\nVí dụ: 324-328;324.5",
                        "Chapter Selection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
                return null;
            }
        }

        /// <summary>
        /// Thử tải lại các trang bị lỗi của một GalleryItem.
        /// </summary>
        public async Task RetryDownloadQueueItemErrorsAsync(GalleryItem queueItem, bool showMessageBox = true)
        {
            if (queueItem == null)
                return;

            var errorsToRetry = queueItem.GetUniqueErrors();
            if (errorsToRetry.Count == 0)
                return;

            // Clear current errors before retrying so we can track new ones
            Dispatcher.Invoke(() => {
                queueItem.Errors.Clear();
                queueItem.ErrorCount = 0;
                queueItem.Status = "Downloading";
                queueItem.CurrentProcess = "Retrying errors...";
            });

            Log($"[Retry] Đang thử tải lại {errorsToRetry.Count} trang bị lỗi của '{queueItem.Name}'...");

            int successfulRetries = 0;
            int failedRetries = 0;

            foreach (var err in errorsToRetry)
            {
                try
                {
                    string targetFolder;
                    bool isViHentai = queueItem.SourceDomain != null && queueItem.SourceDomain.Contains("vi-hentai.pro");
                    bool isTruyenqq = queueItem.SourceDomain != null && queueItem.SourceDomain.Contains("truyenqq");
                    string imageUrl = err.ImageUrl;

                    if (string.IsNullOrEmpty(imageUrl) && isTruyenqq)
                    {
                        imageUrl = await ResolveTruyenqqRetryImageUrlAsync(queueItem, err);
                    }

                    if (string.IsNullOrEmpty(imageUrl))
                    {
                        throw new Exception("No image URL available");
                    }

                    string safeManga = GetSafePathName(queueItem.Name);
                    
                    if (isViHentai)
                    {
                        string safeChapter = GetSafePathName(err.ChapterName);
                        targetFolder = Path.Combine(queueItem.DownloadPath, "vi-hentai.pro", $"{safeManga}-{safeChapter}");
                    }
                    else if (isTruyenqq)
                    {
                        string safeChapter = GetSafePathName(err.ChapterName);
                        targetFolder = Path.Combine(queueItem.DownloadPath, "truyenqq", $"{safeManga}-{safeChapter}");
                    }
                    else
                    {
                        targetFolder = Path.Combine(queueItem.DownloadPath, queueItem.SourceDomain ?? "", safeManga);
                    }

                    Directory.CreateDirectory(targetFolder);

                    string ext = Path.GetExtension(imageUrl.Split('?')[0]);
                    if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                    string fileName = $"{err.PageNumber:D3}{ext}";
                    string finalFilePath = Path.Combine(targetFolder, fileName);

                    var token = _downloadCts?.Token ?? System.Threading.CancellationToken.None;

                    await DownloadUrlToFileWithRefererAsync(
                        imageUrl,
                        queueItem.Link, 
                        finalFilePath, 
                        token, 
                        isViHentai: isViHentai, 
                        isTruyenqq: isTruyenqq
                    );

                    successfulRetries++;
                    Log($"[Retry] Đã tải thành công: {err.ChapterName}, Trang {err.PageNumber}");
                }
                catch (Exception ex)
                {
                    failedRetries++;
                    Log($"[Retry] Thất bại khi tải {err.ChapterName}, Trang {err.PageNumber}: {ex.Message}");
                    Dispatcher.Invoke(() => {
                        queueItem.AddError(err.ChapterName, err.PageNumber, ex.Message, err.ImageUrl);
                    });
                }

                Dispatcher.Invoke(() => {
                    queueItem.CurrentProcess = $"Retry: {successfulRetries} ok, {failedRetries} fail";
                });
            }

            Dispatcher.Invoke(() => {
                queueItem.Status = queueItem.GetUniqueErrorCount() > 0 ? "Error" : "Completed";
                queueItem.CurrentProcess = queueItem.GetUniqueErrorCount() > 0 ? "Done with errors" : "Done";
            });

            UpdateQueueErrorLabel();

            Log($"[Retry] Hoàn tất thử tải lại cho '{queueItem.Name}'. Thành công: {successfulRetries}/{errorsToRetry.Count}.");
            if (showMessageBox)
            {
                MessageBox.Show($"Thử tải lại hoàn tất!\nThành công: {successfulRetries}\nThất bại: {failedRetries}", 
                    "Retry Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task<string> ResolveTruyenqqRetryImageUrlAsync(GalleryItem queueItem, ErrorDetail err)
        {
            string chapterLink = FindTruyenqqChapterLinkForRetry(queueItem, err.ChapterName);
            if (string.IsNullOrWhiteSpace(chapterLink) || err.PageNumber <= 0)
            {
                return null;
            }

            string html = await _httpClient.GetStringAsync(chapterLink);
            string safeHtml = GetSafeChapterHtml(html);
            var imageUrls = ExtractTruyenqqImageUrls(safeHtml, chapterLink);
            int index = err.PageNumber - 1;
            if (index < 0 || index >= imageUrls.Count)
            {
                return null;
            }

            return imageUrls[index];
        }

        private string FindTruyenqqChapterLinkForRetry(GalleryItem queueItem, string chapterName)
        {
            if (queueItem == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(queueItem.Link) && Regex.IsMatch(queueItem.Link, @"(?:chap|chapter|chuong)", RegexOptions.IgnoreCase))
            {
                return queueItem.Link;
            }

            string processPath = GetExistingDownloadProcessFilePath(queueItem.DownloadPath, "truyenqq", queueItem);
            if (!File.Exists(processPath))
            {
                return null;
            }

            string normalizedChapter = NormalizeRetryChapterKey(chapterName);
            foreach (string line in File.ReadAllLines(processPath, System.Text.Encoding.UTF8))
            {
                string[] cells = line.Split('|');
                if (cells.Length < 5)
                {
                    continue;
                }

                string chapterCell = NormalizeRetryChapterKey(cells[3]);
                string link = cells[4].Trim();
                string linkCell = NormalizeRetryChapterKey(link);
                if (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                    (chapterCell.Contains(normalizedChapter) || linkCell.Contains(normalizedChapter)))
                {
                    return link;
                }
            }

            return null;
        }

        private string NormalizeRetryChapterKey(string value)
        {
            string normalized = (value ?? string.Empty).ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"[^a-z0-9]+", " ").Trim();
            return normalized;
        }
    }
}
