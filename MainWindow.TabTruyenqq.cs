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
        private void TruyenqqLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtTruyenqqLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
                txtTruyenqqLog.ScrollToEnd();
            });
        }

        internal async Task<bool> CheckIfTruyenqqBlockedAsync(string testUrl)
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

                        using (var content = response.Content)
                        {
                            string html = await content.ReadAsStringAsync();
                            if (html.Contains("cf-challenge") || 
                                html.Contains("cf-turnstile") || 
                                html.Contains("Turnstile") || 
                                html.Contains("Just a moment...") ||
                                html.Contains("thực hiện xác minh bảo mật") ||
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
                if (ex.Message.Contains("403") || ex.Message.Contains("503"))
                {
                    return true;
                }
                return false;
            }
        }

        internal async Task<bool> SolveTruyenqqCaptchaIfNeededAsync(string testUrl)
        {
            bool isBlocked = await CheckIfTruyenqqBlockedAsync(testUrl);
            if (!isBlocked)
            {
                return true; // Not blocked
            }

            TruyenqqLog("Phát hiện thử thách Cloudflare / Captcha. Đang mở trình duyệt giải tự động...");

            bool solved = false;
            Dispatcher.Invoke(() =>
            {
                var captchaWin = new CaptchaWindow(testUrl)
                {
                    Owner = this
                };

                if (captchaWin.ShowDialog() == true)
                {
                    var originalUri = new Uri(testUrl);
                    var resolvedUri = captchaWin.ResolvedUri ?? originalUri;

                    // Add cookies for resolvedUri
                    var resolvedCookies = captchaWin.ResolvedCookies.GetCookies(resolvedUri);
                    foreach (Cookie cookie in resolvedCookies)
                    {
                        _cookieContainer.Add(resolvedUri, cookie);
                    }

                    // Add cookies for originalUri if different
                    if (originalUri.Host != resolvedUri.Host)
                    {
                        var originalCookies = captchaWin.ResolvedCookies.GetCookies(originalUri);
                        foreach (Cookie cookie in originalCookies)
                        {
                            _cookieContainer.Add(originalUri, cookie);
                        }
                    }

                    if (!string.IsNullOrEmpty(captchaWin.UserAgent))
                    {
                        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
                        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(captchaWin.UserAgent);
                    }

                    TruyenqqLog("Đồng bộ cookie và User-Agent thành công!");
                    solved = true;
                }
                else
                {
                    TruyenqqLog("Người dùng hủy bỏ giải captcha.");
                }
            });

            if (solved)
            {
                bool stillBlocked = await CheckIfTruyenqqBlockedAsync(testUrl);
                if (stillBlocked)
                {
                    TruyenqqLog("Vẫn bị chặn sau khi giải captcha. Vui lòng thử lại.");
                    return false;
                }
                return true;
            }

            return false;
        }

        private bool IsTruyenqqUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            try
            {
                var uri = new Uri(url);
                return Regex.IsMatch(uri.Host, @"truyenqq[a-zA-Z0-9-]*\.com", RegexOptions.IgnoreCase);
            }
            catch
            {
                return Regex.IsMatch(url, @"truyenqq[a-zA-Z0-9-]*\.com", RegexOptions.IgnoreCase);
            }
        }

        private string ExtractTruyenqqBaseUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return $"{uri.Scheme}://{uri.Host}";
            }
            catch
            {
                var match = Regex.Match(url, @"^(https?://[^/]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            return "https://truyenqqto.com"; // Default fallback
        }

        private string GetTruyenqqPageUrl(string baseUrl, int page)
        {
            baseUrl = baseUrl.Trim();
            string cleanUrl = baseUrl;
            // Remove /trang-\d+ if present
            cleanUrl = Regex.Replace(cleanUrl, @"/trang-\d+/?", "", RegexOptions.IgnoreCase);
            cleanUrl = cleanUrl.TrimEnd('/');
            return $"{cleanUrl}/trang-{page}";
        }

        private async void BtnTruyenqqFetchInfo_Click(object sender, RoutedEventArgs e)
        {
            string url = txtTruyenqqTagUrl.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Vui lòng nhập URL hợp lệ.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            btnTruyenqqFetchInfo.IsEnabled = false;
            lblStatus.Text = "Đang phân tích trang truyenqq...";
            progressBar.IsIndeterminate = true;
            TruyenqqLog($"Đang phân tích URL: {url}");

            try
            {
                bool captchaOk = await SolveTruyenqqCaptchaIfNeededAsync(url);
                if (!captchaOk)
                {
                    TruyenqqLog("Không thể bypass Cloudflare. Hủy phân tích.");
                    lblStatus.Text = "Analysis failed (Cloudflare).";
                    return;
                }

                // Fetch targeted page HTML
                string html = await _httpClient.GetStringAsync(url);
                
                int maxPage = 1;
                // Parse page patterns: /trang-420 or trang-420
                var pageMatches = Regex.Matches(html, @"trang-(\d+)", RegexOptions.IgnoreCase);
                foreach (Match m in pageMatches)
                {
                    if (int.TryParse(m.Groups[1].Value, out int pageNum))
                    {
                        if (pageNum > maxPage) maxPage = pageNum;
                    }
                }

                txtTruyenqqTotalPages.Text = maxPage.ToString();
                txtTruyenqqPageTo.Text = maxPage.ToString();
                
                TruyenqqLog($"Phân tích hoàn tất. Phát hiện tối đa {maxPage} trang.");
                lblStatus.Text = $"Analysis complete. Found {maxPage} pages.";
            }
            catch (Exception ex)
            {
                TruyenqqLog($"Lỗi khi phân tích: {ex.Message}");
                txtTruyenqqTotalPages.Text = "1";
                lblStatus.Text = "Analysis failed.";
            }
            finally
            {
                btnTruyenqqFetchInfo.IsEnabled = true;
                progressBar.IsIndeterminate = false;
            }
        }

        private void TxtTruyenqqTotalPages_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtTruyenqqPageTo != null && txtTruyenqqTotalPages != null)
            {
                txtTruyenqqPageTo.Text = txtTruyenqqTotalPages.Text;
            }
        }

        private async void BtnTruyenqqScrape_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                btnTruyenqqScrape.Content = "CANCELLING...";
                btnTruyenqqScrape.IsEnabled = false;
                if (btnTruyenqqCrawlMore != null) btnTruyenqqCrawlMore.IsEnabled = false;
                return;
            }
            await ScrapeTruyenqqAsync(clearExisting: true);
        }

        private async void BtnTruyenqqCrawlMore_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                if (btnTruyenqqCrawlMore != null)
                {
                    btnTruyenqqCrawlMore.Content = "CANCELLING...";
                    btnTruyenqqCrawlMore.IsEnabled = false;
                }
                btnTruyenqqScrape.IsEnabled = false;
                return;
            }
            await ScrapeTruyenqqAsync(clearExisting: false);
        }

        private async Task ScrapeTruyenqqAsync(bool clearExisting)
        {
            string baseUrl = txtTruyenqqTagUrl.Text.Trim();
            if (string.IsNullOrEmpty(baseUrl))
            {
                MessageBox.Show("Vui lòng nhập URL hợp lệ.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = "https://" + baseUrl;
            }

            if (!int.TryParse(txtTruyenqqPageFrom.Text, out int pageFrom) || pageFrom < 1)
            {
                MessageBox.Show("Trang bắt đầu không hợp lệ.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(txtTruyenqqPageTo.Text, out int pageTo) || pageTo < pageFrom)
            {
                MessageBox.Show("Trang kết thúc không hợp lệ.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            btnTruyenqqScrape.Content = "STOP CRAWLER";
            if (btnTruyenqqCrawlMore != null)
            {
                btnTruyenqqCrawlMore.Content = "STOP CRAWLER";
            }
            btnTruyenqqFetchInfo.IsEnabled = false;
            lblStatus.Text = "Đang cào truyenqq...";
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

            TruyenqqLog($"Bắt đầu cào từ trang {pageFrom} đến {pageTo}...");

            try
            {
                int totalPages = pageTo - pageFrom + 1;
                int pagesProcessed = 0;

                for (int page = pageFrom; page <= pageTo; page++)
                {
                    token.ThrowIfCancellationRequested();

                    string pageUrl = GetTruyenqqPageUrl(baseUrl, page);
                    TruyenqqLog($"Đang tải trang {page}: {pageUrl}");

                    bool captchaOk = await SolveTruyenqqCaptchaIfNeededAsync(pageUrl);
                    if (!captchaOk)
                    {
                        TruyenqqLog($"Không thể bypass Cloudflare cho trang {page}. Bỏ qua trang này.");
                        continue;
                    }

                    string html = await _httpClient.GetStringAsync(pageUrl);
                    
                    // Match <a> tags containing /truyen-tranh/ links
                    var viewMatches = Regex.Matches(html, @"<a[^>]+href=[""'](?<link>[^""']*?/truyen-tranh/[^""']+)[""'][^>]*>(?<content>[\s\S]*?)<\/a>", RegexOptions.IgnoreCase);
                    
                    int pageCount = 0;
                    foreach (Match match in viewMatches)
                    {
                        string relativeLink = match.Groups["link"].Value.Trim();
                        if (Regex.IsMatch(relativeLink, @"-chap(?:-|\b)", RegexOptions.IgnoreCase))
                        {
                            continue; // Skip chapter links
                        }
                        string rawContent = match.Groups["content"].Value;
                        string title = Regex.Replace(rawContent, @"<[^>]+>", "").Trim();
                        title = WebUtility.HtmlDecode(title);

                        // Try to get title from title attribute if any
                        var titleAttrMatch = Regex.Match(match.Value, @"title=[""'](?<titleAttr>[^""']+)[""']", RegexOptions.IgnoreCase);
                        if (titleAttrMatch.Success)
                        {
                            string t = WebUtility.HtmlDecode(titleAttrMatch.Groups["titleAttr"].Value.Trim());
                            if (!string.IsNullOrEmpty(t) && t.Length > title.Length)
                            {
                                title = t;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(title) || title.Length < 2) continue;

                        // Normalize link
                        string fullLink = relativeLink;
                        if (!fullLink.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                            !fullLink.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            string activeDomain = ExtractTruyenqqBaseUrl(pageUrl);
                            fullLink = activeDomain + (fullLink.StartsWith("/") ? "" : "/") + fullLink;
                        }

                        // Remove trailing slash for normalization
                        fullLink = fullLink.TrimEnd('/');

                        var existingItem = _scrapedItems.FirstOrDefault(item => item.Link.Equals(fullLink, StringComparison.OrdinalIgnoreCase));
                        if (existingItem == null)
                        {
                            _scrapedItems.Add(new GalleryItem
                            {
                                Link = fullLink,
                                Name = FormatGalleryTitle(title),
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
                    TruyenqqLog($"Trang {page} hoàn tất. Tìm thấy {pageCount} liên kết mới.");
                }

                // Sort items deterministically
                var sortedItems = _scrapedItems
                    .OrderBy(item => item.Name)
                    .ThenBy(item => item.OriginalIndex)
                    .ToList();
                _scrapedItems.Clear();
                foreach (var item in sortedItems) _scrapedItems.Add(item);

                RecalculateDuplicates();
                TruyenqqLog($"Cào dữ liệu hoàn tất! Tổng cộng thu thập được {_scrapedItems.Count} liên kết độc nhất.");
                lblStatus.Text = "Crawling completed successfully.";
                lblLinkCount.Text = _scrapedItems.Count.ToString();
            }
            catch (OperationCanceledException)
            {
                TruyenqqLog("Đã hủy cào theo yêu cầu người dùng.");
                lblStatus.Text = "Crawling cancelled.";
            }
            catch (Exception ex)
            {
                TruyenqqLog($"Lỗi nghiêm trọng khi cào: {ex.Message}");
                lblStatus.Text = "Crawling failed.";
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                btnTruyenqqScrape.Content = "START CRAWLING";
                btnTruyenqqScrape.IsEnabled = true;
                if (btnTruyenqqCrawlMore != null)
                {
                    btnTruyenqqCrawlMore.Content = "CRAWL MORE";
                    btnTruyenqqCrawlMore.IsEnabled = true;
                }
                btnTruyenqqFetchInfo.IsEnabled = true;
            }
        }

        private async void BtnTruyenqqPasteDirect_Click(object sender, RoutedEventArgs e)
        {
            var win = new DirectDownloadWindow(isNhentai: false);
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                var links = win.ImportedLinks;
                if (links != null && links.Any())
                {
                    await ImportTruyenqqDirectLinksAsync(links);
                }
            }
        }

        private async Task ImportTruyenqqDirectLinksAsync(System.Collections.Generic.List<string> links)
        {
            btnTruyenqqScrape.IsEnabled = false;
            btnTruyenqqFetchInfo.IsEnabled = false;
            if (btnStartDownload != null) btnStartDownload.IsEnabled = false;
            
            progressBar.Value = 0;
            progressBar.IsIndeterminate = false;

            int total = links.Count;
            int imported = 0;
            int failed = 0;

            TruyenqqLog($"[Import] Bắt đầu phân tích và nhập {total} liên kết trực tiếp...");
            lblStatus.Text = $"Importing 0/{total} links...";

            try
            {
                for (int i = 0; i < total; i++)
                {
                    string link = links[i].Trim().TrimEnd('/');
                    
                    if (!link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                        !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        link = "https://" + link;
                    }

                    lblStatus.Text = $"[{i + 1}/{total}] Đang phân tích: {link}";

                    try
                    {
                        if (_scrapedItems.Any(item => item.Link.Equals(link, StringComparison.OrdinalIgnoreCase)))
                        {
                            TruyenqqLog($"[Import] Bỏ qua liên kết đã tồn tại: {link}");
                            imported++;
                            continue;
                        }

                        // Try to scrape title from target page
                        bool captchaOk = await SolveTruyenqqCaptchaIfNeededAsync(link);
                        if (!captchaOk)
                        {
                            TruyenqqLog($"[Import] Không thể bypass Cloudflare cho: {link}");
                            failed++;
                            continue;
                        }
                        string html = await _httpClient.GetStringAsync(link);
                        var titleMatch = Regex.Match(html, @"<title>\s*(.*?)\s*</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        string title = "Manga - " + link.Split('/').Last();
                        if (titleMatch.Success)
                        {
                            string rawTitle = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
                            
                            // Remove common suffixes
                            string[] commonSuffixes = { " - TruyệnQQ", " - TruyenQQ", " | TruyệnQQ", " | TruyenQQ" };
                            foreach (var suffix in commonSuffixes)
                            {
                                if (rawTitle.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                                {
                                    rawTitle = rawTitle.Substring(0, rawTitle.Length - suffix.Length).Trim();
                                }
                            }
                            title = rawTitle;
                        }

                        Dispatcher.Invoke(() =>
                        {
                            _scrapedItems.Add(new GalleryItem
                            {
                                Link = link,
                                Name = FormatGalleryTitle(title),
                                OriginalIndex = _scrapedItems.Count,
                                IsChecked = true,
                                HasNoChapters = false
                            });
                        });

                        TruyenqqLog($"[Import {i + 1}/{total}] Thành công: {title}");
                        imported++;
                    }
                    catch (Exception ex)
                    {
                        TruyenqqLog($"[Import] Lỗi xử lý link '{link}': {ex.Message}");
                        failed++;

                        string fallbackTitle = "Fallback - Truyenqq - " + link.Split('/').Last();
                        Dispatcher.Invoke(() =>
                        {
                            _scrapedItems.Add(new GalleryItem
                            {
                                Link = link,
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
                TruyenqqLog($"[Import] Nhập hoàn tất! Thành công: {imported}, Lỗi/Fallback: {failed}.");
                lblStatus.Text = $"Import completed. Success: {imported}, Failed: {failed}.";
                
                MessageBox.Show($"Đã nhập thành công {total} đường dẫn!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                btnTruyenqqScrape.IsEnabled = true;
                btnTruyenqqFetchInfo.IsEnabled = true;
                if (btnStartDownload != null) btnStartDownload.IsEnabled = true;
                progressBar.Value = 100;
            }
        }

        private async Task<byte[]> GetTruyenqqByteArrayWithRefererAsync(string url, string referer, CancellationToken token)
        {
            int delayMs = 600;
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
                            response.EnsureSuccessStatusCode();
                            return await response.Content.ReadAsByteArrayAsync();
                        }
                    }
                }
                catch (HttpRequestException ex) when (attempt < 4)
                {
                    Log($"[truyenqq] Thử tải lại ảnh do lỗi mạng: {ex.Message}. Chờ {delayMs}ms.");
                    await Task.Delay(delayMs, token);
                    delayMs = Math.Min(delayMs * 2, 6000);
                }
                catch (TaskCanceledException) when (!token.IsCancellationRequested && attempt < 4)
                {
                    Log($"[truyenqq] Thử tải lại ảnh do timeout. Chờ {delayMs}ms.");
                    await Task.Delay(delayMs, token);
                    delayMs = Math.Min(delayMs * 2, 6000);
                }
            }

            throw new Exception($"Không thể tải ảnh truyenqq: {url}");
        }

        private string GetSafeChapterHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;

            string[] commentIndicators = new[]
            {
                "class=\"comment-box\"",
                "class=\"comment-wrapper\"",
                "class=\"comment-holder\"",
                "id=\"comment\"",
                "id=\"truyenqq-comment\"",
                "class=\"fb-comments\"",
                "class=\"list-comment\"",
                "class=\"list_comment\"",
                "id=\"disqus_thread\"",
                "class=\"comments\"",
                "class=\"comment\"",
                "id=\"comments\"",
                "<!-- Bình luận -->",
                "<!-- Bình luận"
            };

            int minIndex = html.Length;
            foreach (var indicator in commentIndicators)
            {
                int index = html.IndexOf(indicator, StringComparison.OrdinalIgnoreCase);
                if (index != -1 && index < minIndex)
                {
                    minIndex = index;
                }
            }

            if (minIndex < html.Length)
            {
                return html.Substring(0, minIndex);
            }
            return html;
        }

        private async Task DownloadTruyenqqGalleryAsync(GalleryItem item, string rootFolder, CancellationToken token, GalleryItem queueItem = null, HashSet<int> chapterFilter = null)
        {
            string cleanLink = item.Link.TrimEnd('/');
            string activeDomain = ExtractTruyenqqBaseUrl(cleanLink);

            // Determine if parent link or child link
            // Parent links contain /truyen-tranh/{slug}
            // Child link contains /truyen-tranh/{slug}-{chapterSuffix}
            var uri = new Uri(cleanLink);
            var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length >= 2 && segments[0].Equals("truyen-tranh", StringComparison.OrdinalIgnoreCase))
            {
                string slug = segments[1];
                bool captchaOk = await SolveTruyenqqCaptchaIfNeededAsync(cleanLink);
                if (!captchaOk)
                {
                    throw new Exception("Không thể vượt qua Cloudflare captcha.");
                }
                string html = await _httpClient.GetStringAsync(cleanLink);

                // Update manga title from <title> tag if available
                var titleMatch = Regex.Match(html, @"<title>\s*(.*?)\s*</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (titleMatch.Success)
                {
                    string rawTitle = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
                    string[] commonSuffixes = { " - TruyệnQQ", " - TruyenQQ", " | TruyệnQQ", " | TruyenQQ" };
                    foreach (var suffix in commonSuffixes)
                    {
                        if (rawTitle.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        {
                            rawTitle = rawTitle.Substring(0, rawTitle.Length - suffix.Length).Trim();
                        }
                    }
                    item.Name = FormatGalleryTitle(rawTitle);
                }

                // Parent path (absolute path of the manga info page)
                string parentPath = uri.AbsolutePath.TrimEnd('/');
                string escapedPath = Regex.Escape(parentPath);

                // Scrape child links: href=".../truyen-tranh/slug-{suffix}"
                string pattern = @"href=[""'](?<link>[^""']*?" + escapedPath + @"-(?<suffix>[^""'\s?#]+))[""']";
                var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);

                var chapterLinks = new System.Collections.Generic.List<string>();
                var seenChapters = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (Match m in matches)
                {
                    string link = m.Groups["link"].Value.Trim();
                    if (!link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                        !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        link = activeDomain + (link.StartsWith("/") ? "" : "/") + link;
                    }
                    
                    link = link.TrimEnd('/');

                    if (seenChapters.Add(link))
                    {
                        chapterLinks.Add(link);
                    }
                }

                if (chapterLinks.Count == 0)
                {
                    // Check if it's actually a direct chapter page (was matched as details page but has no chapters inside)
                    // Some chapters might have similar pattern, but if no sub-chapters are found, let's treat it as a direct chapter page download
                    Log($"[truyenqq] Không tìm thấy chương nào trong '{item.Name}'. Thử tải trực tiếp trang này như một chapter...");
                    await DownloadTruyenqqChapterAsync(item, rootFolder, token, queueItem, isParentQueue: false);
                    return;
                }

                // Sort chapters in ascending order so that oldest chapters (like Chap 1) are downloaded first.
                chapterLinks = chapterLinks.OrderBy(ParseChapterNumber).ToList();

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
                        Log($"[truyenqq] Không có chương nào trùng khớp với bộ lọc đã chọn trong tổng số {totalFoundChapters} chương của '{item.Name}'.");
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

                Log($"[truyenqq] Phát hiện {chapterLinks.Count} chương cho truyện '{item.Name}'. Bắt đầu tải lần lượt...");

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
                    Log($"[truyenqq] Đang tải chương {idx + 1}/{chapterLinks.Count}: {chapLink}");

                    var chapItem = new GalleryItem { Link = chapLink, Name = item.Name };
                    await DownloadTruyenqqChapterAsync(chapItem, rootFolder, token, queueItem, isParentQueue: true);

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
            else
            {
                // Direct Chapter page
                await DownloadTruyenqqChapterAsync(item, rootFolder, token, queueItem, isParentQueue: false);
            }
        }

        private async Task DownloadTruyenqqChapterAsync(GalleryItem item, string rootFolder, CancellationToken token, GalleryItem queueItem = null, bool isParentQueue = false)
        {
            bool captchaOk = await SolveTruyenqqCaptchaIfNeededAsync(item.Link);
            if (!captchaOk)
            {
                throw new Exception("Không thể vượt qua Cloudflare captcha.");
            }
            string html = await _httpClient.GetStringAsync(item.Link);

            string mangaTitle = item.Name;
            string chapterTitle = "Chương 1";

            // Try to extract clean titles from page
            var titleMatch = Regex.Match(html, @"<title>\s*(.*?)\s*</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (titleMatch.Success)
            {
                string rawTitle = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
                string[] commonSuffixes = { " - TruyệnQQ", " - TruyenQQ", " | TruyệnQQ", " | TruyenQQ" };
                foreach (var suffix in commonSuffixes)
                {
                    if (rawTitle.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        rawTitle = rawTitle.Substring(0, rawTitle.Length - suffix.Length).Trim();
                    }
                }

                // TruyenQQ formats title: "Tên Truyện - Tên Chương"
                string[] parts = rawTitle.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    mangaTitle = parts[0].Trim();
                    chapterTitle = parts[1].Trim();
                }
                else if (parts.Length == 1)
                {
                    chapterTitle = parts[0].Trim();
                }
            }

            // Clean Manga Title: remove "chương mới nhất \d+" suffix
            string cleanManga = mangaTitle;
            cleanManga = Regex.Replace(cleanManga, @"\s+(?:chương|Chương)\s+mới\s+nhất\s+\d+.*", "", RegexOptions.IgnoreCase);
            cleanManga = cleanManga.Trim();
            if (string.IsNullOrEmpty(cleanManga))
            {
                cleanManga = "Unknown Manga";
            }

            // Clean Chapter Title: extract the exact chapter string (e.g. chap 2)
            string cleanChapter = chapterTitle;
            var chapMatch = Regex.Match(chapterTitle, @"(chap|chương|chapter)\s*(?<num>\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (chapMatch.Success)
            {
                string type = chapMatch.Groups[1].Value.ToLower();
                if (type == "chapter") type = "chap";
                string num = chapMatch.Groups["num"].Value;
                cleanChapter = $"{type} {num}";
            }
            else
            {
                cleanChapter = Regex.Replace(cleanChapter, @"\s+Tiếng\s+Việt\s+TruyenQQ.*", "", RegexOptions.IgnoreCase);
                cleanChapter = cleanChapter.Trim();
            }

            string safeManga = GetSafePathName(cleanManga);
            string safeChapter = GetSafePathName(cleanChapter);
            
            // Save inside "truyenqq" root directory
            string targetFolder = Path.Combine(rootFolder, "truyenqq", $"{safeManga}-{safeChapter}");
            string tempFolder = Path.Combine(rootFolder, "truyenqq", $".tmp_{safeManga}_{safeChapter}_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempFolder);

            // Isolate images using Safe Chapter HTML (no comments section)
            string safeHtml = GetSafeChapterHtml(html);

            // Isolate reading container contents to avoid ads/logo/header images
            int startIndex = -1;
            string[] containerMarkers = new[]
            {
                "class=\"story-see-content\"",
                "class=\"chapter_content\"",
                "class=\"chapter-content\"",
                "class=\"reading-detail\"",
                "id=\"chapter_content\""
            };

            foreach (var marker in containerMarkers)
            {
                int index = safeHtml.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    startIndex = index;
                    break;
                }
            }

            string contentArea = safeHtml;
            if (startIndex != -1)
            {
                contentArea = safeHtml.Substring(startIndex);
            }

            // Extract all image URLs from isolated reading area
            var imageUrls = new System.Collections.Generic.List<string>();
            var imgTags = Regex.Matches(contentArea, @"<img\s+[^>]*>", RegexOptions.IgnoreCase);
            
            foreach (Match imgTag in imgTags)
            {
                string tag = imgTag.Value;
                string imgUrl = null;

                var dataOriginalMatch = Regex.Match(tag, @"data-original=[""'](?<url>[^""']+)[""']", RegexOptions.IgnoreCase);
                if (dataOriginalMatch.Success)
                {
                    imgUrl = dataOriginalMatch.Groups["url"].Value;
                }
                else
                {
                    var dataSrcMatch = Regex.Match(tag, @"data-src=[""'](?<url>[^""']+)[""']", RegexOptions.IgnoreCase);
                    if (dataSrcMatch.Success)
                    {
                        imgUrl = dataSrcMatch.Groups["url"].Value;
                    }
                    else
                    {
                        var srcMatch = Regex.Match(tag, @"src=[""'](?<url>[^""']+)[""']", RegexOptions.IgnoreCase);
                        if (srcMatch.Success)
                        {
                            imgUrl = srcMatch.Groups["url"].Value;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(imgUrl))
                {
                    imgUrl = imgUrl.Trim();
                    
                    // Filter out header, menu, ads, UI and avatar images
                    if (imgUrl.EndsWith("logo.png", StringComparison.OrdinalIgnoreCase) ||
                        imgUrl.EndsWith("avatar.jpg", StringComparison.OrdinalIgnoreCase) ||
                        imgUrl.Contains("avatar") ||
                        imgUrl.Contains("loading") ||
                        imgUrl.Contains("spacer.gif") ||
                        imgUrl.Contains("transparent.gif") ||
                        imgUrl.Contains("/images/logo") ||
                        imgUrl.Contains("/images/favicon") ||
                        imgUrl.Contains("facebook.com") ||
                        imgUrl.Contains("banner") ||
                        imgUrl.Contains("advertisement"))
                    {
                        continue;
                    }

                    // Normalize relative links
                    if (!imgUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                        !imgUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        string activeDomain = ExtractTruyenqqBaseUrl(item.Link);
                        imgUrl = activeDomain + (imgUrl.StartsWith("/") ? "" : "/") + imgUrl;
                    }

                    if (!imageUrls.Contains(imgUrl))
                    {
                        imageUrls.Add(imgUrl);
                    }
                }
            }

            if (imageUrls.Count == 0)
            {
                throw new Exception($"Không thể tìm thấy hình ảnh nào của chương truyện '{chapterTitle}' để tải xuống.");
            }

            // Connection count settings
            int maxThreads = 4;
            Dispatcher.Invoke(() =>
            {
                if (cmbConnections.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem && int.TryParse(selectedItem.Content.ToString(), out int val))
                {
                    maxThreads = val;
                }
            });

            Log($"[truyenqq] Bắt đầu tải {imageUrls.Count} trang của chapter '{chapterTitle}' với {maxThreads} kết nối song song...");

            if (queueItem != null && !isParentQueue)
            {
                Dispatcher.Invoke(() =>
                {
                    queueItem.TotalChapters = imageUrls.Count;
                    queueItem.CompletedChapters = 0;
                });
            }

            using (var semaphore = new DynamicSemaphore(maxThreads, GetCurrentConnectionLimit))
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
                        while (_isDownloadPaused || (queueItem != null && queueItem.IsPaused))
                        {
                            token.ThrowIfCancellationRequested();
                            if (queueItem != null && queueItem.IsStopped) throw new OperationCanceledException();
                            await Task.Delay(200, token);
                        }
                        token.ThrowIfCancellationRequested();

                        await semaphore.WaitAsync(token);
                        try
                        {
                            while (_isDownloadPaused || (queueItem != null && queueItem.IsPaused))
                            {
                                token.ThrowIfCancellationRequested();
                                if (queueItem != null && queueItem.IsStopped) throw new OperationCanceledException();
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
                                                queueItem.CurrentProcess = $"{cleanChapter} (trang {completedPages}/{imageUrls.Count})";
                                            }
                                            else
                                            {
                                                queueItem.CompletedChapters = completedPages;
                                                queueItem.CurrentProcess = $"Trang {completedPages}/{imageUrls.Count}";
                                            }
                                        });
                                    }
                                }
                                return;
                            }

                            try
                            {
                                await DownloadUrlToFileWithRefererAsync(imgUrl, item.Link, localFilePath, token, isTruyenqq: true);
                            }
                            catch (Exception ex)
                            {
                                lock (lockObj)
                                {
                                    if (queueItem != null)
                                    {
                                        queueItem.AddError(cleanChapter, index + 1, ex.Message, imgUrl);
                                    }
                                    Log($"[truyenqq] Lỗi tải trang {index + 1} của chapter '{cleanChapter}': {ex.Message}");
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
                                            queueItem.CurrentProcess = $"{cleanChapter} (trang {completedPages}/{imageUrls.Count})";
                                        }
                                        else
                                        {
                                            queueItem.CompletedChapters = completedPages;
                                            queueItem.CurrentProcess = $"Trang {completedPages}/{imageUrls.Count}";
                                        }
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
                Log($"[truyenqq] Tải xong chapter '{cleanChapter}' của truyện '{cleanManga}'.");
            }
            catch (Exception ex)
            {
                Log($"[truyenqq] [Lỗi] Không thể di chuyển thư mục tạm: {ex.Message}");
            }

            // Check for missing files
            ValidateDownloadedFiles(targetFolder, imageUrls.Count, queueItem, cleanChapter);
        }

        private double ParseChapterNumber(string url)
        {
            if (string.IsNullOrEmpty(url)) return 0.0;
            var match = Regex.Match(url, @"(?:chap|chapter|chuong|trang)[^\d]*(?<num>\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (match.Success && double.TryParse(match.Groups["num"].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num))
            {
                return num;
            }
            return 0.0;
        }
    }
}
