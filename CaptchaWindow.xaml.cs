using System;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace get_link_manga
{
    public partial class CaptchaWindow : Window
    {
        private readonly WebView2 webView = new WebView2();
        public CookieContainer ResolvedCookies { get; private set; } = new CookieContainer();
        public Uri ResolvedUri { get; private set; }
        public string UserAgent { get; private set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        public string ResolvedHtml { get; private set; }
        private readonly string _targetUrl;
        private readonly bool _autoDeleteCookiesOnLoad;
        private readonly bool _headlessAutomation;
        private readonly DateTime _windowOpenedAt = DateTime.Now;
        private DateTime _captchaBypassStartTime = DateTime.MinValue;
        private DateTime _lastCaptchaKeyboardAttempt = DateTime.MinValue;
        private DateTime _challengeDetectedAt = DateTime.MinValue;
        public bool BypassWasNeeded { get; private set; }
        public double WindowElapsedSeconds => (DateTime.Now - _windowOpenedAt).TotalSeconds;

        public CaptchaWindow(string targetUrl, bool autoDeleteCookiesOnLoad = false, bool headlessAutomation = false)
        {
            InitializeComponent();
            if (webViewHost != null)
            {
                webViewHost.Children.Add(webView);
            }
            _targetUrl = targetUrl;
            _autoDeleteCookiesOnLoad = autoDeleteCookiesOnLoad;
            _headlessAutomation = headlessAutomation;
            ApplyLanguage(GetIsVietnameseUiEnabled());

            if (_headlessAutomation)
            {
                ConfigureHeadlessWindow();
            }

            try
            {
                var uri = new Uri(targetUrl);
                this.Title = $"{GetCaptchaWindowTitlePrefix()} - {uri.Host.ToUpper()}";
            }
            catch
            {
                this.Title = GetCaptchaWindowTitlePrefix();
            }
            Loaded += CaptchaWindow_Loaded;
        }

        private void ConfigureHeadlessWindow()
        {
            ShowInTaskbar = false;
            ShowActivated = false;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Width = 1;
            Height = 1;
            Left = -10000;
            Top = -10000;
            Opacity = 0;

            if (txtCaptchaHeader != null)
            {
                txtCaptchaHeader.Visibility = Visibility.Collapsed;
            }
            if (txtCaptchaDescription != null)
            {
                txtCaptchaDescription.Visibility = Visibility.Collapsed;
            }
            if (btnDeleteCookies != null)
            {
                btnDeleteCookies.Visibility = Visibility.Collapsed;
            }
            if (btnDone != null)
            {
                btnDone.Visibility = Visibility.Collapsed;
            }
            if (btnCancel != null)
            {
                btnCancel.Visibility = Visibility.Collapsed;
            }
        }

        private bool GetIsVietnameseUiEnabled()
        {
            try
            {
                if (Application.Current?.Properties["IsVietnameseUi"] is bool isVietnamese)
                {
                    return isVietnamese;
                }
            }
            catch
            {
            }

            return false;
        }

        private string GetCaptchaWindowTitlePrefix()
        {
            return GetIsVietnameseUiEnabled() ? "Vượt Cloudflare Captcha" : "Cloudflare Captcha";
        }

        private void ApplyLanguage(bool isVietnamese)
        {
            if (isVietnamese)
            {
                if (txtCaptchaHeader != null) txtCaptchaHeader.Text = "VƯỢT CLOUDFLARE CAPTCHA";
                if (txtCaptchaDescription != null) txtCaptchaDescription.Text = "VUI LÒNG HOÀN THÀNH THỬ THÁCH TRONG TRÌNH DUYỆT BÊN DƯỚI. KHI TRANG TẢI XONG, NHẤN 'ĐÃ XONG'";
                if (btnDeleteCookies != null) btnDeleteCookies.Content = "XÓA COOKIE";
                if (btnDone != null) btnDone.Content = "ĐÃ XONG CAPTCHA";
                if (btnCancel != null) btnCancel.Content = "HỦY";
            }
            else
            {
                if (txtCaptchaHeader != null) txtCaptchaHeader.Text = "CLOUDFLARE CAPTCHA BYPASS";
                if (txtCaptchaDescription != null) txtCaptchaDescription.Text = "PLEASE COMPLETE THE CHALLENGE IN THE BROWSER BELOW. WHEN THE PAGE FINISHES LOADING, CLICK 'DONE'";
                if (btnDeleteCookies != null) btnDeleteCookies.Content = "DELETE COOKIES";
                if (btnDone != null) btnDone.Content = "CAPTCHA DONE";
                if (btnCancel != null) btnCancel.Content = "CANCEL";
            }
        }

        private static bool UrlContainsHost(string url, params string[] patterns)
        {
            if (string.IsNullOrWhiteSpace(url) || patterns == null)
            {
                return false;
            }

            for (int i = 0; i < patterns.Length; i++)
            {
                string pattern = patterns[i];
                if (!string.IsNullOrWhiteSpace(pattern) &&
                    url.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNettruyenUrl(string url)
        {
            return UrlContainsHost(url, "nettruyen");
        }

        private static bool IsTruyenqqChallengeUrl(string url)
        {
            return UrlContainsHost(url, "truyenqq", "truyenqqko");
        }

        private static bool ShouldUseVerifyFindSequence(string url)
        {
            return IsTruyenqqChallengeUrl(url);
        }

        private double GetInitialCaptchaAttemptDelaySeconds(string url)
        {
            return ShouldUseVerifyFindSequence(url) ? 10.0 : 8.0;
        }

        private double GetRepeatCaptchaAttemptDelaySeconds(string url)
        {
            return ShouldUseVerifyFindSequence(url) ? 12.0 : 5.0;
        }

        private string GetCaptchaFindKeyword(string url)
        {
            return ShouldUseVerifyFindSequence(url) ? "verify" : "human";
        }

        private async Task SendCaptchaKeyboardBypassAsync(string url)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    this.Activate();
                    webView.Focus();
                    System.Windows.Input.Keyboard.Focus(webView);
                }
                catch
                {
                }
            });
            await Task.Delay(300);

            string findKeyword = GetCaptchaFindKeyword(url);

            System.Windows.Forms.SendKeys.SendWait("^f");
            await Task.Delay(500);

            System.Windows.Forms.SendKeys.SendWait(findKeyword);
            await Task.Delay(500);

            System.Windows.Forms.SendKeys.SendWait("{ESCAPE}");
            await Task.Delay(300);

            System.Windows.Forms.SendKeys.SendWait("+{TAB}");
            await Task.Delay(300);

            System.Windows.Forms.SendKeys.SendWait(" ");
        }

        private async void CaptchaWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Keep WebView2 data next to the portable app root so a copied exe remains self-contained.
                var env = await CoreWebView2Environment.CreateAsync(
                    null,
                    PortablePaths.WebView2UserDataFolder,
                    new CoreWebView2EnvironmentOptions());
                await webView.EnsureCoreWebView2Async(env);
                
                webView.Source = new Uri(_targetUrl);

                if (_autoDeleteCookiesOnLoad)
                {
                    await DeleteCookiesAndReloadAsync(showMessage: false);
                }

                // Start auto-bypass detection loop
                _ = AutoDetectBypassAsync();
            }
            catch (Exception ex)
            {
                if (!_headlessAutomation)
                {
                    MessageBox.Show($"Lỗi khởi tạo trình duyệt WebView2: {ex.Message}\n\nHãy đảm bảo bạn đã cài đặt WebView2 Runtime trên hệ thống.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                DialogResult = false;
                Close();
            }
        }

        private async Task AutoDetectBypassAsync()
        {
            DateTime nettruyenChaptersWaitStartTime = DateTime.MinValue;
            while (true)
            {
                await Task.Delay(1000);
                if (webView.CoreWebView2 == null) continue;

                try
                {
                    string url = "";
                    string title = "";
                    Dispatcher.Invoke(() =>
                    {
                        url = webView.Source?.ToString() ?? "";
                        title = webView.CoreWebView2.DocumentTitle ?? "";
                    });

                    // Execute JS check to see if we've successfully loaded the page content without cloudflare block
                    string jsCheck = @"
                        (function() {
                            var html = document.documentElement.outerHTML || '';
                            if (html.indexOf('cf-challenge') !== -1 || 
                                html.indexOf('cf-turnstile') !== -1 || 
                                html.indexOf('Turnstile') !== -1 || 
                                html.indexOf('Just a moment...') !== -1 ||
                                html.indexOf('Performing security verification') !== -1 ||
                                html.indexOf('thực hiện xác minh bảo mật') !== -1 ||
                                html.indexOf('xác minh bạn không phải là bot') !== -1) {
                                return 'challenge';
                            }
                            if (document.getElementById('cf-challenge-running') || 
                                document.getElementById('challenge-form') || 
                                html.indexOf('challenge-platform') !== -1) {
                                return 'challenge';
                            }
                            return 'ok';
                        })()";

                    string result = await Dispatcher.Invoke(async () =>
                    {
                        try
                        {
                            return await webView.CoreWebView2.ExecuteScriptAsync(jsCheck);
                        }
                        catch
                        {
                            return "challenge";
                        }
                    });

                    if (result != null && result.Trim('"') == "ok")
                    {
                        bool challengeWasReal = BypassWasNeeded ||
                            (_challengeDetectedAt != DateTime.MinValue && (DateTime.Now - _challengeDetectedAt).TotalSeconds >= 2.0);
                        BypassWasNeeded = challengeWasReal;

                        if (!title.Contains("Just a moment") && !title.Contains("Cloudflare"))
                        {
                            bool shouldDelay = false;
                            if (IsNettruyenUrl(url))
                            {
                                // Find and click "Xem thêm" by text content (CSS selectors are unreliable across nettruyen domains)
                                string processChaptersJs = @"
                                    (function() {
                                        function getChapterLinks() {
                                            var html = document.documentElement.outerHTML || '';
                                            var matches = html.match(/\/(?:chuong|chap|chapter|c|chuong-tranh|chuong-doc)-\d+(?:\.\d+)?(?:\/|\s|""|'|\?|$)/gi);
                                            return matches ? matches.length : 0;
                                        }
                                        var xemThem = null;
                                        var allEls = document.querySelectorAll('a, button, span, div');
                                        for (var i = 0; i < allEls.length; i++) {
                                            var txt = (allEls[i].textContent || '').trim();
                                            if (txt === 'Xem thêm' || txt === '+ Xem thêm' || txt === 'xem thêm' || txt === '+ xem thêm') {
                                                xemThem = allEls[i];
                                                break;
                                            }
                                        }
                                        if (xemThem && (xemThem.offsetWidth > 0 || xemThem.offsetHeight > 0)) {
                                            xemThem.click();
                                            return 'clicked';
                                        }
                                        var chapterCount = getChapterLinks();
                                        if (chapterCount > 0) {
                                            return 'ready';
                                        }
                                        if (!xemThem) {
                                            return 'waiting';
                                        }
                                        return 'waiting';
                                    })()";

                                string statusStr = await Dispatcher.Invoke(async () =>
                                {
                                    try { return await webView.CoreWebView2.ExecuteScriptAsync(processChaptersJs); } catch { return "ready"; }
                                });

                                string statusVal = statusStr?.Trim('"') ?? "ready";
                                if (statusVal == "clicked" || statusVal == "waiting")
                                {
                                    if (nettruyenChaptersWaitStartTime == DateTime.MinValue)
                                    {
                                        nettruyenChaptersWaitStartTime = DateTime.Now;
                                    }
                                    
                                    double elapsed = (DateTime.Now - nettruyenChaptersWaitStartTime).TotalSeconds;
                                    if (elapsed < 20.0) // Timeout after 20 seconds of waiting for chapters to load
                                    {
                                        shouldDelay = true;
                                    }
                                }
                            }
                            if (!shouldDelay)
                            {
                                // Get final HTML
                                string finalHtml = await Dispatcher.Invoke(async () =>
                                {
                                    try { return await webView.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML"); } catch { return null; }
                                });
                                if (!string.IsNullOrEmpty(finalHtml))
                                {
                                     if (finalHtml.StartsWith("\"") && finalHtml.EndsWith("\""))
                                     {
                                         finalHtml = UnescapeJsonString(finalHtml);
                                     }
                                    ResolvedHtml = finalHtml;
                                }

                                // False positive if window clears almost instantly without a real challenge.
                                Dispatcher.Invoke(() =>
                                {
                                    BtnDone_Click(this, null);
                                });
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (_challengeDetectedAt == DateTime.MinValue)
                        {
                            _challengeDetectedAt = DateTime.Now;
                        }

                        if (_headlessAutomation && _challengeDetectedAt != DateTime.MinValue &&
                            (DateTime.Now - _challengeDetectedAt).TotalSeconds >= 6.0)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                DialogResult = false;
                                Close();
                            });
                            break;
                        }
                    }
                    
                    // Turnstile captcha bypass via keyboard sequence.
                    if (!_headlessAutomation && (IsNettruyenUrl(url) || IsTruyenqqChallengeUrl(url)))
                    {
                        if (_captchaBypassStartTime == DateTime.MinValue)
                        {
                            _captchaBypassStartTime = DateTime.Now;
                        }

                        double secsSinceStart = (DateTime.Now - _captchaBypassStartTime).TotalSeconds;
                        double secsSinceLastAttempt = (DateTime.Now - _lastCaptchaKeyboardAttempt).TotalSeconds;
                        double initialDelay = GetInitialCaptchaAttemptDelaySeconds(url);
                        double repeatDelay = GetRepeatCaptchaAttemptDelaySeconds(url);

                        bool shouldAttempt = false;
                        if (_lastCaptchaKeyboardAttempt == DateTime.MinValue && secsSinceStart >= initialDelay)
                        {
                            shouldAttempt = true;
                        }
                        else if (_lastCaptchaKeyboardAttempt != DateTime.MinValue && secsSinceLastAttempt >= repeatDelay)
                        {
                            shouldAttempt = true;
                        }

                        if (shouldAttempt)
                        {
                            BypassWasNeeded = true;
                            _lastCaptchaKeyboardAttempt = DateTime.Now;
                            await SendCaptchaKeyboardBypassAsync(url);
                        }
                    }
                }
                catch
                {
                    // Ignore errors during page loading
                }
            }
        }

        private async void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (webView.CoreWebView2 == null)
                {
                    if (!_headlessAutomation)
                    {
                        MessageBox.Show("Trình duyệt chưa sẵn sàng.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }

                // Expose current redirected URI from the final WebView navigation if possible
                if (webView.Source != null)
                {
                    ResolvedUri = webView.Source;
                }
                else if (webView.CoreWebView2 != null &&
                         Uri.TryCreate(webView.CoreWebView2.Source, UriKind.Absolute, out Uri finalUri))
                {
                    ResolvedUri = finalUri;
                }

                try
                {
                    string finalHtml = await webView.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");
                    if (!string.IsNullOrWhiteSpace(finalHtml))
                    {
                        if (finalHtml.StartsWith("\"") && finalHtml.EndsWith("\""))
                        {
                            finalHtml = UnescapeJsonString(finalHtml);
                        }

                        ResolvedHtml = finalHtml;
                    }
                }
                catch
                {
                }

                // Get cookies from WebView2 CookieManager for the final redirected URL
                string fetchUrl = ResolvedUri?.ToString() ?? _targetUrl;
                var list = await webView.CoreWebView2.CookieManager.GetCookiesAsync(fetchUrl);
                ResolvedCookies = new CookieContainer();
                var uri = new Uri(fetchUrl);
                
                foreach (var w2Cookie in list)
                {
                    var cookie = new Cookie(w2Cookie.Name, w2Cookie.Value, w2Cookie.Path, w2Cookie.Domain);
                    ResolvedCookies.Add(uri, cookie);
                }

                // Also get cookies for original URL just in case
                if (fetchUrl != _targetUrl)
                {
                    try
                    {
                        var originalUri = new Uri(_targetUrl);
                        var originalList = await webView.CoreWebView2.CookieManager.GetCookiesAsync(_targetUrl);
                        foreach (var w2Cookie in originalList)
                        {
                            var cookie = new Cookie(w2Cookie.Name, w2Cookie.Value, w2Cookie.Path, w2Cookie.Domain);
                            ResolvedCookies.Add(originalUri, cookie);
                        }
                    }
                    catch {}
                }

                // Get User-Agent dynamically
                string ua = await webView.CoreWebView2.ExecuteScriptAsync("navigator.userAgent");
                if (!string.IsNullOrEmpty(ua))
                {
                    if (ua.StartsWith("\"") && ua.EndsWith("\"") && ua.Length > 2)
                    {
                        ua = ua.Substring(1, ua.Length - 2);
                    }
                    UserAgent = ua;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                if (!_headlessAutomation)
                {
                    MessageBox.Show($"Lỗi thu thập cookies: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                DialogResult = false;
                Close();
            }
        }

        private async Task DeleteCookiesAndReloadAsync(bool showMessage)
        {
            if (webView.CoreWebView2 == null)
            {
                throw new InvalidOperationException("Trình duyệt chưa sẵn sàng.");
            }

            webView.CoreWebView2.CookieManager.DeleteAllCookies();

            ResolvedCookies = new CookieContainer();
            ResolvedUri = null;
            ResolvedHtml = null;
            BypassWasNeeded = false;
            _captchaBypassStartTime = DateTime.MinValue;
            _lastCaptchaKeyboardAttempt = DateTime.MinValue;
            _challengeDetectedAt = DateTime.MinValue;

            await Task.Delay(250);

            Dispatcher.Invoke(() =>
            {
                try
                {
                    webView.CoreWebView2.Reload();
                }
                catch
                {
                    try
                    {
                        webView.CoreWebView2.Navigate(_targetUrl);
                    }
                    catch
                    {
                    }
                }
            });

            if (showMessage)
            {
                MessageBox.Show("Đã xóa cookie, refresh trang, tiếp tục chờ captcha/bypass.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnDeleteCookies_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await DeleteCookiesAndReloadAsync(showMessage: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xóa cookie: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private string UnescapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            value = value.Trim();
            if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
            {
                value = value.Substring(1, value.Length - 2);
            }
            return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
