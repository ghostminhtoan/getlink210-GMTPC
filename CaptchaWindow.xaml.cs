using System;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace get_link_manga
{
    public partial class CaptchaWindow : Window
    {
        public CookieContainer ResolvedCookies { get; private set; } = new CookieContainer();
        public Uri ResolvedUri { get; private set; }
        public string UserAgent { get; private set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        private readonly string _targetUrl;

        public CaptchaWindow(string targetUrl)
        {
            InitializeComponent();
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
                // Setup WebView2 user data folder inside local appdata to persist cookies/session and ensure clean execution
                var env = await CoreWebView2Environment.CreateAsync(null, System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "get_link_manga_webview2"));
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
                            // Great! We bypassed it. Let's auto click done.
                            Dispatcher.Invoke(() =>
                            {
                                BtnDone_Click(this, null);
                            });
                            break;
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

                // Expose current redirected URI
                ResolvedUri = webView.Source;

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

        private void BtnDeleteCookies_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.CookieManager.DeleteAllCookies();
                    MessageBox.Show("Đã xóa toàn bộ cookie thành công (All cookies deleted successfully).", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
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
    }
}
