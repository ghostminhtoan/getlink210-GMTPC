using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _downloadCts;
        private volatile bool _isDownloadPaused = false;

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
            string path = txtDownloadPath.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Vui lòng chọn thư mục lưu trước (Please select a download folder first).", "Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể tạo thư mục: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở thư mục: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnStartDownload_Click(object sender, RoutedEventArgs e)
        {
            string downloadRoot = txtDownloadPath.Text.Trim();
            if (string.IsNullOrEmpty(downloadRoot))
            {
                MessageBox.Show("Vui lòng chọn thư mục lưu (Please select a download folder).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var itemsToDownload = dgResults.Items.Cast<GalleryItem>().Where(item => item.IsChecked).ToList();
            if (!itemsToDownload.Any())
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 truyện để tải (Please check at least one gallery to download).", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (itemsToDownload.Any(item => item.Link.Contains("nhentai.net")))
            {
                MessageBox.Show("Không thể download truyện được trên nhentai.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await StartDownloadProcessAsync(itemsToDownload);
        }

        private void BtnPauseDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_downloadCts == null) return;

            if (_isDownloadPaused)
            {
                _isDownloadPaused = false;
                btnPauseDownload.Content = "⏸️";
                Log("Đã tiếp tục tải xuống (Download resumed).");
                foreach (var item in _scrapedItems)
                {
                    if (item.Status == "Paused" || item.IsPaused)
                    {
                        item.IsPaused = false;
                        item.Status = "Downloading";
                    }
                }
            }
            else
            {
                _isDownloadPaused = true;
                btnPauseDownload.Content = "▶️";
                Log("Đã tạm dừng tải xuống (Download paused).");
                foreach (var item in _scrapedItems)
                {
                    if (item.Status == "Downloading" || !item.IsPaused)
                    {
                        item.IsPaused = true;
                        item.Status = "Paused";
                    }
                }
            }
        }

        private void BtnStopDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_downloadCts != null)
            {
                _downloadCts.Cancel();
                _isDownloadPaused = false;
                btnStopDownload.Content = "⏹️...";
                btnStopDownload.IsEnabled = false;
                btnPauseDownload.IsEnabled = false;
                Log("Đang dừng quá trình tải xuống... (Stopping download process...)");

                foreach (var item in _scrapedItems)
                {
                    if (item.Status == "Downloading" || item.Status == "Paused" || item.Status == "Queued")
                    {
                        item.IsStopped = true;
                        item.Status = "Cancelled";
                    }
                }
                
                CleanupActiveTempFolders();
            }
        }

        internal async Task StartDownloadProcessAsync(System.Collections.Generic.List<GalleryItem> itemsToDownload)
        {
            if (_downloadCts != null)
            {
                MessageBox.Show("Một tiến trình tải đang chạy. Vui lòng dừng lại trước khi bắt đầu tải mới (A download process is already running).", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string downloadRoot = txtDownloadPath.Text.Trim();
            if (string.IsNullOrEmpty(downloadRoot))
            {
                MessageBox.Show("Vui lòng chọn thư mục lưu (Please select a download folder).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!Directory.Exists(downloadRoot))
            {
                try
                {
                    Directory.CreateDirectory(downloadRoot);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể tạo thư mục lưu: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Parse chapter selection filter
            ChapterFilter chapterFilter = GetChapterSelectionFilter();

            _downloadCts = new CancellationTokenSource();
            CancellationToken token = _downloadCts.Token;
            _isDownloadPaused = false;

            btnStartDownload.IsEnabled = false;
            btnPauseDownload.Content = "⏸️";
            btnPauseDownload.IsEnabled = true;
            btnStopDownload.IsEnabled = true;
            btnStopDownload.Content = "⏹️";

            btnBrowseFolder.IsEnabled = false;
            // btnOpenFolder remains enabled per user request
            btnScrape.IsEnabled = false;
            btnFetchInfo.IsEnabled = false;
            if (btnNhentaiScrape != null) btnNhentaiScrape.IsEnabled = false;
            if (btnNhentaiFetchInfo != null) btnNhentaiFetchInfo.IsEnabled = false;
            // cmbConnections.IsEnabled = false;
            int totalGalleries = itemsToDownload.Count;
            int completedGalleries = 0;

            int maxParallelBooks = 2;
            Dispatcher.Invoke(() =>
            {
                if (cmbMultiDownload != null && cmbMultiDownload.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem && int.TryParse(selectedItem.Content.ToString(), out int val))
                {
                    maxParallelBooks = val;
                }
            });
            _currentMaxParallelBooks = maxParallelBooks;

            // Initialize GalleryItems for downloading
            foreach (var item in itemsToDownload)
            {
                string domain = "";
                try { domain = new Uri(item.Link).Host; } catch { }

                Dispatcher.Invoke(() =>
                {
                    item.SourceDomain = domain;
                    double num = ExtractNumber(item.LinkCount);
                    item.TotalChapters = num > 0 ? (int)Math.Ceiling(num) : 1;
                    item.CompletedChapters = 0;
                    item.Status = "Queued";
                    item.CurrentProcess = "Waiting...";
                    item.ErrorCount = 0;
                    item.DownloadPath = downloadRoot;
                    item.ProgressPercent = 0;
                    item.IsPaused = false;
                    item.IsStopped = false;
                    item.Errors.Clear();
                });
            }

            Log($"Bắt đầu tải song song {totalGalleries} truyện với tối đa {maxParallelBooks} truyện cùng lúc...");
            lblStatus.Text = $"Downloading 0/{totalGalleries} galleries...";

            try
            {
                var groupedByBook = itemsToDownload
                    .GroupBy(item => GetBookIdentifier(item.Link))
                    .ToList();

                using (var bookSemaphore = new DynamicSemaphore(maxParallelBooks, () => _currentMaxParallelBooks))
                {
                    var tasks = new System.Collections.Generic.List<Task>();
                    object lockObj = new object();

                    foreach (var group in groupedByBook)
                    {
                        var bookGroup = group;
                        
                        // Wait for semaphore sequentially in the main loop to enforce visual grid order
                        await bookSemaphore.WaitAsync(token);

                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                while (_isDownloadPaused)
                                {
                                    token.ThrowIfCancellationRequested();
                                    await Task.Delay(200, token);
                                }
                                token.ThrowIfCancellationRequested();

                                foreach (var item in bookGroup)
                                {
                                    while (_isDownloadPaused || item.IsPaused)
                                    {
                                        token.ThrowIfCancellationRequested();
                                        if (item.IsStopped) throw new OperationCanceledException();
                                        await Task.Delay(200, token);
                                    }
                                    token.ThrowIfCancellationRequested();

                                    // Update queue status
                                    Dispatcher.Invoke(() =>
                                    {
                                        item.Status = "Downloading";
                                        item.CurrentProcess = "Starting...";
                                    });

                                    Log($"[Download] Đang tải: {item.Name} ({item.Link})");

                                    try
                                    {
                                        await DownloadGalleryAsync(item, downloadRoot, token, item, chapterFilter);
                                        lock (lockObj)
                                        {
                                            completedGalleries++;
                                        }

                                        Dispatcher.Invoke(() =>
                                        {
                                            item.Status = item.ErrorCount > 0 ? "Error" : "Completed";
                                            item.CurrentProcess = "Done";
                                        });

                                        Log($"[Download] Hoàn thành truyện: {item.Name}");

                                        // Auto-untick
                                        Dispatcher.Invoke(() => { item.IsChecked = false; });

                                        // Add to history
                                        try
                                        {
                                            int chapCount = item.CompletedChapters;
                                            if (chapCount <= 0) chapCount = 1;
                                            string dlPath = item.DownloadPath ?? downloadRoot;
                                            AddToHistory(item, chapCount, dlPath);
                                        }
                                        catch { }
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        Dispatcher.Invoke(() => { item.Status = "Paused"; item.CurrentProcess = "Cancelled"; });
                                        throw;
                                    }
                                    catch (Exception ex)
                                    {
                                        Log($"[Lỗi] Không thể tải truyện '{item.Name}': {ex.Message}");
                                        Dispatcher.Invoke(() =>
                                        {
                                            item.Status = "Error";
                                            item.CurrentProcess = "Failed";
                                            item.AddError("General", 0, ex.Message);
                                        });
                                    }

                                    lock (lockObj)
                                    {
                                        Dispatcher.Invoke(() =>
                                        {
                                            lblStatus.Text = $"Downloading {completedGalleries}/{totalGalleries} galleries...";
                                        });
                                    }

                                    UpdateQueueErrorLabel();
                                }
                            }
                            finally
                            {
                                bookSemaphore.Release();
                            }
                        }, token));
                    }

                    await Task.WhenAll(tasks);
                }

                lblStatus.Text = "Tải xuống hoàn tất! (Downloads completed)";
                Log("Tải xuống toàn bộ thành công!");
                MessageBox.Show("Đã tải xong toàn bộ truyện được chọn!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                Log("Quá trình tải xuống đã bị dừng bởi người dùng.");
                lblStatus.Text = "Download stopped.";
            }
            catch (Exception ex)
            {
                Log($"Critical download error: {ex.Message}");
                lblStatus.Text = "Download failed.";
            }
            finally
            {
                _downloadCts.Dispose();
                _downloadCts = null;
                _isDownloadPaused = false;

                btnStartDownload.IsEnabled = true;
                btnPauseDownload.Content = "⏸️";
                btnPauseDownload.IsEnabled = false;
                btnStopDownload.IsEnabled = false;
                btnStopDownload.Content = "⏹️";

                btnBrowseFolder.IsEnabled = true;
                btnOpenFolder.IsEnabled = true;
                btnScrape.IsEnabled = true;
                btnFetchInfo.IsEnabled = true;
                if (btnNhentaiScrape != null) btnNhentaiScrape.IsEnabled = true;
                if (btnNhentaiFetchInfo != null) btnNhentaiFetchInfo.IsEnabled = true;
                cmbConnections.IsEnabled = true;

                UpdateQueueErrorLabel();
                CleanupActiveTempFolders();
            }
        }

        private async Task DownloadGalleryAsync(GalleryItem item, string rootFolder, CancellationToken token, GalleryItem queueItem = null, ChapterFilter chapterFilter = null)
        {
            string hostName = "hentaiforce.net";
            try
            {
                hostName = new Uri(item.Link).Host;
            }
            catch {}

            if (hostName.Contains("nhentai.net"))
            {
                Log($"[Bỏ qua] nhentai chỉ dùng để get link, không hỗ trợ tải: {item.Name}");
                return;
            }

            if (hostName.Contains("vi-hentai.pro"))
            {
                await DownloadViHentaiGalleryAsync(item, rootFolder, token, queueItem, chapterFilter);
                return;
            }

            if (IsTruyenqqUrl(item.Link))
            {
                await DownloadTruyenqqGalleryAsync(item, rootFolder, token, queueItem, chapterFilter);
                return;
            }

            string safeTitle = GetSafePathName(item.Name);
            string targetFolder = Path.Combine(rootFolder, hostName, safeTitle);
            string tempFolder = Path.Combine(rootFolder, hostName, $"{safeTitle}-tmp");
            Directory.CreateDirectory(tempFolder);
            RegisterTempFolder(tempFolder);

            // Fetch gallery homepage
            string html = await _httpClient.GetStringAsync(item.Link);

            // 1. Find total pages
            int totalPages = 1;
            var pagesMatch = Regex.Match(html, @"Pages:\s*(\d+)", RegexOptions.IgnoreCase);
            if (pagesMatch.Success)
            {
                totalPages = int.Parse(pagesMatch.Groups[1].Value);
            }
            else
            {
                var thumbMatches = Regex.Matches(html, @"href=""[^""]*?/view/\d+/(\d+)""", RegexOptions.IgnoreCase);
                foreach (Match m in thumbMatches)
                {
                    if (int.TryParse(m.Groups[1].Value, out int pageNum))
                    {
                        if (pageNum > totalPages) totalPages = pageNum;
                    }
                }
            }

            if (queueItem != null)
            {
                Dispatcher.Invoke(() =>
                {
                    queueItem.TotalChapters = totalPages;
                    queueItem.CompletedChapters = 0;
                });
            }

            // 2. Identify path pattern
            string prefix = null;
            string ext = "jpg";
            var patternMatch = Regex.Match(html, @"(?<prefix>https?://[a-zA-Z0-9.-]+/img/\d+-)1t\.(?<ext>[a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
            
            if (patternMatch.Success)
            {
                prefix = patternMatch.Groups["prefix"].Value;
                ext = patternMatch.Groups["ext"].Value;
            }
            else
            {
                var generalMatch = Regex.Match(html, @"(https?://[a-zA-Z0-9.-]+/img/\d+-)\d+t\.(jpg|png|jpeg|webp)", RegexOptions.IgnoreCase);
                if (generalMatch.Success)
                {
                    prefix = generalMatch.Groups[1].Value;
                    ext = generalMatch.Groups[2].Value;
                }
            }

            // Get number of connections
            int maxThreads = 4;
            Dispatcher.Invoke(() =>
            {
                if (cmbConnections.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem && int.TryParse(selectedItem.Content.ToString(), out int val))
                {
                    maxThreads = val;
                }
            });

            bool isFastPath = !string.IsNullOrEmpty(prefix);
            Log($"[Đa luồng] Bắt đầu tải {totalPages} trang với tối đa {maxThreads} kết nối song song...");

            using (var semaphore = new DynamicSemaphore(maxThreads, GetCurrentConnectionLimit))
            {
                var tasks = new System.Collections.Generic.List<Task>();
                int completedPages = 0;
                object lockObj = new object();

                for (int p = 1; p <= totalPages; p++)
                {
                    int pageNum = p;
                    tasks.Add(Task.Run(async () =>
                    {
                        // Check pause/cancel before waiting on semaphore
                        while (_isDownloadPaused || item.IsPaused)
                        {
                            token.ThrowIfCancellationRequested();
                            if (item.IsStopped) throw new OperationCanceledException();
                            await Task.Delay(200, token);
                        }
                        token.ThrowIfCancellationRequested();

                        await semaphore.WaitAsync(token);
                        try
                        {
                            // Check pause/cancel after acquiring semaphore
                            while (_isDownloadPaused || item.IsPaused)
                            {
                                token.ThrowIfCancellationRequested();
                                if (item.IsStopped) throw new OperationCanceledException();
                                await Task.Delay(200, token);
                            }
                            token.ThrowIfCancellationRequested();

                            string fileName = isFastPath ? $"{pageNum:D3}.{ext}" : $"{pageNum:D3}.jpg";
                            string localFilePath = Path.Combine(tempFolder, fileName);
                            string finalFilePath = Path.Combine(targetFolder, fileName);

                            // Skip if file already exists in either temp or final folder
                            if ((File.Exists(localFilePath) && new FileInfo(localFilePath).Length > 1024) ||
                                (File.Exists(finalFilePath) && new FileInfo(finalFilePath).Length > 1024))
                            {
                                lock (lockObj)
                                {
                                    completedPages++;
                                    if (queueItem != null)
                                    {
                                        Dispatcher.Invoke(() =>
                                        {
                                            queueItem.CompletedChapters = completedPages;
                                            queueItem.CurrentProcess = $"{completedPages}/{totalPages} pages";
                                        });
                                    }
                                }
                                return;
                            }

                            if (isFastPath)
                            {
                                string imgUrl = $"{prefix}{pageNum}.{ext}";
                                try
                                {
                                    await DownloadUrlToFileWithRefererAsync(imgUrl, null, localFilePath, token);
                                }
                                catch (Exception ex)
                                {
                                    Log($"[Fast Path] Lỗi trang {pageNum} ({ex.Message}). Thử Slow Path fallback...");
                                    await DownloadPageSlowPathAsync(item, pageNum, localFilePath, token);
                                }
                            }
                            else
                            {
                                await DownloadPageSlowPathAsync(item, pageNum, localFilePath, token);
                            }

                            lock (lockObj)
                            {
                                completedPages++;
                                if (queueItem != null)
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        queueItem.CompletedChapters = completedPages;
                                        queueItem.CurrentProcess = $"{completedPages}/{totalPages} pages";
                                    });
                                }
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, token));
                }

                await Task.WhenAll(tasks);

                try
                {
                    if (Directory.Exists(tempFolder))
                    {
                        if (Directory.Exists(targetFolder))
                        {
                            MergeDirectoryContents(tempFolder, targetFolder);
                        }
                        else
                        {
                            Directory.Move(tempFolder, targetFolder);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"[Lỗi] Không thể di chuyển thư mục tạm HentaiForce: {ex.Message}");
                }
                finally
                {
                    UnregisterTempFolder(tempFolder);
                }

                // Check for missing files
                ValidateDownloadedFiles(targetFolder, totalPages, queueItem, "Pages");
            }
        }

        private async Task DownloadPageSlowPathAsync(GalleryItem item, int pageNum, string targetPath, CancellationToken token)
        {
            // Respect pause check
            while (_isDownloadPaused || item.IsPaused)
            {
                token.ThrowIfCancellationRequested();
                if (item.IsStopped) throw new OperationCanceledException();
                await Task.Delay(200, token);
            }
            token.ThrowIfCancellationRequested();

            string pageUrl = $"{item.Link}/{pageNum}";
            string html = await _httpClient.GetStringAsync(pageUrl);

            // Match image src/data-src in Hentaiforce reader page
            var imgMatch = Regex.Match(html, @"(?:src|data-src)\s*=\s*""(?<imgUrl>https?://[a-zA-Z0-9.-]+/img/\d+-\d+\.(?:jpg|png|jpeg|webp))""", RegexOptions.IgnoreCase);
            
            if (imgMatch.Success)
            {
                string imgUrl = imgMatch.Groups["imgUrl"].Value;
                
                // Adjust file extension based on actual source URL
                string actualExt = Path.GetExtension(imgUrl);
                string finalPath = targetPath;
                if (!string.IsNullOrEmpty(actualExt) && !targetPath.EndsWith(actualExt, StringComparison.OrdinalIgnoreCase))
                {
                    finalPath = Path.ChangeExtension(targetPath, actualExt);
                }

                // Respect pause check
                while (_isDownloadPaused || item.IsPaused)
                {
                    token.ThrowIfCancellationRequested();
                    if (item.IsStopped) throw new OperationCanceledException();
                    await Task.Delay(200, token);
                }
                token.ThrowIfCancellationRequested();

                await DownloadUrlToFileWithRefererAsync(imgUrl, null, finalPath, token);
            }
            else
            {
throw new Exception($"Không thể trích xuất địa chỉ ảnh từ trang đọc {pageNum}");
            }
        }

        private async Task<byte[]> GetByteArrayWithRefererAsync(string url, string referer)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Referrer = new Uri(referer);
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsByteArrayAsync();
                }
            }
        }

        private async Task DownloadNhentaiGalleryAsync(GalleryItem item, string rootFolder, CancellationToken token)
        {
            string safeTitle = GetSafePathName(item.Name);
            string targetFolder = Path.Combine(rootFolder, "nhentai.net", safeTitle);
            string tempFolder = Path.Combine(rootFolder, "nhentai.net", ".tmp", $".tmp_{safeTitle}_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempFolder);
            RegisterTempFolder(tempFolder);

            // Check if the link is a direct CDN link
            var cdnMatch = Regex.Match(item.Link, @"(?:https?:)?//(?<subdomain>[it]\d*)\.nhentai\.net/galleries/(?<mediaId>\d+)/(?<pageNum>\d+)(?<isThumb>t)?\.(?<ext>jpg|png|gif|webp|jpeg)", RegexOptions.IgnoreCase);
            bool isCdnLink = cdnMatch.Success;

            int totalPages = 1;
            string prefix = null;
            string ext = "jpg";

            if (isCdnLink)
            {
                string mediaId = cdnMatch.Groups["mediaId"].Value;
                string subdomain = cdnMatch.Groups["subdomain"].Value;
                ext = cdnMatch.Groups["ext"].Value;

                // Map subdomain t* to i*
                string cdnSubdomain = "i";
                if (subdomain.Length > 1)
                {
                    cdnSubdomain = "i" + subdomain.Substring(1);
                }
                else if (subdomain.StartsWith("t", StringComparison.OrdinalIgnoreCase))
                {
                    cdnSubdomain = "i";
                }
                else
                {
                    cdnSubdomain = subdomain;
                }

                prefix = $"https://{cdnSubdomain}.nhentai.net/galleries/{mediaId}/";
                totalPages = await ProbeTotalPagesAsync(mediaId, ext, token);
                if (totalPages == 0)
                {
                    throw new Exception($"Không thể xác định số lượng trang hoặc trang không khả dụng cho CDN Gallery: {mediaId}");
                }
            }
            else
            {
                // Fetch gallery homepage to extract thumbnail pattern -> derive full-size CDN URL
                // (Like Bulk Image Downloader "thumbnail mode": t*.nhentai.net/.../Nt.jpg -> i*.nhentai.net/.../N.jpg)
                string html = null;
                try
                {
                    html = await _httpClient.GetStringAsync(item.Link);
                }
                catch (HttpRequestException)
                {
                    // Likely Cloudflare 403/503 — ask user to solve captcha once then retry
                    bool ok = await SolveNhentaiCaptchaIfNeededAsync(item.Link);
                    if (!ok)
                        throw new Exception("Không thể vượt qua Cloudflare. Tải xuống bị hủy.");
                    html = await _httpClient.GetStringAsync(item.Link);
                }

                // 1. Find total pages
                var pagesMatch = Regex.Match(html, @"(\d+)\s+pages", RegexOptions.IgnoreCase);
                if (pagesMatch.Success)
                {
                    totalPages = int.Parse(pagesMatch.Groups[1].Value);
                }
                else
                {
                    var pagesValueMatch = Regex.Match(html, @"Pages:.*?class=""value""[^>]*>(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (pagesValueMatch.Success)
                    {
                        totalPages = int.Parse(pagesValueMatch.Groups[1].Value);
                    }
                }

                // 2. Extract thumbnail URL (t*.nhentai.net/galleries/{id}/1t.{ext}) and derive full-size prefix (i*)
                // This mirrors the "thumbnail mode" of Bulk Image Downloader on nhentai
                var patternMatch = Regex.Match(html, @"(?<prefix>(?:https?:)?//(?<subdomain>t\d*)\.nhentai\.net/galleries/(?<galleryId>\d+)/)1t\.(?<ext>[a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
                
                if (patternMatch.Success)
                {
                    string subdomain = patternMatch.Groups["subdomain"].Value;
                    string galleryId = patternMatch.Groups["galleryId"].Value;
                    ext = patternMatch.Groups["ext"].Value;

                    // Convert thumbnail subdomain (t*) to image subdomain (i*)
                    string cdnSubdomain = subdomain.StartsWith("t", StringComparison.OrdinalIgnoreCase)
                        ? "i" + subdomain.Substring(1)  // t3 -> i3, t -> i
                        : "i";

                    // Full-size images: https://i*.nhentai.net/galleries/{id}/{N}.{ext}
                    prefix = $"https://{cdnSubdomain}.nhentai.net/galleries/{galleryId}/";
                    Log($"[nhentai] Phát hiện CDN prefix: {prefix}, ext: {ext}, total pages: {totalPages}");
                }
                else
                {
                    Log($"[nhentai] Cảnh báo: Không tìm thấy thumbnail pattern trong trang gallery. Sẽ thử slow path.");
                }
            }

            // Get number of connections
            int maxThreads = 4;
            Dispatcher.Invoke(() =>
            {
                if (cmbConnections.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem && int.TryParse(selectedItem.Content.ToString(), out int val))
                {
                    maxThreads = val;
                }
            });

            // Fast path: use CDN prefix directly (works for both direct CDN links and normal galleries where prefix was extracted)
            // CDN images on i*.nhentai.net are publicly accessible with referrer header - no reader page visit needed
            bool isFastPath = !string.IsNullOrEmpty(prefix);
            string nhentaiModeLabel = isFastPath ? "Fast Path (CDN Direct)" : "Slow Path (Reader Page)";
            Log($"[Đa luồng nhentai] Bắt đầu tải {totalPages} trang, mode: {nhentaiModeLabel}, tối đa {maxThreads} kết nối song song...");

            using (var semaphore = new DynamicSemaphore(maxThreads, GetCurrentConnectionLimit))
            {
                var tasks = new System.Collections.Generic.List<Task>();
                int completedPages = 0;
                object lockObj = new object();

                for (int p = 1; p <= totalPages; p++)
                {
                    int pageNum = p;
                    tasks.Add(Task.Run(async () =>
                    {
                        // Check pause/cancel before waiting on semaphore
                        while (_isDownloadPaused)
                        {
                            token.ThrowIfCancellationRequested();
                            await Task.Delay(200, token);
                        }
                        token.ThrowIfCancellationRequested();

                        await semaphore.WaitAsync(token);
                        try
                        {
                            // Check pause/cancel after acquiring semaphore
                            while (_isDownloadPaused)
                            {
                                token.ThrowIfCancellationRequested();
                                await Task.Delay(200, token);
                            }
                            token.ThrowIfCancellationRequested();

                            string fileName = isFastPath ? $"{pageNum:D3}.{ext}" : $"{pageNum:D3}.jpg";
                            string localFilePath = Path.Combine(tempFolder, fileName);
                            string finalFilePath = Path.Combine(targetFolder, fileName);

                            // Skip if file already exists in either temp or final folder (with any common image extension)
                            bool alreadyExists = false;
                            string existingFile = null;
                            if (isFastPath)
                            {
                                if (File.Exists(localFilePath) && new FileInfo(localFilePath).Length > 1024)
                                {
                                    alreadyExists = true;
                                    existingFile = localFilePath;
                                }
                                else if (File.Exists(finalFilePath) && new FileInfo(finalFilePath).Length > 1024)
                                {
                                    alreadyExists = true;
                                    existingFile = finalFilePath;
                                }
                            }
                            else
                            {
                                string[] checkExts = { "jpg", "png", "webp", "gif", "jpeg" };
                                foreach (var checkExt in checkExts)
                                {
                                    string testPathTemp = Path.ChangeExtension(localFilePath, checkExt);
                                    string testPathFinal = Path.ChangeExtension(finalFilePath, checkExt);
                                    if (File.Exists(testPathTemp) && new FileInfo(testPathTemp).Length > 1024)
                                    {
                                        alreadyExists = true;
                                        existingFile = testPathTemp;
                                        break;
                                    }
                                    if (File.Exists(testPathFinal) && new FileInfo(testPathFinal).Length > 1024)
                                    {
                                        alreadyExists = true;
                                        existingFile = testPathFinal;
                                        break;
                                    }
                                }
                            }

                            if (alreadyExists)
                            {
                                lock (lockObj)
                                {
                                    completedPages++;
                                    string modeText = isFastPath ? "Fast Path" : "Slow Path";
                                    Dispatcher.Invoke(() =>
                                    {
                                        lblStatus.Text = $"[{completedPages}/{totalPages}] Tải {safeTitle} ({modeText})";
                                    });
                                }
                                return;
                            }

                            if (isFastPath)
                            {
                                string imgUrl = $"{prefix}{pageNum}.{ext}";
                                try
                                {
                                    await DownloadUrlToFileWithRefererAsync(imgUrl, "https://nhentai.net/", localFilePath, token);
                                }
                                catch (Exception ex)
                                {
                                    // Try other common extensions as fallback
                                    bool success = false;
                                    string[] extensions = { "jpg", "png", "webp", "gif", "jpeg" };
                                    foreach (var otherExt in extensions)
                                    {
                                        if (string.Equals(otherExt, ext, StringComparison.OrdinalIgnoreCase)) continue;
                                        try
                                        {
                                            string altUrl = $"{prefix}{pageNum}.{otherExt}";
                                            string finalPath = Path.Combine(tempFolder, $"{pageNum:D3}.{otherExt}");
                                            await DownloadUrlToFileWithRefererAsync(altUrl, "https://nhentai.net/", finalPath, token);
                                            success = true;
                                            break;
                                        }
                                        catch {}
                                    }
                                    if (!success)
                                    {
                                        Log($"[nhentai Fast Path] Lỗi trang {pageNum} ({ex.Message}). Thử Slow Path fallback...");
                                        await DownloadNhentaiPageSlowPathAsync(item.Link, pageNum, localFilePath, token);
                                    }
                                }
                            }
                            else
                            {
                                await DownloadNhentaiPageSlowPathAsync(item.Link, pageNum, localFilePath, token);
                            }

                            lock (lockObj)
                            {
                                completedPages++;
                                if (completedPages % 5 == 0 || completedPages == totalPages)
                                {
                                    string modeText = isFastPath ? "Fast Path" : "Slow Path";
                                    Dispatcher.Invoke(() =>
                                    {
                                        lblStatus.Text = $"[{completedPages}/{totalPages}] Tải {safeTitle} ({modeText})";
                                    });
                                }
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, token));
                }

                await Task.WhenAll(tasks);

                try
                {
                    if (Directory.Exists(tempFolder))
                    {
                        if (Directory.Exists(targetFolder))
                        {
                            MergeDirectoryContents(tempFolder, targetFolder);
                        }
                        else
                        {
                            Directory.Move(tempFolder, targetFolder);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"[Lỗi] Không thể di chuyển thư mục tạm nhentai: {ex.Message}");
                }
                finally
                {
                    UnregisterTempFolder(tempFolder);
                }
            }
        }

        private async Task<int> ProbeTotalPagesAsync(string mediaId, string defaultExt, CancellationToken token)
        {
            Log($"[nhentai] Đang dò tìm tổng số trang cho media ID {mediaId}...");
            
            // Check if page 1 exists
            string p1Ext = await FindValidExtensionAsync(mediaId, 1, defaultExt);
            if (p1Ext == null)
            {
                Log($"[nhentai] Lỗi: Không thể tìm thấy trang 1 cho media ID {mediaId}");
                return 0;
            }

            int low = 1;
            int high = 1000;
            int detectedPages = 1;

            while (low <= high)
            {
                token.ThrowIfCancellationRequested();
                int mid = (low + high) / 2;
                string ext = await FindValidExtensionAsync(mediaId, mid, defaultExt);
                
                if (ext != null)
                {
                    detectedPages = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            Log($"[nhentai] Đã dò tìm xong. Tổng số trang phát hiện: {detectedPages}");
            return detectedPages;
        }

        private async Task<string> FindValidExtensionAsync(string mediaId, int pageNum, string defaultExt)
        {
            string[] extensions = { "jpg", "png", "webp", "gif", "jpeg" };
            
            // Try default extension first
            string url = $"https://i3.nhentai.net/galleries/{mediaId}/{pageNum}.{defaultExt}";
            if (await CheckPageExistsAsync(url)) return defaultExt;

            foreach (var ext in extensions)
            {
                if (string.Equals(ext, defaultExt, StringComparison.OrdinalIgnoreCase)) continue;
                url = $"https://i3.nhentai.net/galleries/{mediaId}/{pageNum}.{ext}";
                if (await CheckPageExistsAsync(url)) return ext;
            }

            return null;
        }

        private async Task<bool> CheckPageExistsAsync(string url)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Head, url))
                {
                    request.Headers.Referrer = new Uri("https://nhentai.net/");
                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (response.IsSuccessStatusCode) return true;
                    }
                }
                
                // Fallback to GET if HEAD method is not allowed or fails
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Referrer = new Uri("https://nhentai.net/");
                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        return response.IsSuccessStatusCode;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task DownloadNhentaiPageSlowPathAsync(string galleryUrl, int pageNum, string targetPath, CancellationToken token)
        {
            // Respect pause check
            while (_isDownloadPaused)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(200, token);
            }
            token.ThrowIfCancellationRequested();

            // Reader URL format is galleryUrl/pageNum
            string cleanGalleryUrl = galleryUrl.TrimEnd('/');
            string pageUrl = $"{cleanGalleryUrl}/{pageNum}";
            
            string html = await _httpClient.GetStringAsync(pageUrl);

            // Match image URL on the reader page (quote-independent)
            string imgUrl = null;
            var imgMatch = Regex.Match(html, @"(?<imgUrl>(?:https?:)?//(?<subdomain>i\d*)\.nhentai\.net/galleries/(?<galleryId>\d+)/" + pageNum + @"\.(?<ext>jpg|png|gif|webp|jpeg))", RegexOptions.IgnoreCase);
            if (imgMatch.Success)
            {
                imgUrl = imgMatch.Groups["imgUrl"].Value;
            }
            else
            {
                // Fallback: search inside section/div with class/id image-container
                var fallbackMatch = Regex.Match(html, @"<(?:section|div)\s+[^>]*?(?:id|class)=[""']image-container[""'][^>]*>.*?<img\s+[^>]*?(?:src|data-src)=[""'](?<imgUrl>[^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (fallbackMatch.Success)
                {
                    imgUrl = fallbackMatch.Groups["imgUrl"].Value;
                }
                else
                {
                    // General fallback for any image in galleries directory
                    var generalMatch = Regex.Match(html, @"(?:src|data-src)=[""'](?<imgUrl>(?:https?:)?//i\d*\.nhentai\.net/galleries/[^""']+)[""']", RegexOptions.IgnoreCase);
                    if (generalMatch.Success)
                    {
                        imgUrl = generalMatch.Groups["imgUrl"].Value;
                    }
                }
            }

            if (!string.IsNullOrEmpty(imgUrl))
            {
                if (imgUrl.StartsWith("//"))
                {
                    imgUrl = "https:" + imgUrl;
                }

                // Adjust file extension based on actual source URL
                string actualExt = Path.GetExtension(imgUrl);
                string finalPath = targetPath;
                if (!string.IsNullOrEmpty(actualExt) && !targetPath.EndsWith(actualExt, StringComparison.OrdinalIgnoreCase))
                {
                    finalPath = Path.ChangeExtension(targetPath, actualExt);
                }

                // Respect pause check
                while (_isDownloadPaused)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(200, token);
                }
                token.ThrowIfCancellationRequested();

                await DownloadUrlToFileWithRefererAsync(imgUrl, pageUrl, finalPath, token);
            }
            else
            {
                throw new Exception($"Không thể trích xuất địa chỉ ảnh từ trang đọc nhentai {pageNum}");
            }
        }

        internal async Task<bool> CheckIfNhentaiBlockedAsync(string testUrl)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, testUrl))
                {
                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.ServiceUnavailable)
                        {
                            return true; // Cloudflare blocked (403/503)
                        }

                        // Also read a snippet of the page to check for challenge
                        using (var content = response.Content)
                        {
                            string html = await content.ReadAsStringAsync();
                            if (html.Contains("cf-challenge") || 
                                html.Contains("cf-turnstile") || 
                                html.Contains("Turnstile") || 
                                html.Contains("Just a moment..."))
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("403") || ex.Message.Contains("503"))
                {
                    return true;
                }
                return false;
            }
        }

        internal async Task<bool> SolveNhentaiCaptchaIfNeededAsync(string testUrl)
        {
            bool isBlocked = await CheckIfNhentaiBlockedAsync(testUrl);
            if (!isBlocked)
            {
                return true; // Not blocked, all good!
            }

            if (_isCaptchaWindowActive)
            {
                while (_isCaptchaWindowActive)
                {
                    await Task.Delay(500);
                }
                isBlocked = await CheckIfNhentaiBlockedAsync(testUrl);
                if (!isBlocked)
                {
                    return true;
                }
            }

            await _captchaSemaphore.WaitAsync();
            try
            {
                // Re-check after lock
                isBlocked = await CheckIfNhentaiBlockedAsync(testUrl);
                if (!isBlocked)
                {
                    return true;
                }

                _isCaptchaWindowActive = true;
                _isDownloadPaused = true;
                Log("[nhentai.net] Phát hiện thử thách Cloudflare / Captcha. Tạm dừng tải và đang mở trình duyệt giải tự động...");

                bool solved = false;
                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        var captchaWin = new CaptchaWindow(testUrl)
                        {
                            Owner = this
                        };

                        if (captchaWin.ShowDialog() == true)
                        {
                            // Copy cookies to our global CookieContainer
                            var uri = new Uri("https://nhentai.net");
                            var cookies = captchaWin.ResolvedCookies.GetCookies(uri);
                            foreach (Cookie cookie in cookies)
                            {
                                _cookieContainer.Add(uri, cookie);
                            }

                            // Copy User-Agent to HttpClient DefaultRequestHeaders
                            if (!string.IsNullOrEmpty(captchaWin.UserAgent))
                            {
                                _httpClient.DefaultRequestHeaders.UserAgent.Clear();
                                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(captchaWin.UserAgent);
                            }
                            solved = true;
                        }
                    });
                }
                finally
                {
                    _isCaptchaWindowActive = false;
                }

                if (solved)
                {
                    Log("[nhentai.net] Giải captcha thành công. Tiếp tục tải...");
                    _isDownloadPaused = false;
                    return true;
                }
                return false;
            }
            finally
            {
                _captchaSemaphore.Release();
            }
        }

        internal async Task<bool> CheckIfViHentaiBlockedAsync(string testUrl)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, testUrl))
                {
                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (response.StatusCode == HttpStatusCode.Forbidden || 
                            response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                            (int)response.StatusCode == 429)
                        {
                            return true; // Cloudflare blocked or throttled (403/503/429)
                        }

                        using (var content = response.Content)
                        {
                            string html = await content.ReadAsStringAsync();
                            if (html.Contains("cf-challenge") || 
                                html.Contains("cf-turnstile") || 
                                html.Contains("Turnstile") || 
                                html.Contains("Just a moment...") ||
                                html.Contains("xác minh bạn không phải là bot"))
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("403") || ex.Message.Contains("503") || ex.Message.Contains("429"))
                {
                    return true;
                }
                return false;
            }
        }

        internal async Task<bool> SolveViHentaiCaptchaIfNeededAsync(string testUrl)
        {
            bool isBlocked = await CheckIfViHentaiBlockedAsync(testUrl);
            if (!isBlocked)
            {
                return true; // Not blocked
            }

            if (_isCaptchaWindowActive)
            {
                while (_isCaptchaWindowActive)
                {
                    await Task.Delay(500);
                }
                isBlocked = await CheckIfViHentaiBlockedAsync(testUrl);
                if (!isBlocked)
                {
                    return true;
                }
            }

            await _captchaSemaphore.WaitAsync();
            try
            {
                // Re-check after lock
                isBlocked = await CheckIfViHentaiBlockedAsync(testUrl);
                if (!isBlocked)
                {
                    return true;
                }

                _isCaptchaWindowActive = true;
                _isDownloadPaused = true;
                Log("[vi-hentai.pro] Phát hiện thử thách Cloudflare / Captcha. Tạm dừng tải và đang mở trình duyệt giải tự động...");

                bool solved = false;
                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        var captchaWin = new CaptchaWindow(testUrl)
                        {
                            Owner = this
                        };

                        if (captchaWin.ShowDialog() == true)
                        {
                            var uri = new Uri("https://vi-hentai.pro");
                            var cookies = captchaWin.ResolvedCookies.GetCookies(uri);
                            foreach (Cookie cookie in cookies)
                            {
                                _cookieContainer.Add(uri, cookie);
                            }

                            if (!string.IsNullOrEmpty(captchaWin.UserAgent))
                            {
                                _httpClient.DefaultRequestHeaders.UserAgent.Clear();
                                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(captchaWin.UserAgent);
                            }
                            solved = true;
                        }
                    });
                }
                finally
                {
                    _isCaptchaWindowActive = false;
                }

                if (solved)
                {
                    Log("[vi-hentai.pro] Giải captcha thành công. Tiếp tục tải...");
                    _isDownloadPaused = false;
                    return true;
                }
                return false;
            }
            finally
            {
                _captchaSemaphore.Release();
            }
        }

        private string GetSafePathName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            
            var invalid = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct();
            string safeName = name;
            foreach (var c in invalid)
            {
                safeName = safeName.Replace(c, ' ');
            }

            // Remove multiple consecutive spaces
            safeName = Regex.Replace(safeName, @"\s+", " ");
            return safeName.Trim();
        }

        private int GetCurrentConnectionLimit()
        {
            int val = 4;
            Dispatcher.Invoke(() =>
            {
                if (cmbConnections != null && cmbConnections.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem && int.TryParse(selectedItem.Content.ToString(), out int parsed))
                {
                    val = parsed;
                }
            });
            return val;
        }

        private string GetBookIdentifier(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            try
            {
                var uri = new Uri(url);
                string host = uri.Host.ToLower();
                string path = uri.AbsolutePath;

                if (host.Contains("vi-hentai.pro"))
                {
                    var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 2 && segments[0].Equals("truyen", StringComparison.OrdinalIgnoreCase))
                    {
                        return "vi-hentai.pro|" + segments[1].ToLower();
                    }
                }

                if (host.Contains("truyenqq"))
                {
                    var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 2 && segments[0].Equals("truyen-tranh", StringComparison.OrdinalIgnoreCase))
                    {
                        string rawSlug = segments[1].ToLower();
                        int idx = rawSlug.IndexOf("-chap", StringComparison.OrdinalIgnoreCase);
                        if (idx != -1)
                        {
                            rawSlug = rawSlug.Substring(0, idx);
                        }
                        return "truyenqq|" + rawSlug;
                    }
                }
            }
            catch {}
            return url;
        }

        private async Task DownloadUrlToFileWithRefererAsync(string url, string referer, string filePath, CancellationToken token, bool isViHentai = false, bool isTruyenqq = false)
        {
            if (File.Exists(filePath) && new FileInfo(filePath).Length > 1024)
            {
                return; // skip duplicate
            }

            int delayMs = isViHentai ? 800 : (isTruyenqq ? 600 : 500);
            int maxAttempts = 4;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        if (!string.IsNullOrEmpty(referer))
                        {
                            request.Headers.Referrer = new Uri(referer);
                        }

                        using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
                        {
                            if (isViHentai && (int)response.StatusCode == 429 && attempt < maxAttempts)
                            {
                                int retryDelay = GetRetryDelayMilliseconds(response, attempt, delayMs);
                                Log($"[vi-hentai.pro] 429 khi tải ảnh. Chờ {retryDelay}ms rồi thử lại ({attempt}/{maxAttempts}): {url}");
                                await Task.Delay(retryDelay, token);
                                delayMs = Math.Min(delayMs * 2, 8000);
                                continue;
                            }

                            response.EnsureSuccessStatusCode();

                            using (var contentStream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                            {
                                await contentStream.CopyToAsync(fileStream, 81920, token);
                            }
                            return; // Success!
                        }
                    }
                }
                catch (HttpRequestException ex) when (attempt < maxAttempts)
                {
                    string label = isViHentai ? "[vi-hentai.pro]" : (isTruyenqq ? "[truyenqq]" : "[network]");
                    Log($"{label} Thử tải lại ảnh do lỗi mạng: {ex.Message}. Chờ {delayMs}ms ({attempt}/{maxAttempts}).");
                    await Task.Delay(delayMs, token);
                    delayMs = Math.Min(delayMs * 2, 8000);
                }
                catch (TaskCanceledException) when (!token.IsCancellationRequested && attempt < maxAttempts)
                {
                    string label = isViHentai ? "[vi-hentai.pro]" : (isTruyenqq ? "[truyenqq]" : "[network]");
                    Log($"{label} Thử tải lại ảnh do timeout. Chờ {delayMs}ms ({attempt}/{maxAttempts}).");
                    await Task.Delay(delayMs, token);
                    delayMs = Math.Min(delayMs * 2, 8000);
                }
            }

            throw new Exception($"Không thể tải ảnh sau {maxAttempts} lần thử: {url}");
        }

        private void ValidateDownloadedFiles(string folderPath, int expectedCount, GalleryItem queueItem, string chapterName = "General")
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return;

            try
            {
                var files = Directory.GetFiles(folderPath);
                var existingPageNumbers = new HashSet<int>();
                foreach (var file in files)
                {
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(file);
                    if (int.TryParse(nameWithoutExt, out int pageNum))
                    {
                        existingPageNumbers.Add(pageNum);
                    }
                }

                var missingPages = new List<int>();
                for (int i = 1; i <= expectedCount; i++)
                {
                    if (!existingPageNumbers.Contains(i))
                    {
                        missingPages.Add(i);
                    }
                }

                if (missingPages.Count > 0)
                {
                    string missingMsg = $"Thiếu các trang: {string.Join(", ", missingPages.Select(p => p.ToString("D3")))}";
                    Log($"[Cảnh báo] Thư mục '{Path.GetFileName(folderPath)}' bị thiếu {missingPages.Count} trang!");
                    if (queueItem != null)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            foreach (var p in missingPages)
                            {
                                queueItem.AddError(chapterName, p, "Trang bị thiếu (Missing page)", null);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[Lỗi] Không thể kiểm tra tính toàn vẹn của thư mục '{folderPath}': {ex.Message}");
            }
        }

        private void CmbMultiDownload_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbMultiDownload == null || cmbMultiDownload.SelectedItem == null) return;
            var selectedItem = cmbMultiDownload.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (selectedItem == null) return;
            if (!int.TryParse(selectedItem.Content.ToString(), out int newVal)) return;

            _currentMaxParallelBooks = newVal;
            Log($"[Multi Download] Số luồng tải song song được chỉnh thành {newVal}.");
        }
    }


    public class VistaFolderBrowser
    {
        public string SelectedPath { get; set; }
        public string Title { get; set; }

        public bool ShowDialog(IntPtr owner)
        {
            var dialog = (IFileOpenDialog)new FileOpenDialog();
            try
            {
                // FOS_PICKFOLDERS (0x20) | FOS_FORCEFILESYSTEM (0x40)
                dialog.SetOptions(0x00000020 | 0x00000040);
                
                if (!string.IsNullOrEmpty(Title))
                {
                    dialog.SetTitle(Title);
                }

                int hr = dialog.Show(owner);
                if (hr == 0) // S_OK
                {
                    IShellItem item;
                    dialog.GetResult(out item);
                    string path;
                    item.GetDisplayName(SIGDN.FILESYSPATH, out path);
                    SelectedPath = path;
                    return true;
                }
                return false;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(dialog);
            }
        }

        [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialog { }

        [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, uint fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close([MarshalAs(UnmanagedType.Error)] int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void FilterShowEvent(IntPtr pfde);
            void GetResults(out IntPtr ppenum);
            void GetSelectedItems(out IntPtr ppssa);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string name);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        private enum SIGDN : uint
        {
            FILESYSPATH = 0x80058000
        }
    }

    public class DynamicSemaphore : IDisposable
    {
        private readonly SemaphoreSlim _sem;
        private int _currentLimit;
        private readonly Func<int> _limitProvider;

        public DynamicSemaphore(int initialLimit, Func<int> limitProvider)
        {
            _sem = new SemaphoreSlim(initialLimit);
            _currentLimit = initialLimit;
            _limitProvider = limitProvider;
        }

        public async Task WaitAsync(CancellationToken token)
        {
            AdjustLimit();
            await _sem.WaitAsync(token);
        }

        public void Release()
        {
            _sem.Release();
            AdjustLimit();
        }

        private void AdjustLimit()
        {
            int target = _limitProvider();
            if (target == _currentLimit) return;

            lock (this)
            {
                if (target > _currentLimit)
                {
                    int diff = target - _currentLimit;
                    _sem.Release(diff);
                    _currentLimit = target;
                }
                else if (target < _currentLimit)
                {
                    int diff = _currentLimit - target;
                    for (int i = 0; i < diff; i++)
                    {
                        Task.Run(async () => {
                            try { await _sem.WaitAsync(); } catch {}
                        });
                    }
                    _currentLimit = target;
                }
            }
        }

        public void Dispose()
        {
            _sem.Dispose();
        }
    }
}
