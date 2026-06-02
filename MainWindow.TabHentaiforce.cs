using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private string GetPageUrl(string baseUrl, int page)
        {
            baseUrl = baseUrl.Trim();
            if (baseUrl.Contains("?"))
            {
                // Strip existing page parameter
                // e.g. &page=2 or &amp;page=2 or ?page=2
                string cleanUrl = Regex.Replace(baseUrl, @"([?&])(?:amp;)?page=\d+(&|$)", "$1", RegexOptions.IgnoreCase);
                cleanUrl = cleanUrl.TrimEnd('&', '?');
                if (page == 1)
                {
                    return cleanUrl;
                }
                string separator = cleanUrl.Contains("?") ? "&" : "?";
                return $"{cleanUrl}{separator}page={page}";
            }
            else
            {
                // Clean any trailing page numbers or trailing slash
                // e.g. /page/2 or /2 or /2/ or /page/2/
                string cleanUrl = Regex.Replace(baseUrl, @"/(?:page/)?\d+/?$", "");
                cleanUrl = cleanUrl.TrimEnd('/');
                
                if (page == 1)
                {
                    return cleanUrl + "/";
                }
                
                if (_usePagePathSegment)
                {
                    return $"{cleanUrl}/page/{page}";
                }
                return $"{cleanUrl}/{page}";
            }
        }

        private async void BtnFetchInfo_Click(object sender, RoutedEventArgs e)
        {
            string url = txtTagUrl.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Please enter a valid URL.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Ensure URL has a scheme
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            // Detect from current URL if it uses page path segment
            _usePagePathSegment = url.Contains("/page/");

            btnFetchInfo.IsEnabled = false;
            lblStatus.Text = "Analyzing target page...";
            progressBar.IsIndeterminate = true;
            Log($"Analyzing URL: {url}");

            try
            {
                string html = await _httpClient.GetStringAsync(url);
                
                // Parse absolute path to build pagination matching rules
                Uri uri = new Uri(url);
                string path = uri.AbsolutePath;
                // Strip trailing page number if present (e.g. /page/2 or /2)
                string baseFolder = Regex.Replace(path, @"/(?:page/)?\d+/?$", "");
                baseFolder = baseFolder.TrimEnd('/');

                // Create precise pattern for dynamic pagination links
                // e.g. ^/tag/brother/(?:page/)?(\d+)/?$
                string cleanBase = Regex.Escape(baseFolder);
                string patternDirect = "^" + cleanBase + @"/(?:page/)?(\d+)/?$";
                if (string.IsNullOrEmpty(baseFolder))
                {
                    patternDirect = @"^/(?:page/)?(\d+)/?$";
                }

                // Category fallback pattern: e.g. ^/character/naruto/(?:page/)?(\d+)/?$
                string patternFallback = @"^/(?:tag|character|parody|artist|group|groups|language|category|uploader)/[^/]+/(?:page/)?(\d+)/?$";

                // Parse all hrefs from the HTML
                var hrefMatches = Regex.Matches(html, @"href\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                
                int maxPage = 1;
                _usePagePathSegment = url.Contains("/page/");

                foreach (Match hrefMatch in hrefMatches)
                {
                    string link = hrefMatch.Groups[1].Value.Trim();
                    string linkPath = link;

                    // If it is absolute, extract path only
                    if (link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            linkPath = new Uri(link).AbsolutePath;
                        }
                        catch {}
                    }

                    // 1. Check direct pattern
                    var matchDirect = Regex.Match(linkPath, patternDirect, RegexOptions.IgnoreCase);
                    if (matchDirect.Success)
                    {
                        if (int.TryParse(matchDirect.Groups[1].Value, out int pageNum))
                        {
                            if (pageNum > maxPage) maxPage = pageNum;
                            if (linkPath.Contains("/page/"))
                            {
                                _usePagePathSegment = true;
                            }
                        }
                        continue;
                    }

                    // 2. Check fallback pattern
                    var matchFallback = Regex.Match(linkPath, patternFallback, RegexOptions.IgnoreCase);
                    if (matchFallback.Success)
                    {
                        if (int.TryParse(matchFallback.Groups[1].Value, out int pageNum))
                        {
                            if (pageNum > maxPage) maxPage = pageNum;
                            if (linkPath.Contains("/page/"))
                            {
                                _usePagePathSegment = true;
                            }
                        }
                        continue;
                    }

                    // 3. Check search query pattern
                    if (linkPath.Equals("/search", StringComparison.OrdinalIgnoreCase) || linkPath.Equals("/", StringComparison.OrdinalIgnoreCase))
                    {
                        var matchSearch = Regex.Match(link, @"[?&](?:amp;)?page=(\d+)", RegexOptions.IgnoreCase);
                        if (matchSearch.Success)
                        {
                            if (int.TryParse(matchSearch.Groups[1].Value, out int pageNum))
                            {
                                if (pageNum > maxPage) maxPage = pageNum;
                            }
                        }
                    }
                }

                _detectedMaxPage = maxPage;
                txtTotalPages.Text = _detectedMaxPage.ToString();
                txtPageTo.Text = _detectedMaxPage.ToString();
                Log($"Analysis completed. Detected maximum pages: {_detectedMaxPage}");
                lblStatus.Text = $"Analysis complete. Found {_detectedMaxPage} pages.";
            }
            catch (Exception ex)
            {
                Log($"Error during analysis: {ex.Message}");
                txtTotalPages.Text = "1";
                lblStatus.Text = "Analysis failed.";
            }
            finally
            {
                btnFetchInfo.IsEnabled = true;
                progressBar.IsIndeterminate = false;
            }
        }

        private void TxtTotalPages_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtPageTo != null && txtTotalPages != null)
            {
                txtPageTo.Text = txtTotalPages.Text;
            }
        }

        private async void BtnScrape_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null)
            {
                // Cancel active crawling process
                _cts.Cancel();
                btnScrape.Content = "CANCELLING...";
                btnScrape.IsEnabled = false;
                return;
            }

            string baseUrl = txtTagUrl.Text.Trim();
            if (string.IsNullOrEmpty(baseUrl))
            {
                MessageBox.Show("Please enter a valid URL.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Ensure URL has a scheme
            if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = "https://" + baseUrl;
            }

            // Detect page format from current URL
            if (baseUrl.Contains("/page/"))
            {
                _usePagePathSegment = true;
            }

            if (!int.TryParse(txtPageFrom.Text, out int pageFrom) || pageFrom < 1)
            {
                MessageBox.Show("Invalid 'From Page' value. Must be greater than or equal to 1.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(txtPageTo.Text, out int pageTo) || pageTo < pageFrom)
            {
                MessageBox.Show("Invalid 'To Page' value. Must be greater than or equal to 'From Page'.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            btnScrape.Content = "STOP CRAWLER";
            btnFetchInfo.IsEnabled = false;
            lblStatus.Text = "Crawling in progress...";
            progressBar.Value = 0;

            _scrapedItems.Clear();
            if (chkSelectAll != null)
            {
                chkSelectAll.IsChecked = false;
            }
            lblLinkCount.Text = "0";

            Log($"Starting crawler from page {pageFrom} to {pageTo}...");

            try
            {
                int totalPages = pageTo - pageFrom + 1;
                int pagesProcessed = 0;

                for (int page = pageFrom; page <= pageTo; page++)
                {
                    if (token.IsCancellationRequested)
                    {
                        token.ThrowIfCancellationRequested();
                    }

                    string pageUrl = GetPageUrl(baseUrl, page);
                    Log($"Requesting page {page}: {pageUrl}");
                    
                    string html = null;
                    bool pageLoaded = false;
                    try
                    {
                        html = await _httpClient.GetStringAsync(pageUrl);
                        pageLoaded = true;
                    }
                    catch (Exception ex)
                    {
                        Log($"Warning: Page {page} could not be loaded ({ex.Message}). Trying fallback as single gallery ID...");
                    }

                    int pageCount = 0;

                    if (pageLoaded && html != null)
                    {
                        // Extract view links along with their titles
                        // E.g. <a href="https://hentaiforce.net/view/10576">[Khaimera] SummeHea2</a>
                        var viewMatches = Regex.Matches(html, @"<a\s+href=""[^""]*?/view/(\d+)""\s*>\s*([^<]+?)\s*</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        
                        foreach (Match match in viewMatches)
                        {
                            string viewId = match.Groups[1].Value;
                            string title = WebUtility.HtmlDecode(match.Groups[2].Value.Trim());
                            title = FormatGalleryTitle(title);
                            string fullLink = $"https://hentaiforce.net/view/{viewId}";

                            if (!_scrapedItems.Any(item => item.Link == fullLink || item.Name.Equals(title, StringComparison.OrdinalIgnoreCase)))
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
                        Log($"Page {page} processed. Found {pageCount} unique view links on this page.");
                    }
                    else
                    {
                        // Fallback: Try to request the page number directly as a single gallery ID
                        string galleryUrl = $"https://hentaiforce.net/view/{page}";
                        try
                        {
                            string galleryHtml = await _httpClient.GetStringAsync(galleryUrl);
                            // Extract title of the gallery: <h1 class="text-left font-weight-bold">BlondBlaze(Dispatch).</h1>
                            var titleMatch = Regex.Match(galleryHtml, @"<h1\s+class=""[^""]*?font-weight-bold[^""]*?"">\s*([^<]+?)\s*</h1>", RegexOptions.IgnoreCase);
                            string title = $"Gallery {page}";
                            if (titleMatch.Success)
                            {
                                title = WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim());
                            }
                            title = FormatGalleryTitle(title);

                            string fullLink = galleryUrl;
                            if (!_scrapedItems.Any(item => item.Link == fullLink || item.Name.Equals(title, StringComparison.OrdinalIgnoreCase)))
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
                            Log($"Gallery ID {page} processed as single item. Found gallery: {title}");
                        }
                        catch (Exception ex2)
                        {
                            Log($"Warning: Gallery ID {page} fallback failed ({ex2.Message}). Skipping.");
                        }
                    }

                    pagesProcessed++;
                    double progressPct = ((double)pagesProcessed / totalPages) * 100;
                    progressBar.Value = progressPct;
                    lblStatus.Text = $"Searching page {page}/{pageTo} ({progressPct:0}%)";
                }

                RecalculateDuplicates();
                Log($"Crawling finished! Total unique links gathered: {_scrapedItems.Count}");
                lblStatus.Text = "Crawling completed successfully.";
                
                lblLinkCount.Text = _scrapedItems.Count.ToString();
            }
            catch (OperationCanceledException)
            {
                Log("Crawling process cancelled by user.");
                lblStatus.Text = "Crawling cancelled.";
            }
            catch (Exception ex)
            {
                Log($"Critical crawler error: {ex.Message}");
                lblStatus.Text = "Crawling failed due to error.";
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                btnScrape.Content = "START CRAWLING";
                btnScrape.IsEnabled = true;
                btnFetchInfo.IsEnabled = true;
            }
        }

        private async void BtnPasteDirect_Click(object sender, RoutedEventArgs e)
        {
            var win = new DirectDownloadWindow();
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                var links = win.ImportedLinks;
                if (links != null && links.Any())
                {
                    await ImportDirectLinksAsync(links);
                }
            }
        }

        private async Task ImportDirectLinksAsync(System.Collections.Generic.List<string> links)
        {
            btnScrape.IsEnabled = false;
            btnFetchInfo.IsEnabled = false;
            if (btnStartDownload != null) btnStartDownload.IsEnabled = false;
            
            progressBar.Value = 0;
            progressBar.IsIndeterminate = false;

            int total = links.Count;
            int imported = 0;
            int failed = 0;

            Log($"[Import] Bắt đầu phân tích và nhập {total} liên kết trực tiếp...");
            lblStatus.Text = $"Importing 0/{total} links...";

            try
            {
                for (int i = 0; i < total; i++)
                {
                    string link = links[i];
                    lblStatus.Text = $"[{i + 1}/{total}] Đang tải tiêu đề: {link}";

                    try
                    {
                        // Check if already exists in results grid
                        if (_scrapedItems.Any(item => item.Link.Equals(link, StringComparison.OrdinalIgnoreCase)))
                        {
                            Log($"[Import] Bỏ qua liên kết đã tồn tại: {link}");
                            imported++;
                            continue;
                        }

                        // Try to scrape title from page
                        string html = await _httpClient.GetStringAsync(link);
                        var titleMatch = Regex.Match(html, @"<h1\s+class=""[^""]*?font-weight-bold[^""]*?"">\s*([^<]+?)\s*</h1>", RegexOptions.IgnoreCase);
                        string title = "Gallery ID " + GetGalleryIdFromLink(link);
                        
                        if (titleMatch.Success)
                        {
                            title = WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim());
                        }

                        title = FormatGalleryTitle(title);

                        Dispatcher.Invoke(() =>
                        {
                            _scrapedItems.Add(new GalleryItem
                            {
                                Link = link,
                                Name = title,
                                OriginalIndex = _scrapedItems.Count,
                                IsChecked = true // Tick checkmark by default for direct links
                            });
                        });

                        Log($"[Import {i + 1}/{total}] Thành công: {title} ({link})");
                        imported++;
                    }
                    catch (Exception ex)
                    {
                        Log($"[Import] Lỗi khi xử lý link '{link}': {ex.Message}");
                        failed++;
                        
                        // Add fallback anyway so user doesn't lose the link
                        string fallbackTitle = "Fallback - Gallery ID " + GetGalleryIdFromLink(link);
                        Dispatcher.Invoke(() =>
                        {
                            _scrapedItems.Add(new GalleryItem
                            {
                                Link = link,
                                Name = fallbackTitle,
                                OriginalIndex = _scrapedItems.Count,
                                IsChecked = true
                            });
                        });
                    }

                    double pct = ((double)(i + 1) / total) * 100;
                    progressBar.Value = pct;
                }

                RecalculateDuplicates();
                lblLinkCount.Text = _scrapedItems.Count.ToString();

                Log($"[Import] Nhập hoàn tất! Thành công: {imported}, Lỗi/Fallback: {failed}. Tổng số liên kết hiện tại: {_scrapedItems.Count}");
                lblStatus.Text = $"Import completed. Success: {imported}, Failed: {failed}.";
                
                MessageBox.Show($"Đã nhập thành công {total} đường dẫn vào bảng Extracted Gallery Links!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                btnScrape.IsEnabled = true;
                btnFetchInfo.IsEnabled = true;
                if (btnStartDownload != null) btnStartDownload.IsEnabled = true;
                progressBar.Value = 100;
            }
        }

        private string GetGalleryIdFromLink(string link)
        {
            var match = Regex.Match(link, @"/view/(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return "Unknown";
        }

        private string FormatGalleryTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return title;

            string original = title.Trim();
            string leadingBrackets = "";
            
            while (true)
            {
                string trimmed = original.TrimStart();
                if (trimmed.Length == 0) break;
                
                char firstChar = trimmed[0];
                if (firstChar != '(' && firstChar != '[' && firstChar != '{')
                {
                    break;
                }
                
                char closeChar = firstChar == '(' ? ')' : (firstChar == '[' ? ']' : '}');
                
                // Find matching closing bracket considering nesting depth
                int depth = 0;
                int closeIndex = -1;
                for (int i = 0; i < trimmed.Length; i++)
                {
                    if (trimmed[i] == firstChar)
                    {
                        depth++;
                    }
                    else if (trimmed[i] == closeChar)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            closeIndex = i;
                            break;
                        }
                    }
                }
                
                if (closeIndex != -1)
                {
                    string bracketContent = trimmed.Substring(0, closeIndex + 1);
                    leadingBrackets += " " + bracketContent;
                    original = trimmed.Substring(closeIndex + 1);
                }
                else
                {
                    break;
                }
            }
            
            title = original.Trim();
            
            // Clean up leading delimiters like hyphens, dashes, dots, commas, colons, slashes, plus signs, pipes, underscores, or tildes left over after stripping brackets
            title = Regex.Replace(title, @"^[-–—_.\u2026·•．・°:;：,/\\+~=|*\s]+", "");

            if (!string.IsNullOrEmpty(leadingBrackets))
            {
                title = title + " " + leadingBrackets.Trim();
            }
            
            return title.Trim();
        }
    }
}
