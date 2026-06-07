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
        private DateTime _captchaBypassStartTime = DateTime.MinValue;
        private DateTime _lastCaptchaKeyboardAttempt = DateTime.MinValue;

        public CaptchaWindow(string targetUrl)
        {
            InitializeComponent();
            if (webViewHost != null)
            {
                webViewHost.Children.Add(webView);
            }
            _targetUrl = targetUrl;
            try
            {
                var uri = new Uri(targetUrl);
                this.Title = $"GIẢI CAPTCHA CLOUDFLARE - {uri.Host.ToUpper()}";
            }
            catch
            {
                this.Title = "GIẢI CAPTCHA CLOUDFLARE";
            }
            Loaded += CaptchaWindow_Loaded;
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

                // Start auto-bypass detection loop
                _ = AutoDetectBypassAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khởi tạo trình duyệt WebView2: {ex.Message}\n\nHãy đảm bảo bạn đã cài đặt WebView2 Runtime trên hệ thống.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        if (!title.Contains("Just a moment") && !title.Contains("Cloudflare"))
                        {
                            bool shouldDelay = false;
                            if (url.IndexOf("nettruyen", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                // Find and click "Xem thêm" by text content (CSS selectors are unreliable across nettruyen domains)
                                string processChaptersJs = @"
                                    (function() {
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
                                        var html = document.documentElement.outerHTML || '';
                                        var hasChapter = /\/(?:chuong|chap|chapter|c|chuong-tranh|chuong-doc)-(?:0|1|2|3|4|5|6|7|8|9|10)(?:\/|\s|""|'|\?|$)/i.test(html);
                                        if (hasChapter) {
                                            return 'ready';
                                        }
                                        if (!xemThem) {
                                            return 'ready';
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
                                    if (elapsed < 15.0) // Timeout after 15 seconds of waiting for chapters to load
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

                                // Great! We bypassed it. Let's auto click done.
                                Dispatcher.Invoke(() =>
                                {
                                    BtnDone_Click(this, null);
                                });
                                break;
                            }
                        }
                    }
                    
                    // Turnstile captcha bypass via keyboard sequence: Ctrl+F → "human" → Escape → Shift+Tab → Space
                    if (url.IndexOf("nettruyen", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Track when the captcha page first appeared
                        if (_captchaBypassStartTime == DateTime.MinValue)
                        {
                            _captchaBypassStartTime = DateTime.Now;
                        }

                        double secsSinceStart = (DateTime.Now - _captchaBypassStartTime).TotalSeconds;
                        double secsSinceLastAttempt = (DateTime.Now - _lastCaptchaKeyboardAttempt).TotalSeconds;

                        // First attempt after 10 seconds, then every 5 seconds
                        bool shouldAttempt = false;
                        if (_lastCaptchaKeyboardAttempt == DateTime.MinValue && secsSinceStart >= 10)
                        {
                            shouldAttempt = true;
                        }
                        else if (_lastCaptchaKeyboardAttempt != DateTime.MinValue && secsSinceLastAttempt >= 5)
                        {
                            shouldAttempt = true;
                        }

                        if (shouldAttempt)
                        {
                            _lastCaptchaKeyboardAttempt = DateTime.Now;

                            await Dispatcher.InvokeAsync(() =>
                            {
                                try
                                {
                                    // Activate window and focus WebView2
                                    this.Activate();
                                    webView.Focus();
                                    System.Windows.Input.Keyboard.Focus(webView);
                                }
                                catch { }
                            });
                            await Task.Delay(300);

                            // Step 1: Ctrl+F to open Find dialog
                            System.Windows.Forms.SendKeys.SendWait("^f");
                            await Task.Delay(500);

                            // Step 2: Type "human" to search
                            System.Windows.Forms.SendKeys.SendWait("human");
                            await Task.Delay(500);

                            // Step 3: Escape to close Find dialog
                            System.Windows.Forms.SendKeys.SendWait("{ESCAPE}");
                            await Task.Delay(300);

                            // Step 4: Shift+Tab to move focus to the checkbox
                            System.Windows.Forms.SendKeys.SendWait("+{TAB}");
                            await Task.Delay(300);

                            // Step 5: Space to check the checkbox
                            System.Windows.Forms.SendKeys.SendWait(" ");
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
                    MessageBox.Show("Trình duyệt chưa sẵn sàng.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"Lỗi thu thập cookies: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDeleteCookies_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.CookieManager.DeleteAllCookies();

                    // Reset the bypass state so the challenge flow starts over cleanly.
                    ResolvedCookies = new CookieContainer();
                    ResolvedUri = null;
                    ResolvedHtml = null;
                    _captchaBypassStartTime = DateTime.MinValue;
                    _lastCaptchaKeyboardAttempt = DateTime.MinValue;

                    await Task.Delay(250);

                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            if (webView.CoreWebView2 != null)
                            {
                                webView.CoreWebView2.Navigate(_targetUrl);
                            }
                        }
                        catch { }
                    });

                    MessageBox.Show("Đã xóa cookie và tải lại trang để bypass captcha lại.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Trình duyệt chưa sẵn sàng.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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
