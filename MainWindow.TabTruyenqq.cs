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
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string logLine = $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";
                bool isError = IsErrorMessage(message);
                AppendLogLine(txtTruyenqqLog, logLine, isError);
                if (chkAutoScrollTruyenqqLog?.IsChecked == true)
                    ScrollTextBoxToEnd(txtTruyenqqLog);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        internal async Task<bool> CheckIfTruyenqqBlockedAsync(string testUrl)
        {
            testUrl = ResolveTruyenqqRequestUrl(testUrl);
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
                                html.Contains("thá»±c hiá»‡n xÃ¡c minh báº£o máº­t") ||
                                html.Contains("xÃ¡c minh báº¡n khÃ´ng pháº£i lÃ  bot"))
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

        private string ResolveTruyenqqRequestUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return url;
            }

            string normalized = NormalizeTruyenqqUrl(url);
            if (string.IsNullOrWhiteSpace(_truyenqqPreferredBaseUrl))
            {
                return normalized;
            }

            if (!Uri.TryCreate(_truyenqqPreferredBaseUrl, UriKind.Absolute, out Uri preferredBase))
            {
                return normalized;
            }

            var uri = new Uri(normalized);
            var builder = new UriBuilder(uri)
            {
                Scheme = preferredBase.Scheme,
                Host = preferredBase.Host,
                Port = preferredBase.Port
            };

            return builder.Uri.AbsoluteUri.TrimEnd('/');
        }
        internal async Task<bool> SolveTruyenqqCaptchaIfNeededAsync(string testUrl)
        {
            testUrl = ResolveTruyenqqRequestUrl(testUrl);
            bool isBlocked = await CheckIfTruyenqqBlockedAsync(testUrl);
            if (!isBlocked && !string.IsNullOrWhiteSpace(_truyenqqPreferredBaseUrl))
            {
                return true; // Not blocked
            }

            if (!isBlocked && string.IsNullOrWhiteSpace(_truyenqqPreferredBaseUrl))
            {
                // First-time setup: force one captcha pass so we can learn the active domain.
                isBlocked = true;
            }

            if (_isCaptchaWindowActive)
            {
                while (_isCaptchaWindowActive)
                {
                    await Task.Delay(500);
                }
                isBlocked = await CheckIfTruyenqqBlockedAsync(testUrl);
                if (!isBlocked)
                {
                    return true;
                }
            }

            await _captchaSemaphore.WaitAsync();
            try
            {
                // Re-check after acquiring lock
                isBlocked = await CheckIfTruyenqqBlockedAsync(testUrl);
                if (!isBlocked)
                {
                    return true;
                }

                _isCaptchaWindowActive = true;
                _isDownloadPaused = true;
                TruyenqqLog("PhÃ¡t hiá»‡n thá»­ thÃ¡ch Cloudflare / Captcha. Táº¡m dá»«ng táº£i vÃ  Ä‘ang má»Ÿ trÃ¬nh duyá»‡t giáº£i tá»± Ä‘á»™ng...");

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
                            var originalUri = new Uri(testUrl);
                            var resolvedUri = captchaWin.ResolvedUri ?? originalUri;
                            _truyenqqPreferredBaseUrl = $"{resolvedUri.Scheme}://{resolvedUri.Host}";

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
                    TruyenqqLog("Giáº£i captcha thÃ nh cÃ´ng. Tiáº¿p tá»¥c táº£i...");
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

        private bool IsTruyenqqUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            try
            {
                var uri = new Uri(url);
                return IsTruyenqqHost(uri.Host);
            }
            catch
            {
                return Regex.IsMatch(url, @"(?:https?://)?(?:www\.)?truyenqq[a-zA-Z0-9-]*\.com", RegexOptions.IgnoreCase);
            }
        }

        private bool IsTruyenqqHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            return Regex.IsMatch(host, @"^(?:www\.)?truyenqq[a-zA-Z0-9-]*\.com$", RegexOptions.IgnoreCase);
        }

        private string NormalizeTruyenqqUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return "";
            }

            string normalized = url.Trim();
            if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "https://" + normalized;
            }

            if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri) || !IsTruyenqqHost(uri.Host))
            {
                throw new ArgumentException("URL pháº£i thuá»™c domain truyenqq*.com.");
            }

            var builder = new UriBuilder(uri)
            {
                Fragment = ""
            };

            if (!string.IsNullOrWhiteSpace(_truyenqqPreferredBaseUrl) &&
                Uri.TryCreate(_truyenqqPreferredBaseUrl, UriKind.Absolute, out Uri preferredBase))
            {
                builder.Scheme = preferredBase.Scheme;
                builder.Host = preferredBase.Host;
                builder.Port = preferredBase.Port;
            }

            string path = builder.Path ?? "/";
            path = Regex.Replace(path, @"/{2,}", "/");
            if (path.Length > 1)
            {
                path = path.TrimEnd('/');
            }

            builder.Path = path;
            return builder.Uri.AbsoluteUri.TrimEnd('/');
        }

        private string StripTruyenqqBrandSuffix(string rawTitle)
        {
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return rawTitle;
            }

            return Regex.Replace(
                rawTitle.Trim(),
                @"\s*[\-\|]\s*truy[eá»‡]nqq[a-zA-Z0-9-]*.*$",
                "",
                RegexOptions.IgnoreCase).Trim();
        }

        private string ExtractTruyenqqBaseUrl(string url)
        {
            if (!string.IsNullOrWhiteSpace(_truyenqqPreferredBaseUrl))
            {
                return _truyenqqPreferredBaseUrl.TrimEnd('/');
            }

            try
            {
                var uri = new Uri(NormalizeTruyenqqUrl(url));
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
            return "https://truyenqqko.com"; // Default fallback
        }

        private string GetTruyenqqPageUrl(string baseUrl, int page)
        {
            baseUrl = baseUrl.Trim();
            if (page == 1) return baseUrl;

            try
            {
                var uri = new Uri(baseUrl);
                string path = uri.AbsolutePath;
                path = Regex.Replace(path, @"/trang-\d+/?", "", RegexOptions.IgnoreCase);
                path = path.TrimEnd('/');
                
                string newPath = $"{path}/trang-{page}";
                var builder = new UriBuilder(uri)
                {
                    Path = newPath
                };
                return builder.Uri.ToString();
            }
            catch
            {
                string cleanUrl = baseUrl;
                cleanUrl = Regex.Replace(cleanUrl, @"/trang-\d+/?", "", RegexOptions.IgnoreCase);
                cleanUrl = cleanUrl.TrimEnd('/');
                return $"{cleanUrl}/trang-{page}";
            }
        }

        private async void BtnTruyenqqFetchInfo_Click(object sender, RoutedEventArgs e)
        {
            string rawUrl = txtTruyenqqTagUrl.Text.Trim();
            if (string.IsNullOrEmpty(rawUrl))
            {
                MessageBox.Show("Vui lÃ²ng nháº­p URL há»£p lá»‡.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            btnTruyenqqFetchInfo.IsEnabled = false;
            lblStatus.Text = "Äang phÃ¢n tÃ­ch trang truyenqq...";
            progressBar.IsIndeterminate = true;


            try
            {
                string normalizedUrl = ResolveTruyenqqRequestUrl(rawUrl);
                txtTruyenqqTagUrl.Text = normalizedUrl;
                TruyenqqLog($"Đang phân tích URL: {normalizedUrl}");
                bool captchaOk = await SolveTruyenqqCaptchaIfNeededAsync(normalizedUrl);
                if (!captchaOk)
                {
                    TruyenqqLog("KhÃ´ng thá»ƒ bypass Cloudflare. Há»§y phÃ¢n tÃ­ch.");
                    lblStatus.Text = "Analysis failed (Cloudflare).";
                    return;
                }

                normalizedUrl = ResolveTruyenqqRequestUrl(normalizedUrl);
                txtTruyenqqTagUrl.Text = normalizedUrl;

                // Fetch targeted page HTML
                string html = await _httpClient.GetStringAsync(normalizedUrl);
                
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
                
                TruyenqqLog($"PhÃ¢n tÃ­ch hoÃ n táº¥t. PhÃ¡t hiá»‡n tá»‘i Ä‘a {maxPage} trang.");
                lblStatus.Text = $"Analysis complete. Found {maxPage} pages.";
            }
            catch (Exception ex)
            {
                TruyenqqLog($"Lá»—i khi phÃ¢n tÃ­ch: {ex.Message}");
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
            string rawBaseUrl = txtTruyenqqTagUrl.Text.Trim();
            if (string.IsNullOrEmpty(rawBaseUrl))
            {
                MessageBox.Show("Vui lÃ²ng nháº­p URL há»£p lá»‡.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(txtTruyenqqPageFrom.Text, out int pageFrom) || pageFrom < 1)
            {
                MessageBox.Show("Trang báº¯t Ä‘áº§u khÃ´ng há»£p lá»‡.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(txtTruyenqqPageTo.Text, out int pageTo) || pageTo < pageFrom)
            {
                MessageBox.Show("Trang káº¿t thÃºc khÃ´ng há»£p lá»‡.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            lblStatus.Text = "Äang cÃ o truyenqq...";
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

            TruyenqqLog($"Báº¯t Ä‘áº§u cÃ o tá»« trang {pageFrom} Ä‘áº¿n {pageTo}...");

            try
            {
                string baseUrl = NormalizeTruyenqqUrl(rawBaseUrl);
                txtTruyenqqTagUrl.Text = baseUrl;
                int totalPages = pageTo - pageFrom + 1;
                int pagesProcessed = 0;

                for (int page = pageFrom; page <= pageTo; page++)
                {
                    token.ThrowIfCancellationRequested();

                    string pageUrl = GetTruyenqqPageUrl(baseUrl, page);
                    TruyenqqLog($"Äang táº£i trang {page}: {pageUrl}");

                    bool captchaOk = await SolveTruyenqqCaptchaIfNeededAsync(pageUrl);
                    if (!captchaOk)
                    {
                        TruyenqqLog($"KhÃ´ng thá»ƒ bypass Cloudflare cho trang {page}. Bá» qua trang nÃ y.");
                        continue;
                    }

                    pageUrl = ResolveTruyenqqRequestUrl(pageUrl);
                    string html = await _httpClient.GetStringAsync(pageUrl);
                    
                    // Match all <a> tags containing /truyen-tranh/ links, parent or child (chapter) links
                    var viewMatches = Regex.Matches(html, @"<a\s+[^>]*?href=[""'](?<link>[^""']*?/truyen-tranh/[^""']+)[""'][^>]*>(?<content>[\s\S]*?)<\/a>", RegexOptions.IgnoreCase);
                    
                    int pageCount = 0;
                    
                    // Parse parent links first and find their latest chap from adjacent child links
                    var pageParents = new System.Collections.Generic.List<GalleryItem>();
                    var parentLatestChaps = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    // First pass: extract all parents and trace adjacent chapter mappings
                    string lastParentUrl = null;
                    foreach (Match match in viewMatches)
                    {
                        string relativeLink = match.Groups["link"].Value.Trim();
                        string fullLink = relativeLink;
                        if (!fullLink.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                            !fullLink.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            string activeDomain = ExtractTruyenqqBaseUrl(pageUrl);
                            fullLink = activeDomain + (fullLink.StartsWith("/") ? "" : "/") + fullLink;
                        }
                        fullLink = fullLink.TrimEnd('/');

                        if (Regex.IsMatch(relativeLink, @"-chap(?:-|\b)", RegexOptions.IgnoreCase))
                        {
                            // It's a chapter link. If we have a preceding parent, associate the chapter text.
                            if (lastParentUrl != null)
                            {
                                string textVal = Regex.Replace(match.Groups["content"].Value, @"<[^>]+>", "").Trim();
                                textVal = WebUtility.HtmlDecode(textVal);
                                if (!string.IsNullOrEmpty(textVal))
                                {
                                    if (!parentLatestChaps.ContainsKey(lastParentUrl))
                                    {
                                        parentLatestChaps[lastParentUrl] = textVal;
                                    }
                                    else
                                    {
                                        double existingNum = ParseChapterNumberFromText(parentLatestChaps[lastParentUrl]);
                                        double currentNum = ParseChapterNumberFromText(textVal);
                                        if (currentNum > existingNum)
                                        {
                                            parentLatestChaps[lastParentUrl] = textVal;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // It's a parent link
                            lastParentUrl = fullLink;
                            
                            string rawContent = match.Groups["content"].Value;
                            string title = Regex.Replace(rawContent, @"<[^>]+>", "").Trim();
                            title = WebUtility.HtmlDecode(title);

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

                            var existingItem = _scrapedItems.FirstOrDefault(item => item.Link.Equals(fullLink, StringComparison.OrdinalIgnoreCase));
                            if (existingItem == null && !pageParents.Any(p => p.Link.Equals(fullLink, StringComparison.OrdinalIgnoreCase)))
                            {
                                pageParents.Add(new GalleryItem
                                {
                                    Link = fullLink,
                                    Name = FormatGalleryTitle(title),
                                    OriginalIndex = _scrapedItems.Count + pageParents.Count,
                                    IsChecked = false
                                });
                            }
                        }
                    }

                    // Apply the latest chap numbers and add to list
                    foreach (var item in pageParents)
                    {
                        if (parentLatestChaps.TryGetValue(item.Link, out string latestChap))
                        {
                            item.LinkCount = latestChap;
                        }
                        _scrapedItems.Add(item);
                        pageCount++;
                    }

                    pagesProcessed++;
                    double progressPct = ((double)pagesProcessed / totalPages) * 100;
                    progressBar.Value = progressPct;
                    lblStatus.Text = $"Searching page {page}/{pageTo} ({progressPct:0}%)";
                    lblLinkCount.Text = _scrapedItems.Count.ToString();
                    TruyenqqLog($"Trang {page} hoÃ n táº¥t. TÃ¬m tháº¥y {pageCount} liÃªn káº¿t má»›i.");
                }

                // Sort items deterministically
                var sortedItems = _scrapedItems
                    .OrderBy(item => item.Name)
                    .ThenBy(item => item.OriginalIndex)
                    .ToList();
                _scrapedItems.Clear();
                foreach (var item in sortedItems) _scrapedItems.Add(item);

                RecalculateDuplicates();
                TruyenqqLog($"CÃ o dá»¯ liá»‡u hoÃ n táº¥t! Tá»•ng cá»™ng thu tháº­p Ä‘Æ°á»£c {_scrapedItems.Count} liÃªn káº¿t Ä‘á»™c nháº¥t.");
                lblStatus.Text = "Crawling completed successfully.";
                lblLinkCount.Text = _scrapedItems.Count.ToString();
            }
            catch (OperationCanceledException)
            {
                TruyenqqLog("ÄÃ£ há»§y cÃ o theo yÃªu cáº§u ngÆ°á»i dÃ¹ng.");
                lblStatus.Text = "Crawling cancelled.";
            }
            catch (Exception ex)
            {
                TruyenqqLog($"Lá»—i nghiÃªm trá»ng khi cÃ o: {ex.Message}");
                lblStatus.Text = "Crawling failed.";
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                btnTruyenqqScrape.Content = "GET LINK";
                btnTruyenqqScrape.IsEnabled = true;
                if (btnTruyenqqCrawlMore != null)
                {
                    btnTruyenqqCrawlMore.Content = "GET MORE";
                    btnTruyenqqCrawlMore.IsEnabled = true;
                }
                btnTruyenqqFetchInfo.IsEnabled = true;
            }
        }

        private void BtnTruyenqqPasteDirect_Click(object sender, RoutedEventArgs e)
        {
            var win = new DirectDownloadWindow(isNhentai: false);
            win.Owner = this;
            win.OnImport = async (links) =>
            {
                if (links != null && links.Any())
                {
                    await ImportTruyenqqDirectLinksAsync(links);
                }
            };
            win.Show();
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

            TruyenqqLog($"[Import] Báº¯t Ä‘áº§u phÃ¢n tÃ­ch vÃ  nháº­p {total} liÃªn káº¿t trá»±c tiáº¿p...");
            lblStatus.Text = $"Importing 0/{total} links...";

            try
            {
                for (int i = 0; i < total; i++)
                {
                    string link = links[i].Trim();
                    
                    if (!link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                        !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        link = NormalizeTruyenqqUrl(link);
                    }

                    lblStatus.Text = $"[{i + 1}/{total}] Äang phÃ¢n tÃ­ch: {link}";

                    try
                    {
                        link = ResolveTruyenqqRequestUrl(link);
                        lblStatus.Text = $"[{i + 1}/{total}] Đang phân tích: {link}";
                        if (_scrapedItems.Any(item => item.Link.Equals(link, StringComparison.OrdinalIgnoreCase)))
                        {
                            TruyenqqLog($"[Import] Bá» qua liÃªn káº¿t Ä‘Ã£ tá»“n táº¡i: {link}");
                            imported++;
                            continue;
                        }

                        // Try to scrape title from target page
                        bool captchaOk = await SolveTruyenqqCaptchaIfNeededAsync(link);
                        if (!captchaOk)
                        {
                            TruyenqqLog($"[Import] KhÃ´ng thá»ƒ bypass Cloudflare cho: {link}");
                            failed++;
                            continue;
                        }
                        link = ResolveTruyenqqRequestUrl(link);
                        string html = await _httpClient.GetStringAsync(link);
                        var titleMatch = Regex.Match(html, @"<title>\s*(.*?)\s*</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        string title = "Manga - " + link.Split('/').Last();
                        string latestChapText = "";
                        if (titleMatch.Success)
                        {
                            string rawTitle = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
                            ParseMangaNameAndLatestChap(rawTitle, out string mangaName, out latestChapText);
                            title = mangaName;
                        }

                        Dispatcher.Invoke(() =>
                        {
                            _scrapedItems.Add(new GalleryItem
                            {
                                Link = link,
                                Name = FormatGalleryTitle(title),
                                OriginalIndex = _scrapedItems.Count,
                                IsChecked = true,
                                HasNoChapters = false,
                                LinkCount = latestChapText
                            });
                        });

                        TruyenqqLog($"[Import {i + 1}/{total}] ThÃ nh cÃ´ng: {title}");
                        imported++;
                    }
                    catch (Exception ex)
                    {
                        TruyenqqLog($"[Import] Lá»—i xá»­ lÃ½ link '{link}': {ex.Message}");
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
                    lblLinkCount.Text = _scrapedItems.Count.ToString(); // real-time update
                }

                RecalculateDuplicates();
                lblLinkCount.Text = _scrapedItems.Count.ToString();
                TruyenqqLog($"[Import] Nháº­p hoÃ n táº¥t! ThÃ nh cÃ´ng: {imported}, Lá»—i/Fallback: {failed}.");
                lblStatus.Text = $"Import completed. Success: {imported}, Failed: {failed}.";
                
                MessageBox.Show($"ÄÃ£ nháº­p thÃ nh cÃ´ng {total} Ä‘Æ°á»ng dáº«n!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    Log($"[truyenqq] Thá»­ táº£i láº¡i áº£nh do lá»—i máº¡ng: {ex.Message}. Chá» {delayMs}ms.");
                    await Task.Delay(delayMs, token);
                    delayMs = Math.Min(delayMs * 2, 6000);
                }
                catch (TaskCanceledException) when (!token.IsCancellationRequested && attempt < 4)
                {
                    Log($"[truyenqq] Thá»­ táº£i láº¡i áº£nh do timeout. Chá» {delayMs}ms.");
                    await Task.Delay(delayMs, token);
                    delayMs = Math.Min(delayMs * 2, 6000);
                }
            }

            throw new Exception($"KhÃ´ng thá»ƒ táº£i áº£nh truyenqq: {url}");
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
                "<!-- BÃ¬nh luáº­n -->",
                "<!-- BÃ¬nh luáº­n"
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

        private async Task DownloadTruyenqqGalleryAsync(GalleryItem item, string rootFolder, CancellationToken token, GalleryItem queueItem = null, ChapterFilter chapterFilter = null)
        {
            string cleanLink = ResolveTruyenqqRequestUrl(item.Link);
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
                    throw new Exception("KhÃ´ng thá»ƒ vÆ°á»£t qua Cloudflare captcha.");
                }
                cleanLink = ResolveTruyenqqRequestUrl(cleanLink);
                string html = await _httpClient.GetStringAsync(cleanLink);

                // Update manga title from <title> tag if available
                var titleMatch = Regex.Match(html, @"<title>\s*(.*?)\s*</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (titleMatch.Success)
                {
                    string rawTitle = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
                    string[] commonSuffixes = { " - Truyá»‡nQQ", " - TruyenQQ", " | Truyá»‡nQQ", " | TruyenQQ" };
                    foreach (var suffix in commonSuffixes)
                    {
                        if (rawTitle.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        {
                            rawTitle = rawTitle.Substring(0, rawTitle.Length - suffix.Length).Trim();
                        }
                    }
                    rawTitle = StripTruyenqqBrandSuffix(rawTitle);
                    item.Name = FormatGalleryTitle(rawTitle);
                }

                // Parent path (absolute path of the manga info page)
                string parentPath = uri.AbsolutePath.TrimEnd('/');
                string escapedPath = Regex.Escape(parentPath);

                // Scrape child links: href=".../truyen-tranh/slug-{suffix}"
                // IMPORTANT: only match links where the suffix starts with "chap"
                // to avoid matching other non-chapter links that share the same slug prefix.
                string pattern = @"href=[""'](?<link>[^""']*?" + escapedPath + @"-chap(?:[^""'\s?#]*)?)[""']";
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
                    Log($"[truyenqq] KhÃ´ng tÃ¬m tháº¥y chÆ°Æ¡ng nÃ o trong '{item.Name}'. Thá»­ táº£i trá»±c tiáº¿p trang nÃ y nhÆ° má»™t chapter...");
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
                        if (chapterFilter.IsMatch(chapNum))
                        {
                            filtered.Add(link);
                        }
                    }
                    chapterLinks = filtered;
                    if (chapterLinks.Count == 0)
                    {
                        Log($"[truyenqq] KhÃ´ng cÃ³ chÆ°Æ¡ng nÃ o trÃ¹ng khá»›p vá»›i bá»™ lá»c Ä‘Ã£ chá»n trong tá»•ng sá»‘ {totalFoundChapters} chÆ°Æ¡ng cá»§a '{item.Name}'.");
                        if (queueItem != null)
                        {
                            Dispatcher.Invoke(() => {
                                queueItem.Status = "Completed";
                                queueItem.CurrentProcess = "KhÃ´ng cÃ³ chÆ°Æ¡ng trÃ¹ng khá»›p bá»™ lá»c";
                            });
                        }
                        return;
                    }
                }

                Log($"[truyenqq] PhÃ¡t hiá»‡n {chapterLinks.Count} chÆ°Æ¡ng cho truyá»‡n '{item.Name}'. Báº¯t Ä‘áº§u táº£i láº§n lÆ°á»£t...");

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
                    Log($"[truyenqq] Äang táº£i chÆ°Æ¡ng {idx + 1}/{chapterLinks.Count}: {chapLink}");

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
                    item.LinkCount = chapterLinks.Count.ToString();
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
            item.Link = ResolveTruyenqqRequestUrl(item.Link);

            bool captchaOk = await SolveTruyenqqCaptchaIfNeededAsync(item.Link);
            if (!captchaOk)
            {
                throw new Exception("KhÃ´ng thá»ƒ vÆ°á»£t qua Cloudflare captcha.");
            }
            item.Link = ResolveTruyenqqRequestUrl(item.Link);
            string html = await _httpClient.GetStringAsync(item.Link);

            string mangaTitle = item.Name;
            string chapterTitle = "ChÆ°Æ¡ng 1";

            // Try to extract clean titles from page
            var titleMatch = Regex.Match(html, @"<title>\s*(.*?)\s*</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (titleMatch.Success)
            {
                string rawTitle = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
                string[] commonSuffixes = { " - Truyá»‡nQQ", " - TruyenQQ", " | Truyá»‡nQQ", " | TruyenQQ" };
                foreach (var suffix in commonSuffixes)
                {
                    if (rawTitle.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        rawTitle = rawTitle.Substring(0, rawTitle.Length - suffix.Length).Trim();
                    }
                }

                // TruyenQQ formats title: "TÃªn Truyá»‡n - TÃªn ChÆ°Æ¡ng"
                // Some manga names have " - " in them so we must find the RIGHTMOST
                // chapter-like part (containing chap/chÆ°Æ¡ng/chapter keyword) as the separator.
                string[] parts = rawTitle.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    // Find the rightmost part that looks like a chapter label
                    int chapPartIdx = -1;
                    for (int i = parts.Length - 1; i >= 1; i--)
                    {
                        if (Regex.IsMatch(parts[i], @"\b(chap|chÆ°Æ¡ng|chapter)\b", RegexOptions.IgnoreCase))
                        {
                            chapPartIdx = i;
                            break;
                        }
                    }
                    if (chapPartIdx > 0)
                    {
                        // Join everything before the chapter part as manga title
                        mangaTitle = string.Join(" - ", parts, 0, chapPartIdx).Trim();
                        chapterTitle = string.Join(" - ", parts, chapPartIdx, parts.Length - chapPartIdx).Trim();
                    }
                    else
                    {
                        // Fallback: last part is chapter
                        mangaTitle = string.Join(" - ", parts, 0, parts.Length - 1).Trim();
                        chapterTitle = parts[parts.Length - 1].Trim();
                    }
                }
                else if (parts.Length == 1)
                {
                    chapterTitle = parts[0].Trim();
                }
            }

            // Clean Manga Title: remove "chÆ°Æ¡ng má»›i nháº¥t \d+" suffix
            string cleanManga = mangaTitle;
            cleanManga = Regex.Replace(cleanManga, @"\s+(?:chÆ°Æ¡ng|ChÆ°Æ¡ng)\s+má»›i\s+nháº¥t\s+\d+.*", "", RegexOptions.IgnoreCase);
            cleanManga = cleanManga.Trim();
            if (string.IsNullOrEmpty(cleanManga))
            {
                cleanManga = "Unknown Manga";
            }

            // Clean Chapter Title: extract the exact chapter string (e.g. chap 2)
            string cleanChapter = chapterTitle;
            var chapMatch = Regex.Match(chapterTitle, @"(chap|chÆ°Æ¡ng|chapter)\s*(?<num>\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (chapMatch.Success)
            {
                string type = chapMatch.Groups[1].Value.ToLower();
                if (type == "chapter") type = "chap";
                string num = chapMatch.Groups["num"].Value;
                cleanChapter = $"{type} {num}";
            }
            else
            {
                cleanChapter = Regex.Replace(cleanChapter, @"\s+Tiáº¿ng\s+Viá»‡t\s+TruyenQQ.*", "", RegexOptions.IgnoreCase);
                cleanChapter = cleanChapter.Trim();
            }

            string safeManga = GetSafePathName(cleanManga);
            string safeChapter = GetSafePathName(cleanChapter);
            
            // Save inside "truyenqq" root directory
            string unmergedPath = Path.Combine(rootFolder, "truyenqq", $"{safeManga}-{safeChapter}");
            string mergedPath = Path.Combine(rootFolder, "truyenqq", safeManga, safeChapter);
            string tempFolder = Path.Combine(rootFolder, "truyenqq", ".tmp", $".tmp_{safeManga}_{safeChapter}_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempFolder);
            RegisterTempFolder(tempFolder);

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
                throw new Exception($"KhÃ´ng thá»ƒ tÃ¬m tháº¥y hÃ¬nh áº£nh nÃ o cá»§a chÆ°Æ¡ng truyá»‡n '{chapterTitle}' Ä‘á»ƒ táº£i xuá»‘ng.");
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

            Log($"[truyenqq] Báº¯t Ä‘áº§u táº£i {imageUrls.Count} trang cá»§a chapter '{chapterTitle}' vá»›i {maxThreads} káº¿t ná»‘i song song...");

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
                            string unmergedFilePath = Path.Combine(unmergedPath, $"{index + 1:D3}{ext}");
                            string mergedFilePath = Path.Combine(mergedPath, $"{index + 1:D3}{ext}");

                            if ((File.Exists(localFilePath) && new FileInfo(localFilePath).Length > 1024) ||
                                (File.Exists(unmergedFilePath) && new FileInfo(unmergedFilePath).Length > 1024) ||
                                (File.Exists(mergedFilePath) && new FileInfo(mergedFilePath).Length > 1024))
                            {
                                lock (lockObj)
                                {
                                    completedPages++;
                                    if (queueItem != null)
                                    {
                                        Dispatcher.BeginInvoke((Action)(() =>
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
                                        }));
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
                                    Log($"[truyenqq] Lá»—i táº£i trang {index + 1} cá»§a chapter '{cleanChapter}': {ex.Message}");
                                }
                            }

                            lock (lockObj)
                            {
                                completedPages++;
                                if (queueItem != null)
                                {
                                    Dispatcher.BeginInvoke((Action)(() =>
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
                                    }));
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
                    string currentTargetFolder = Directory.Exists(mergedPath) ? mergedPath : unmergedPath;
                    if (Directory.Exists(currentTargetFolder))
                    {
                        MergeDirectoryContents(tempFolder, currentTargetFolder);
                    }
                    else
                    {
                        Directory.Move(tempFolder, currentTargetFolder);
                    }
                }

                await AutoMergeChapterFolderAsync(unmergedPath, mergedPath, token);
                Log($"[truyenqq] Táº£i xong chapter '{cleanChapter}' cá»§a truyá»‡n '{cleanManga}'.");
            }
            catch (Exception ex)
            {
                Log($"[truyenqq] [Lá»—i] KhÃ´ng thá»ƒ di chuyá»ƒn thÆ° má»¥c táº¡m: {ex.Message}");
            }
            finally
            {
                UnregisterTempFolder(tempFolder);
            }

            // Check for missing files
            string finalTargetFolder = Directory.Exists(mergedPath) ? mergedPath : unmergedPath;
            ValidateDownloadedFiles(finalTargetFolder, imageUrls.Count, queueItem, cleanChapter);
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

        private static void ParseMangaNameAndLatestChap(string rawTitle, out string mangaName, out string latestChap)
        {
            mangaName = rawTitle;
            latestChap = "";
            mangaName = Regex.Replace(
                mangaName,
                @"\s*[\-\|]\s*truy[eá»‡]nqq[a-zA-Z0-9-]*.*$",
                "",
                RegexOptions.IgnoreCase).Trim();
            string[] commonSuffixes = { " - Truyá»‡nQQ", " - TruyenQQ", " | Truyá»‡nQQ", " | TruyenQQ" };
            foreach (var suffix in commonSuffixes)
            {
                if (mangaName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    mangaName = mangaName.Substring(0, mangaName.Length - suffix.Length).Trim();
            }
            var match = Regex.Match(mangaName, @"^(?<manga>.*?)\s+(?<indicator>chÆ°Æ¡ng má»›i nháº¥t|chap má»›i nháº¥t|chÆ°Æ¡ng|chap|chapter)\s+(?<val>[\d\w\.-]+(?:[\s\S]*?))$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                mangaName = match.Groups["manga"].Value.Trim();
                latestChap = match.Groups["indicator"].Value.Trim() + " " + match.Groups["val"].Value.Trim();
            }
        }

        private double ParseChapterNumberFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0.0;
            var match = Regex.Match(text, @"(?<num>\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (match.Success && double.TryParse(match.Groups["num"].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num))
            {
                return num;
            }
            return 0.0;
        }
    }
}

