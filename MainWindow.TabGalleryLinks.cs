using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private async void BtnCopyChaptersLink_Click(object sender, RoutedEventArgs e)
        {
            var items = GetItemsToExport();
            if (!items.Any())
            {
                MessageBox.Show(_isVietnameseUi ? "Không có truyện nào để lấy link chapter." : "No books to copy chapter links.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            progressBar.IsIndeterminate = true;
            lblStatus.Text = _isVietnameseUi ? "Đang lấy link các chapter..." : "Extracting chapter links...";
            
            try
            {
                var allChapterLinks = new List<string>();
                foreach (var item in items)
                {
                    var links = await ExtractChapterLinksFromBookAsync(item, CancellationToken.None);
                    allChapterLinks.AddRange(links);
                }

                if (allChapterLinks.Any())
                {
                    string text = string.Join("\r\n", allChapterLinks);
                    Clipboard.SetText(text);
                    Log($"Copied {allChapterLinks.Count} chapter link(s) to clipboard.");
                    lblStatus.Text = _isVietnameseUi ? $"Đã copy {allChapterLinks.Count} link chapter." : $"Copied {allChapterLinks.Count} chapter links.";
                }
                else
                {
                    MessageBox.Show(_isVietnameseUi ? "Không tìm thấy link chapter nào." : "No chapter links found.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Log($"Error copying chapter links: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                progressBar.IsIndeterminate = false;
            }
        }

        private async Task<List<string>> ExtractChapterLinksFromBookAsync(GalleryItem item, CancellationToken token)
        {
            string url = item.Link;
            if (string.IsNullOrWhiteSpace(url)) return new List<string>();

            if (url.Contains("/chuong-") || url.Contains("/chap-") || url.Contains("/doc-truyen-tranh/") || url.Contains("/chapter-"))
            {
                return new List<string> { url };
            }

            try
            {
                if (IsTruyenqqUrl(url))
                {
                    string cleanLink = ResolveTruyenqqRequestUrl(url);
                    string activeDomain = ExtractTruyenqqBaseUrl(cleanLink);
                    var uri = new Uri(cleanLink);
                    var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 2 && segments[0].Equals("truyen-tranh", StringComparison.OrdinalIgnoreCase))
                    {
                        bool captchaOk = await SolveTruyenqqCaptchaIfNeededAsync(cleanLink);
                        cleanLink = ResolveTruyenqqRequestUrl(cleanLink);
                        string html = await FetchStringAsync(cleanLink, token);
                        string parentPath = uri.AbsolutePath.TrimEnd('/');
                        string escapedPath = Regex.Escape(parentPath);
                        string pattern = @"href=[""'](?<link>[^""']*?" + escapedPath + @"-chap(?:[^""'\s?#]*)?)[""']";
                        var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
                        var chapterLinks = new List<string>();
                        var seenChapters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                        if (chapterLinks.Count > 0)
                        {
                            return chapterLinks.OrderBy(ParseChapterNumber).ToList();
                        }
                    }
                }
                else if (IsNettruyenUrl(url))
                {
                    string cleanLink = url.TrimEnd('/');
                    string activeDomain = ExtractNettruyenBaseUrl(cleanLink);
                    var uri = new Uri(cleanLink);
                    bool captchaOk = await SolveNettruyenCaptchaIfNeededAsync(cleanLink);
                    string html = "";
                    if (!string.IsNullOrEmpty(_lastCaptchaResolvedHtml))
                    {
                        html = NormalizeNettruyenHtml(_lastCaptchaResolvedHtml);
                        _lastCaptchaResolvedHtml = null;
                    }
                    else
                    {
                        html = NormalizeNettruyenHtml(await FetchStringAsync(cleanLink, token));
                    }
                    string storyId = null;
                    var idMatch = Regex.Match(html, @"id=[""'](?:story_id|storyId|comicId)[""'][^>]*value=[""'\s]?(?<id>\d+)[""'\s]?", RegexOptions.IgnoreCase);
                    if (!idMatch.Success) idMatch = Regex.Match(html, @"value=[""'\s]?(?<id>\d+)[""'\s]?[^>]*id=[""'](?:story_id|storyId|comicId)[""']", RegexOptions.IgnoreCase);
                    if (!idMatch.Success) idMatch = Regex.Match(html, @"(?:story_id|storyId|comicId)\s*=\s*(?:[""']?(?<id>\d+)[""']?|\d+)", RegexOptions.IgnoreCase);
                    if (!idMatch.Success) idMatch = Regex.Match(html, @"data-id=[""'](?<id>\d+)[""']", RegexOptions.IgnoreCase);
                    if (idMatch.Success) storyId = idMatch.Groups["id"].Value;

                    string chapterListHtml = html;
                    if (!string.IsNullOrEmpty(storyId))
                    {
                        try
                        {
                            string ajaxUrl = $"{activeDomain}/Comic/Services/ComicService.asmx/ProcessChapterList";
                            using (var request = new HttpRequestMessage(HttpMethod.Post, ajaxUrl))
                            {
                                request.Headers.Referrer = new Uri(cleanLink);
                                request.Content = new StringContent($"{{\"comicId\":{storyId}}}", System.Text.Encoding.UTF8, "application/json");
                                using (var response = await _httpClient.SendAsync(request, token))
                                {
                                    if (response.IsSuccessStatusCode)
                                    {
                                        string jsonResponse = await response.Content.ReadAsStringAsync();
                                        var dMatch = Regex.Match(jsonResponse, @"""d""\s*:\s*""(?<htmlContent>.*?)""\s*}", RegexOptions.Singleline);
                                        if (dMatch.Success)
                                        {
                                            string unescapedHtml = NormalizeNettruyenHtml(Regex.Unescape(dMatch.Groups["htmlContent"].Value));
                                            if (!string.IsNullOrWhiteSpace(unescapedHtml) && unescapedHtml.Length > 100)
                                            {
                                                chapterListHtml = unescapedHtml;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch {}
                    }
                    string parentPath = uri.AbsolutePath.TrimEnd('/');
                    var chapterLinks = ExtractNettruyenChapterLinks(chapterListHtml, activeDomain, parentPath);
                    if (chapterLinks.Count > 0)
                    {
                        return chapterLinks.OrderBy(ParseChapterNumber).ToList();
                    }
                }
                else if (IsDaomeodenUrl(url))
                {
                    string normalizedLink = NormalizeDaomeodenUrl(url);
                    string html = await FetchStringAsync(normalizedLink, token);
                    var chapterMatches = Regex.Matches(html, @"(?:href|openUrl\()\s*(?:=\s*|['""])(?<link>/doc-truyen-tranh/[^'"")\s>]+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    var chapterLinks = new List<string>();
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (Match match in chapterMatches)
                    {
                        string link = NormalizeDaomeodenUrl(match.Groups["link"].Value);
                        if (seen.Add(link))
                        {
                            chapterLinks.Add(link);
                        }
                    }
                    if (chapterLinks.Count > 0)
                    {
                        return chapterLinks.OrderBy(ParseChapterNumber).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[Copy Chapters] Lỗi lấy link chapter cho {item.Name}: {ex.Message}");
            }

            return new List<string> { url };
        }
    }
}
