using System;
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

            var itemsToDownload = _scrapedItems.Where(item => item.IsChecked).ToList();
            if (!itemsToDownload.Any())
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 truyện để tải (Please check at least one gallery to download).", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
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
                btnPauseDownload.Content = "PAUSE";
                Log("Đã tiếp tục tải xuống (Download resumed).");
            }
            else
            {
                _isDownloadPaused = true;
                btnPauseDownload.Content = "RESUME";
                Log("Đã tạm dừng tải xuống (Download paused).");
            }
        }

        private void BtnStopDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_downloadCts != null)
            {
                _downloadCts.Cancel();
                _isDownloadPaused = false;
                btnStopDownload.Content = "STOPPING...";
                btnStopDownload.IsEnabled = false;
                btnPauseDownload.IsEnabled = false;
                Log("Đang dừng quá trình tải xuống... (Stopping download process...)");
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

            _downloadCts = new CancellationTokenSource();
            CancellationToken token = _downloadCts.Token;
            _isDownloadPaused = false;

            btnStartDownload.IsEnabled = false;
            btnPauseDownload.Content = "PAUSE";
            btnPauseDownload.IsEnabled = true;
            btnStopDownload.IsEnabled = true;
            btnStopDownload.Content = "STOP";

            btnBrowseFolder.IsEnabled = false;
            // btnOpenFolder remains enabled per user request
            btnScrape.IsEnabled = false;
            btnFetchInfo.IsEnabled = false;
            if (btnNhentaiScrape != null) btnNhentaiScrape.IsEnabled = false;
            if (btnNhentaiFetchInfo != null) btnNhentaiFetchInfo.IsEnabled = false;
            cmbConnections.IsEnabled = false;

            progressBar.Value = 0;
            progressBar.IsIndeterminate = false;

            int totalGalleries = itemsToDownload.Count;
            int completedGalleries = 0;

            Log($"Bắt đầu tải {totalGalleries} truyện...");
            lblStatus.Text = $"Downloading 0/{totalGalleries} galleries...";

            try
            {
                for (int i = 0; i < totalGalleries; i++)
                {
                    token.ThrowIfCancellationRequested();

                    // Respect pause check
                    while (_isDownloadPaused)
                    {
                        token.ThrowIfCancellationRequested();
                        await Task.Delay(200, token);
                    }

                    var item = itemsToDownload[i];

                    Log($"[Download {i + 1}/{totalGalleries}] Đang tải: {item.Name} ({item.Link})");
                    
                    try
                {
                    await DownloadGalleryAsync(item, downloadRoot, token);
                    completedGalleries++;
                    Log($"[Download {i + 1}/{totalGalleries}] Hoàn thành truyện: {item.Name}");
                    // Auto-untick checkbox after successful download
                    item.IsChecked = false;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log($"[Lỗi] Không thể tải truyện '{item.Name}': {ex.Message}");
                }

                    double overallProgress = ((double)completedGalleries / totalGalleries) * 100;
                    progressBar.Value = overallProgress;
                    lblStatus.Text = $"Downloading {completedGalleries}/{totalGalleries} galleries...";
                }

                progressBar.Value = 100;
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
                btnPauseDownload.Content = "PAUSE";
                btnPauseDownload.IsEnabled = false;
                btnStopDownload.IsEnabled = false;
                btnStopDownload.Content = "STOP";

                btnBrowseFolder.IsEnabled = true;
                btnOpenFolder.IsEnabled = true;
                btnScrape.IsEnabled = true;
                btnFetchInfo.IsEnabled = true;
                if (btnNhentaiScrape != null) btnNhentaiScrape.IsEnabled = true;
                if (btnNhentaiFetchInfo != null) btnNhentaiFetchInfo.IsEnabled = true;
                cmbConnections.IsEnabled = true;
            }
        }

        private async Task DownloadGalleryAsync(GalleryItem item, string rootFolder, CancellationToken token)
        {
            string hostName = "hentaiforce.net";
            try
            {
                hostName = new Uri(item.Link).Host;
            }
            catch {}

            if (hostName.Contains("nhentai.net"))
            {
                await DownloadNhentaiGalleryAsync(item, rootFolder, token);
                return;
            }

            if (hostName.Contains("vi-hentai.pro"))
            {
                await DownloadViHentaiGalleryAsync(item, rootFolder, token);
                return;
            }

            string safeTitle = GetSafePathName(item.Name);
            string targetFolder = Path.Combine(rootFolder, hostName, safeTitle);
            Directory.CreateDirectory(targetFolder);

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

            using (var semaphore = new SemaphoreSlim(maxThreads))
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
                            string localFilePath = Path.Combine(targetFolder, fileName);

                            // Skip if file already exists
                            if (File.Exists(localFilePath) && new FileInfo(localFilePath).Length > 1024)
                            {
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
                                return;
                            }

                            if (isFastPath)
                            {
                                string imgUrl = $"{prefix}{pageNum}.{ext}";
                                try
                                {
                                    byte[] bytes = await _httpClient.GetByteArrayAsync(imgUrl);
                                    File.WriteAllBytes(localFilePath, bytes);
                                }
                                catch (Exception ex)
                                {
                                    Log($"[Fast Path] Lỗi trang {pageNum} ({ex.Message}). Thử Slow Path fallback...");
                                    await DownloadPageSlowPathAsync(item.Link, pageNum, localFilePath, token);
                                }
                            }
                            else
                            {
                                await DownloadPageSlowPathAsync(item.Link, pageNum, localFilePath, token);
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
            }
        }

        private async Task DownloadPageSlowPathAsync(string galleryUrl, int pageNum, string targetPath, CancellationToken token)
        {
            // Respect pause check
            while (_isDownloadPaused)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(200, token);
            }
            token.ThrowIfCancellationRequested();

            string pageUrl = $"{galleryUrl}/{pageNum}";
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
                while (_isDownloadPaused)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(200, token);
                }
                token.ThrowIfCancellationRequested();

                byte[] bytes = await _httpClient.GetByteArrayAsync(imgUrl);
                File.WriteAllBytes(finalPath, bytes);
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
            Directory.CreateDirectory(targetFolder);

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

            using (var semaphore = new SemaphoreSlim(maxThreads))
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
                            string localFilePath = Path.Combine(targetFolder, fileName);

                            // Skip if file already exists (with any common image extension)
                            bool alreadyExists = false;
                            string existingFile = null;
                            if (isFastPath)
                            {
                                if (File.Exists(localFilePath) && new FileInfo(localFilePath).Length > 1024)
                                {
                                    alreadyExists = true;
                                    existingFile = localFilePath;
                                }
                            }
                            else
                            {
                                string[] checkExts = { "jpg", "png", "webp", "gif", "jpeg" };
                                foreach (var checkExt in checkExts)
                                {
                                    string testPath = Path.ChangeExtension(localFilePath, checkExt);
                                    if (File.Exists(testPath) && new FileInfo(testPath).Length > 1024)
                                    {
                                        alreadyExists = true;
                                        existingFile = testPath;
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
                                    byte[] bytes = await GetByteArrayWithRefererAsync(imgUrl, "https://nhentai.net/");
                                    File.WriteAllBytes(localFilePath, bytes);
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
                                            byte[] bytes = await GetByteArrayWithRefererAsync(altUrl, "https://nhentai.net/");
                                            string finalPath = Path.Combine(targetFolder, $"{pageNum:D3}.{otherExt}");
                                            File.WriteAllBytes(finalPath, bytes);
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

                byte[] bytes = await GetByteArrayWithRefererAsync(imgUrl, pageUrl);
                File.WriteAllBytes(finalPath, bytes);
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

            Log("[nhentai.net] Phát hiện thử thách Cloudflare / Captcha. Đang mở trình duyệt giải tự động...");

            bool solved = false;
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

                    Log("[nhentai.net] Đồng bộ cookie và User-Agent thành công!");
                    solved = true;
                }
                else
                {
                    Log("[nhentai.net] Người dùng hủy bỏ giải captcha.");
                }
            });

            if (solved)
            {
                // Double check if solved successfully
                bool stillBlocked = await CheckIfNhentaiBlockedAsync(testUrl);
                if (stillBlocked)
                {
                    Log("[nhentai.net] Vẫn bị chặn sau khi giải captcha. Vui lòng thử lại.");
                    return false;
                }
                return true;
            }

            return false;
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
}
