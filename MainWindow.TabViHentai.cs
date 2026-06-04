using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private readonly SemaphoreSlim _viHentaiHtmlGate = new SemaphoreSlim(1, 1);

        private void ViHentaiLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtViHentaiLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
                txtViHentaiLog.ScrollToEnd();
            });
        }

        private string GetViHentaiPageUrl(string baseUrl, int page)
        {
            baseUrl = baseUrl.Trim();
            // Remove page parameter
            string cleanUrl = Regex.Replace(baseUrl, @"([?&])page=\d+(&|$)", "$1", RegexOptions.IgnoreCase);
            cleanUrl = cleanUrl.TrimEnd('&', '?');

            string separator = cleanUrl.Contains("?") ? "&" : "?";
            return $"{cleanUrl}{separator}page={page}";
        }

        private async void BtnViHentaiFetchInfo_Click(object sender, RoutedEventArgs e)
        {
            string url = txtViHentaiTagUrl.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Vui lòng nhập URL hợp lệ (Please enter a valid URL).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            btnViHentaiFetchInfo.IsEnabled = false;
            lblStatus.Text = "Đang phân tích trang vi-hentai.pro...";
            progressBar.IsIndeterminate = true;
            ViHentaiLog($"Đang phân tích URL: {url}");

            try
            {
                string html = await GetViHentaiStringWithRetryAsync(url, CancellationToken.None, "analyze target page");
                
                int maxPage = 1;
                
                // 1. Check data-last-page attribute
                var lastPageMatch = Regex.Match(html, @"data-last-page=""(\d+)""", RegexOptions.IgnoreCase);
                if (lastPageMatch.Success)
                {
                    maxPage = int.Parse(lastPageMatch.Groups[1].Value);
                }
                else
                {
                    // 2. Check all page= href links
                    var pageMatches = Regex.Matches(html, @"[?&]page=(\d+)", RegexOptions.IgnoreCase);
                    foreach (Match m in pageMatches)
                    {
                        if (int.TryParse(m.Groups[1].Value, out int pageNum))
                        {
                            if (pageNum > maxPage) maxPage = pageNum;
                        }
                    }
                }

                txtViHentaiTotalPages.Text = maxPage.ToString();
                txtViHentaiPageTo.Text = maxPage.ToString();
                
                ViHentaiLog($"Phân tích hoàn tất. Phát hiện tối đa {maxPage} trang.");
                lblStatus.Text = $"Analysis complete. Found {maxPage} pages.";
            }
            catch (Exception ex)
            {
                ViHentaiLog($"Lỗi khi phân tích: {ex.Message}");
                txtViHentaiTotalPages.Text = "1";
                lblStatus.Text = "Analysis failed.";
            }
            finally
            {
                btnViHentaiFetchInfo.IsEnabled = true;
                progressBar.IsIndeterminate = false;
            }
        }

        private void TxtViHentaiTotalPages_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtViHentaiPageTo != null && txtViHentaiTotalPages != null)
            {
                txtViHentaiPageTo.Text = txtViHentaiTotalPages.Text;
            }
        }

        private async void BtnViHentaiScrape_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                btnViHentaiScrape.Content = "CANCELLING...";
                btnViHentaiScrape.IsEnabled = false;
                if (btnViHentaiCrawlMore != null) btnViHentaiCrawlMore.IsEnabled = false;
                return;
            }
            await ScrapeViHentaiAsync(clearExisting: true);
        }

        private async void BtnViHentaiCrawlMore_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                if (btnViHentaiCrawlMore != null)
                {
                    btnViHentaiCrawlMore.Content = "CANCELLING...";
                    btnViHentaiCrawlMore.IsEnabled = false;
                }
                btnViHentaiScrape.IsEnabled = false;
                return;
            }
            await ScrapeViHentaiAsync(clearExisting: false);
        }

        private async Task ScrapeViHentaiAsync(bool clearExisting)
        {
            string baseUrl = txtViHentaiTagUrl.Text.Trim();
            if (string.IsNullOrEmpty(baseUrl))
            {
                MessageBox.Show("Vui lòng nhập URL hợp lệ (Please enter a valid URL).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = "https://" + baseUrl;
            }

            if (!int.TryParse(txtViHentaiPageFrom.Text, out int pageFrom) || pageFrom < 1)
            {
                MessageBox.Show("Trang bắt đầu không hợp lệ (Invalid 'From Page').", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(txtViHentaiPageTo.Text, out int pageTo) || pageTo < pageFrom)
            {
                MessageBox.Show("Trang kết thúc không hợp lệ (Invalid 'To Page').", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            btnViHentaiScrape.Content = "STOP CRAWLER";
            if (btnViHentaiCrawlMore != null)
            {
                btnViHentaiCrawlMore.Content = "STOP CRAWLER";
            }
            btnViHentaiFetchInfo.IsEnabled = false;
            lblStatus.Text = "Đang cào vi-hentai.pro...";
            progressBar.Value = 0;

            if (clearExisting)
            {
                _scrapedItems.Clear();
                if (chkSelectAll != null)
                {
                    chkSelectAll.IsChecked = false;
                }
                lblLinkCount.Text = "0";
            }

            ViHentaiLog($"Bắt đầu cào từ trang {pageFrom} đến {pageTo}...");

            try
            {
                int totalPages = pageTo - pageFrom + 1;
                int pagesProcessed = 0;

                for (int page = pageFrom; page <= pageTo; page++)
                {
                    token.ThrowIfCancellationRequested();

                    string pageUrl = GetViHentaiPageUrl(baseUrl, page);
                    ViHentaiLog($"Đang tải trang {page}: {pageUrl}");

                    string html = await GetViHentaiStringWithRetryAsync(pageUrl, token, $"crawl page {page}");
                    
                    // Match <a> tags containing /truyen/ links
                    var viewMatches = Regex.Matches(html, @"<a[^>]+href=[""'](?<link>[^""']*?/truyen/[^""']*)[""'][^>]*>(?<content>[\s\S]*?)<\/a>", RegexOptions.IgnoreCase);
                    
                    int pageCount = 0;
                    foreach (Match match in viewMatches)
                    {
                        string relativeLink = match.Groups["link"].Value.Trim();

                        // Normalize link
                        string fullLink = relativeLink;
                        if (!fullLink.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                            !fullLink.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            fullLink = "https://vi-hentai.pro" + (fullLink.StartsWith("/") ? "" : "/") + fullLink;
                        }

                        if (!TryNormalizeViHentaiMangaLink(fullLink, out string normalizedLink))
                        {
                            continue;
                        }
                        fullLink = normalizedLink;

                        string title = GetViHentaiTitleFromSlug(GetViHentaiSlugFromLink(fullLink));
                        title = FormatGalleryTitle(title);
                        title = CleanChapterSuffix(title);

                        var existingItem = _scrapedItems.FirstOrDefault(item => item.Link.Equals(fullLink, StringComparison.OrdinalIgnoreCase));
                        if (existingItem == null)
                        {
                            _scrapedItems.Add(new GalleryItem
                            {
                                Link = fullLink,
                                Name = title,
                                OriginalIndex = _scrapedItems.Count,
                                IsChecked = false
                            });
                            pageCount++;
                        }
                    }

                    pagesProcessed++;
                    double progressPct = ((double)pagesProcessed / totalPages) * 100;
                    progressBar.Value = progressPct;
                    lblStatus.Text = $"Searching page {page}/{pageTo} ({progressPct:0}%)";
                    ViHentaiLog($"Trang {page} hoàn tất. Tìm thấy {pageCount} liên kết mới.");
                }

                // Sort items deterministically (by Name then OriginalIndex)
                var sortedItems = _scrapedItems
                    .OrderBy(item => item.Name)
                    .ThenBy(item => item.OriginalIndex)
                    .ToList();
                _scrapedItems.Clear();
                foreach (var item in sortedItems) _scrapedItems.Add(item);

                RecalculateDuplicates();
                ViHentaiLog($"Cào dữ liệu hoàn tất! Tổng cộng thu thập được {_scrapedItems.Count} liên kết độc nhất.");
                lblStatus.Text = "Crawling completed successfully.";
                lblLinkCount.Text = _scrapedItems.Count.ToString();
            }
            catch (OperationCanceledException)
            {
                ViHentaiLog("Đã hủy cào theo yêu cầu người dùng.");
                lblStatus.Text = "Crawling cancelled.";
            }
            catch (Exception ex)
            {
                ViHentaiLog($"Lỗi nghiêm trọng khi cào: {ex.Message}");
                lblStatus.Text = "Crawling failed.";
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                btnViHentaiScrape.Content = "START CRAWLING";
                btnViHentaiScrape.IsEnabled = true;
                if (btnViHentaiCrawlMore != null)
                {
                    btnViHentaiCrawlMore.Content = "CRAWL MORE";
                    btnViHentaiCrawlMore.IsEnabled = true;
                }
                btnViHentaiFetchInfo.IsEnabled = true;
            }
        }

        private async void BtnViHentaiPasteDirect_Click(object sender, RoutedEventArgs e)
        {
            var win = new DirectDownloadWindow(isNhentai: false);
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                var links = win.ImportedLinks;
                if (links != null && links.Any())
                {
                    await ImportViHentaiDirectLinksAsync(links);
                }
            }
        }

        private async Task ImportViHentaiDirectLinksAsync(System.Collections.Generic.List<string> links)
        {
            btnViHentaiScrape.IsEnabled = false;
            btnViHentaiFetchInfo.IsEnabled = false;
            if (btnStartDownload != null) btnStartDownload.IsEnabled = false;
            
            progressBar.Value = 0;
            progressBar.IsIndeterminate = false;

            int total = links.Count;
            int imported = 0;
            int failed = 0;

            ViHentaiLog($"[Import] Bắt đầu phân tích và nhập {total} liên kết trực tiếp...");
            lblStatus.Text = $"Importing 0/{total} links...";

            try
            {
                for (int i = 0; i < total; i++)
                {
                    string link = links[i];
                    string normalizedLink = NormalizeViHentaiLink(link);
                    lblStatus.Text = $"[{i + 1}/{total}] Đang phân tích: {normalizedLink}";

                    try
                    {
                        if (_scrapedItems.Any(item => item.Link.Equals(normalizedLink, StringComparison.OrdinalIgnoreCase)))
                        {
                            ViHentaiLog($"[Import] Bỏ qua liên kết đã tồn tại: {normalizedLink}");
                            imported++;
                            continue;
                        }

                        string slug = GetViHentaiSlugFromLink(normalizedLink);
                        string title = GetViHentaiTitleFromSlug(slug);
                        title = FormatGalleryTitle(title);
                        title = CleanChapterSuffix(title);

                        Dispatcher.Invoke(() =>
                        {
                            _scrapedItems.Add(new GalleryItem
                            {
                                Link = normalizedLink,
                                Name = title,
                                OriginalIndex = _scrapedItems.Count,
                                IsChecked = true,
                                HasNoChapters = false
                            });
                        });

                        ViHentaiLog($"[Import {i + 1}/{total}] Thành công: {title}");
                        imported++;
                    }
                    catch (Exception ex)
                    {
                        ViHentaiLog($"[Import] Lỗi xử lý link '{normalizedLink}': {ex.Message}");
                        failed++;

                        string fallbackTitle = "Fallback - ViHentai - " + GetViHentaiSlugFromLink(normalizedLink);
                        Dispatcher.Invoke(() =>
                        {
                            _scrapedItems.Add(new GalleryItem
                            {
                                Link = normalizedLink,
                                Name = fallbackTitle,
                                OriginalIndex = _scrapedItems.Count,
                                IsChecked = true,
                                HasNoChapters = false
                            });
                        });
                    }

                    double pct = ((double)(i + 1) / total) * 100;
                    progressBar.Value = pct;
                }

                RecalculateDuplicates();
                lblLinkCount.Text = _scrapedItems.Count.ToString();
                ViHentaiLog($"[Import] Nhập hoàn tất! Thành công: {imported}, Lỗi/Fallback: {failed}.");
                lblStatus.Text = $"Import completed. Success: {imported}, Failed: {failed}.";
                
                MessageBox.Show($"Đã nhập thành công {total} đường dẫn!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                btnViHentaiScrape.IsEnabled = true;
                btnViHentaiFetchInfo.IsEnabled = true;
                if (btnStartDownload != null) btnStartDownload.IsEnabled = true;
                progressBar.Value = 100;
            }
        }

        private string GetViHentaiSlugFromLink(string link)
        {
            try
            {
                var uri = new Uri(link);
                var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2)
                {
                    return segments[1];
                }
            }
            catch {}
            return "Unknown";
        }

        private string GetViHentaiTitleFromSlug(string slug)
        {
            if (string.IsNullOrEmpty(slug)) return "Unknown";
            var words = slug.Split('-');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
                }
            }
            return string.Join(" ", words);
        }

        private string CleanChapterSuffix(string title)
        {
            if (string.IsNullOrEmpty(title)) return title;
            // Clean common chapter suffixes: e.g. " - chương 1", " - oneshot", " - full color", " - chap 2", " - part 3"
            string pattern = @"\s*-\s*(?:chương|chuong|oneshot|full\s*color|chap|ch|part|pt|vol|đầy\s*đủ)\s*\d*\s*$";
            title = Regex.Replace(title, pattern, "", RegexOptions.IgnoreCase);
            return title.Trim();
        }

        private string NormalizeViHentaiLink(string link)
        {
            if (string.IsNullOrEmpty(link)) return link;
            try
            {
                if (!link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                    !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    link = "https://vi-hentai.pro" + (link.StartsWith("/") ? "" : "/") + link;
                }

                var uri = new Uri(link);
                if (uri.Host.Contains("vi-hentai.pro"))
                {
                    var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 2 && segments[0].Equals("truyen", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"https://vi-hentai.pro/truyen/{segments[1]}";
                    }
                }
            }
            catch {}
            return link;
        }

        private bool TryNormalizeViHentaiMangaLink(string link, out string normalizedLink)
        {
            normalizedLink = null;
            if (string.IsNullOrWhiteSpace(link))
            {
                return false;
            }

            try
            {
                if (!link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    link = "https://vi-hentai.pro" + (link.StartsWith("/") ? "" : "/") + link;
                }

                var uri = new Uri(link);
                if (!uri.Host.Contains("vi-hentai.pro"))
                {
                    return false;
                }

                var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length != 2 || !segments[0].Equals("truyen", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                normalizedLink = $"https://vi-hentai.pro/truyen/{segments[1]}";
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> GetViHentaiStringWithRetryAsync(string url, CancellationToken token, string action)
        {
            await _viHentaiHtmlGate.WaitAsync(token);
            try
            {
                bool ok = await SolveViHentaiCaptchaIfNeededAsync(url);
                if (!ok)
                {
                    throw new Exception("Bị chặn bởi Cloudflare Captcha trên vi-hentai.pro.");
                }

                int delayMs = 1200;
                for (int attempt = 1; attempt <= 4; attempt++)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                        using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, token))
                        {
                            if ((int)response.StatusCode == 429 && attempt < 4)
                            {
                                ViHentaiLog($"[Throttle] vi-hentai trả về 429 khi {action}. Tự động mở cửa sổ Captcha để gia hạn session...");
                                bool solved = await SolveViHentaiCaptchaIfNeededAsync(url);
                                if (solved)
                                {
                                    ViHentaiLog("[Throttle] Đã giải captcha thành công, thử lại ngay lập tức.");
                                    continue;
                                }

                                int retryDelay = GetRetryDelayMilliseconds(response, attempt, delayMs);
                                ViHentaiLog($"[Throttle] vi-hentai trả về 429. Chờ {retryDelay}ms rồi thử lại ({attempt}/4).");
                                await Task.Delay(retryDelay, token);
                                delayMs = Math.Min(delayMs * 2, 8000);
                                continue;
                            }

                            response.EnsureSuccessStatusCode();
                            string content = await response.Content.ReadAsStringAsync();
                            await Task.Delay(delayMs, token);
                            return content;
                        }
                    }
                    catch (HttpRequestException ex) when (attempt < 4)
                    {
                        ViHentaiLog($"[Retry] Lỗi request vi-hentai khi {action}: {ex.Message}. Thử lại sau {delayMs}ms ({attempt}/4).");
                        await Task.Delay(delayMs, token);
                        delayMs = Math.Min(delayMs * 2, 8000);
                    }
                    catch (TaskCanceledException) when (!token.IsCancellationRequested && attempt < 4)
                    {
                        ViHentaiLog($"[Retry] Request vi-hentai bị timeout khi {action}. Thử lại sau {delayMs}ms ({attempt}/4).");
                        await Task.Delay(delayMs, token);
                        delayMs = Math.Min(delayMs * 2, 8000);
                    }
                }

                throw new Exception($"Không thể tải nội dung vi-hentai cho tác vụ: {action}");
            }
            finally
            {
                _viHentaiHtmlGate.Release();
            }
        }

        private async Task<byte[]> GetViHentaiByteArrayWithRefererAsync(string url, string referer, CancellationToken token)
        {
            int delayMs = 800;
            for (int attempt = 1; attempt <= 4; attempt++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        request.Headers.Referrer = new Uri(referer);
                        using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
                        {
                            if ((int)response.StatusCode == 429 && attempt < 4)
                            {
                                int retryDelay = GetRetryDelayMilliseconds(response, attempt, delayMs);
                                Log($"[vi-hentai.pro] 429 khi tải ảnh. Chờ {retryDelay}ms rồi thử lại ({attempt}/4): {url}");
                                await Task.Delay(retryDelay, token);
                                delayMs = Math.Min(delayMs * 2, 8000);
                                continue;
                            }

                            response.EnsureSuccessStatusCode();
                            return await response.Content.ReadAsByteArrayAsync();
                        }
                    }
                }
                catch (HttpRequestException ex) when (attempt < 4)
                {
                    Log($"[vi-hentai.pro] Retry tải ảnh do lỗi mạng: {ex.Message}. Chờ {delayMs}ms.");
                    await Task.Delay(delayMs, token);
                    delayMs = Math.Min(delayMs * 2, 8000);
                }
                catch (TaskCanceledException) when (!token.IsCancellationRequested && attempt < 4)
                {
                    Log($"[vi-hentai.pro] Retry tải ảnh do timeout. Chờ {delayMs}ms.");
                    await Task.Delay(delayMs, token);
                    delayMs = Math.Min(delayMs * 2, 8000);
                }
            }

            throw new Exception($"Không thể tải ảnh vi-hentai: {url}");
        }

        private int GetRetryDelayMilliseconds(HttpResponseMessage response, int attempt, int fallbackDelayMs)
        {
            if (response.Headers.RetryAfter?.Delta.HasValue == true)
            {
                double retryMs = response.Headers.RetryAfter.Delta.Value.TotalMilliseconds;
                return (int)Math.Max(retryMs, fallbackDelayMs);
            }

            return Math.Min(fallbackDelayMs * (attempt + 1), 10000);
        }

        // ==========================================
        // DECRYPTION CORE (SCRIPT 10 Packer Decoder)
        // ==========================================

        public static string DecodeViHentaiPayload(string h, int u, string n, int t, int e, int r_val)
        {
            char separator = n[e];
            System.Text.StringBuilder r = new System.Text.StringBuilder();

            int i = 0;
            while (i < h.Length)
            {
                System.Text.StringBuilder s = new System.Text.StringBuilder();
                while (i < h.Length && h[i] != separator)
                {
                    s.Append(h[i]);
                    i++;
                }
                i++; // skip separator

                if (s.Length > 0)
                {
                    string sStr = s.ToString();
                    for (int j = 0; j < n.Length; j++)
                    {
                        sStr = sStr.Replace(n[j].ToString(), j.ToString());
                    }

                    long val = ConvertBase(sStr, e, 10);
                    long charCode = val - t;
                    r.Append((char)charCode);
                }
            }

            string decodedStr = r.ToString();
            
            // Standard JavaScript decodeURIComponent(escape(r)) translation
            // In JS, escape() turns characters > 255 into %uXXXX, and characters <= 255 into %XX.
            // Since our char values correspond to bytes in a UTF-8 string:
            byte[] bytes = new byte[decodedStr.Length];
            for (int k = 0; k < decodedStr.Length; k++)
            {
                bytes[k] = (byte)decodedStr[k];
            }
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        private static long ConvertBase(string d, int e, int f)
        {
            string chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ+/";
            string h = chars.Substring(0, e);
            string i = chars.Substring(0, f);

            char[] dArr = d.ToCharArray();
            Array.Reverse(dArr);

            long j = 0;
            for (int c = 0; c < dArr.Length; c++)
            {
                char b = dArr[c];
                int index = h.IndexOf(b);
                if (index != -1)
                {
                    j += index * (long)Math.Pow(e, c);
                }
            }

            return j;
        }

        private async Task DownloadViHentaiGalleryAsync(GalleryItem item, string rootFolder, CancellationToken token, DownloadQueueItem queueItem = null, HashSet<int> chapterFilter = null)
        {
            var uri = new Uri(item.Link);
            var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 2)
            {
                // Manga Details page - fetch chapters
                string html = await GetViHentaiStringWithRetryAsync(item.Link, token, $"load manga page '{item.Name}'");
                string mangaSlug = segments[1];

                // Update Name with a better name from <title> tag if available
                var titleMatch = Regex.Match(html, @"<title>\s*(.*?)\s*</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (titleMatch.Success)
                {
                    string rawTitle = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
                    string suffix = " - Việt Hentai";
                    if (rawTitle.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        rawTitle = rawTitle.Substring(0, rawTitle.Length - suffix.Length).Trim();
                    }
                    string suffix2 = " - Kuro Neko";
                    if (rawTitle.Contains(suffix2))
                    {
                        rawTitle = rawTitle.Replace(suffix2, "").Trim();
                    }
                    item.Name = FormatGalleryTitle(rawTitle);
                }

                // Regex matches /truyen/mangaSlug/chapterSlug
                var chapterMatches = Regex.Matches(html, @"href=[""'](?<link>[^""']*?/truyen/" + Regex.Escape(mangaSlug) + @"/[^""']+)[""']", RegexOptions.IgnoreCase);

                var chapterLinks = new System.Collections.Generic.List<string>();
                var seenChapterLinks = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match m in chapterMatches)
                {
                    string link = m.Groups["link"].Value.Trim();
                    if (!link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                        !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        link = "https://vi-hentai.pro" + (link.StartsWith("/") ? "" : "/") + link;
                    }

                    try
                    {
                        var cUri = new Uri(link);
                        var cSegs = cUri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (cSegs.Length >= 3)
                        {
                            string normalizedChapterLink = "https://vi-hentai.pro/" + string.Join("/", cSegs);
                            if (seenChapterLinks.Add(normalizedChapterLink))
                            {
                                chapterLinks.Add(normalizedChapterLink);
                            }
                        }
                    }
                    catch {}
                }

                if (chapterLinks.Count == 0)
                {
                    item.HasNoChapters = true;
                    string safeManga = GetSafePathName(item.Name);
                    string sourceFolder = Path.Combine(rootFolder, "vi-hentai.pro", safeManga);
                    string targetFolder = Path.Combine(rootFolder, "vi-hentai.pro", ".missing", safeManga);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetFolder));
                    if (Directory.Exists(sourceFolder) &&
                        !string.Equals(sourceFolder, targetFolder, StringComparison.OrdinalIgnoreCase) &&
                        !Directory.Exists(targetFolder))
                    {
                        Directory.Move(sourceFolder, targetFolder);
                    }
                    Directory.CreateDirectory(targetFolder);
                    File.WriteAllText(Path.Combine(targetFolder, "info.txt"), $"Truyện '{item.Name}' ({item.Link}) không có chương nào.");
                    Log($"[vi-hentai.pro] Truyện '{item.Name}' không có chương nào. Đã phân loại vào thư mục .missing.");
                    return;
                }

                var totalFoundChapters = chapterLinks.Count;
                if (chapterFilter != null)
                {
                    var filtered = new System.Collections.Generic.List<string>();
                    foreach (var link in chapterLinks)
                    {
                        double chapNum = ParseChapterNumber(link);
                        int chapInt = (int)Math.Floor(chapNum);
                        if (chapterFilter.Contains(chapInt))
                        {
                            filtered.Add(link);
                        }
                    }
                    chapterLinks = filtered;
                    if (chapterLinks.Count == 0)
                    {
                        Log($"[vi-hentai.pro] Không có chương nào trùng khớp với bộ lọc đã chọn trong tổng số {totalFoundChapters} chương của '{item.Name}'.");
                        if (queueItem != null)
                        {
                            Dispatcher.Invoke(() => {
                                queueItem.Status = "Completed";
                                queueItem.CurrentProcess = "Không có chương trùng khớp bộ lọc";
                            });
                        }
                        return;
                    }
                }

                Log($"[vi-hentai.pro] Phát hiện {chapterLinks.Count} chapters cho truyện '{item.Name}'. Sẽ tải lần lượt...");

                if (queueItem != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        queueItem.TotalChapters = chapterLinks.Count;
                        queueItem.CompletedChapters = 0;
                    });
                }

                for (int idx = 0; idx < chapterLinks.Count; idx++)
                {
                    token.ThrowIfCancellationRequested();
                    string chapLink = chapterLinks[idx];
                    Log($"[vi-hentai.pro] Đang tải chapter {idx + 1}/{chapterLinks.Count}: {chapLink}");

                    var chapItem = new GalleryItem { Link = chapLink, Name = item.Name };
                    await DownloadViHentaiChapterAsync(chapItem, rootFolder, token, queueItem, isParentQueue: true);

                    if (queueItem != null)
                    {
                        int currentIdx = idx + 1;
                        Dispatcher.Invoke(() =>
                        {
                            queueItem.CompletedChapters = currentIdx;
                        });
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    item.LinkCount = chapterLinks.Count;
                });
            }
            else if (segments.Length >= 3)
            {
                // Direct Chapter page
                await DownloadViHentaiChapterAsync(item, rootFolder, token, queueItem, isParentQueue: false);
            }
            else
            {
                throw new Exception("Đường dẫn vi-hentai.pro không hợp lệ. Phải là trang chi tiết truyện hoặc trang đọc chapter.");
            }
        }

        private async Task DownloadViHentaiChapterAsync(GalleryItem item, string rootFolder, CancellationToken token, DownloadQueueItem queueItem = null, bool isParentQueue = false)
        {
            string html = await GetViHentaiStringWithRetryAsync(item.Link, token, $"load chapter page '{item.Link}'");

            string mangaTitle = "Unknown Manga";
            string chapterTitle = "Unknown Chapter";
            try
            {
                var titleMatch = Regex.Match(html, @"<title>\s*(.*?)\s*</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (titleMatch.Success)
                {
                    string rawTitle = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
                    string suffix = " - Việt Hentai";
                    if (rawTitle.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        rawTitle = rawTitle.Substring(0, rawTitle.Length - suffix.Length).Trim();
                    }
                    string suffix2 = " - Kuro Neko";
                    if (rawTitle.Contains(suffix2))
                    {
                        rawTitle = rawTitle.Replace(suffix2, "").Trim();
                    }

                    string[] parts = rawTitle.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        chapterTitle = parts[0].Trim();
                        mangaTitle = parts[1].Trim();
                    }
                    else if (parts.Length == 1)
                    {
                        chapterTitle = parts[0].Trim();
                    }
                }
            }
            catch {}

            if (string.IsNullOrEmpty(mangaTitle) || mangaTitle == "Unknown Manga")
            {
                mangaTitle = item.Name;
            }
            if (string.IsNullOrEmpty(chapterTitle) || chapterTitle == "Unknown Chapter")
            {
                try
                {
                    var uri = new Uri(item.Link);
                    var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 3) chapterTitle = string.Join(" - ", segments.Skip(2));
                }
                catch {}
            }

            string safeManga = GetSafePathName(mangaTitle);
            string safeChapter = GetSafePathName(chapterTitle);
            string targetFolder = Path.Combine(rootFolder, "vi-hentai.pro", $"{safeManga}-{safeChapter}");
            string tempFolder = Path.Combine(rootFolder, "vi-hentai.pro", $".tmp_{safeManga}_{safeChapter}_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempFolder);

            var evalIndex = html.IndexOf("eval(function(h,u,n,t,e,r)");
            if (evalIndex == -1)
            {
                throw new Exception("Không tìm thấy khối mã hóa ảnh trong trang (Obfuscated JS block not found).");
            }

            string sub = html.Substring(evalIndex);
            var matchParams = Regex.Match(sub, @"}\s*\(\s*""(?<h>[^""]+)""\s*,\s*(?<u>\d+)\s*,\s*""(?<n>[^""]+)""\s*,\s*(?<t>\d+)\s*,\s*(?<e>\d+)\s*,\s*(?<r>\d+)\s*\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!matchParams.Success)
            {
                throw new Exception("Không thể phân tích tham số giải mã (Could not parse decoding parameters).");
            }

            string h = matchParams.Groups["h"].Value;
            int u = int.Parse(matchParams.Groups["u"].Value);
            string n = matchParams.Groups["n"].Value;
            int t = int.Parse(matchParams.Groups["t"].Value);
            int e = int.Parse(matchParams.Groups["e"].Value);
            int r_val = int.Parse(matchParams.Groups["r"].Value);

            string decoded = DecodeViHentaiPayload(h, u, n, t, e, r_val);

            var imgMatches = Regex.Matches(decoded, @"""(?<imgUrl>https?:[^""]+)""", RegexOptions.IgnoreCase);
            var imageUrls = new System.Collections.Generic.List<string>();
            foreach (Match m in imgMatches)
            {
                string url = m.Groups["imgUrl"].Value.Replace(@"\/", "/").Replace(@"\", "");
                if (!imageUrls.Contains(url))
                {
                    imageUrls.Add(url);
                }
            }

            if (imageUrls.Count == 0)
            {
                throw new Exception("Không tìm thấy URL ảnh sau khi giải mã.");
            }

            int maxThreads = 2;
            Dispatcher.Invoke(() =>
            {
                if (cmbConnections.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem && int.TryParse(selectedItem.Content.ToString(), out int val))
                {
                    maxThreads = Math.Max(1, Math.Min(val, 2));
                }
            });

            Log($"[vi-hentai.pro] Bắt đầu tải {imageUrls.Count} trang của chapter '{chapterTitle}' với {maxThreads} kết nối song song...");

            if (queueItem != null && !isParentQueue)
            {
                Dispatcher.Invoke(() =>
                {
                    queueItem.TotalChapters = imageUrls.Count;
                    queueItem.CompletedChapters = 0;
                });
            }

            using (var semaphore = new DynamicSemaphore(maxThreads, () => Math.Max(1, Math.Min(GetCurrentConnectionLimit(), 2))))
            {
                var tasks = new System.Collections.Generic.List<Task>();
                int completedPages = 0;
                object lockObj = new object();

                for (int p = 0; p < imageUrls.Count; p++)
                {
                    int index = p;
                    string imgUrl = imageUrls[index];

                    tasks.Add(Task.Run(async () =>
                    {
                        while (_isDownloadPaused)
                        {
                            token.ThrowIfCancellationRequested();
                            await Task.Delay(200, token);
                        }
                        token.ThrowIfCancellationRequested();

                        await semaphore.WaitAsync(token);
                        try
                        {
                            while (_isDownloadPaused)
                            {
                                token.ThrowIfCancellationRequested();
                                await Task.Delay(200, token);
                            }
                            token.ThrowIfCancellationRequested();

                            string ext = Path.GetExtension(imgUrl.Split('?')[0]);
                            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                            string localFilePath = Path.Combine(tempFolder, $"{index + 1:D3}{ext}");
                            string finalFilePath = Path.Combine(targetFolder, $"{index + 1:D3}{ext}");

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
                                            if (isParentQueue)
                                            {
                                                queueItem.CurrentProcess = $"{chapterTitle} (trang {completedPages}/{imageUrls.Count})";
                                            }
                                            else
                                            {
                                                queueItem.CompletedChapters = completedPages;
                                                queueItem.CurrentProcess = $"Trang {completedPages}/{imageUrls.Count}";
                                            }
                                        });
                                    }
                                    if (completedPages % 5 == 0 || completedPages == imageUrls.Count)
                                    {
                                        Dispatcher.Invoke(() =>
                                        {
                                            lblStatus.Text = $"[{completedPages}/{imageUrls.Count}] Tải {mangaTitle} - {chapterTitle}";
                                        });
                                    }
                                }
                                return;
                            }

                            try
                            {
                                await DownloadUrlToFileWithRefererAsync(imgUrl, item.Link, localFilePath, token, isViHentai: true);
                            }
                            catch (Exception ex)
                            {
                                lock (lockObj)
                                {
                                    if (queueItem != null)
                                    {
                                        queueItem.AddError(chapterTitle, index + 1, ex.Message, imgUrl);
                                    }
                                    Log($"[vi-hentai.pro] Lỗi tải trang {index + 1} của chapter '{chapterTitle}': {ex.Message}");
                                }
                            }

                            lock (lockObj)
                            {
                                completedPages++;
                                if (queueItem != null)
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        if (isParentQueue)
                                        {
                                            queueItem.CurrentProcess = $"{chapterTitle} (trang {completedPages}/{imageUrls.Count})";
                                        }
                                        else
                                        {
                                            queueItem.CompletedChapters = completedPages;
                                            queueItem.CurrentProcess = $"Trang {completedPages}/{imageUrls.Count}";
                                        }
                                    });
                                }
                                if (completedPages % 5 == 0 || completedPages == imageUrls.Count)
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        lblStatus.Text = $"[{completedPages}/{imageUrls.Count}] Tải {mangaTitle} - {chapterTitle}";
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
                    Log($"[Lỗi] Không thể di chuyển thư mục tạm vi-hentai: {ex.Message}");
                }

                // Check for missing files
                ValidateDownloadedFiles(targetFolder, imageUrls.Count, queueItem, chapterTitle);
            }
        }
    }
}
