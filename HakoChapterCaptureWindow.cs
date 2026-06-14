using System;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace get_link_manga
{
    [DataContract]
    internal sealed class HakoChapterCaptureResult
    {
        [DataMember(Name = "title")]
        public string ChapterTitle { get; set; }

        [DataMember(Name = "contentHtml")]
        public string ContentHtml { get; set; }

        [DataMember(Name = "bookTitle")]
        public string BookTitle { get; set; }

        [DataMember(Name = "isChallenge")]
        public bool IsChallenge { get; set; }
    }

    internal sealed class HakoChapterCaptureWindow : Window
    {
        private readonly string _targetUrl;
        private readonly bool _isVietnamese;
        private readonly TextBlock _statusText;
        private readonly WebView2 _webView;
        private bool _captureFinished;

        internal HakoChapterCaptureResult CaptureResult { get; private set; }

        private HakoChapterCaptureWindow(string targetUrl, bool isVietnamese)
        {
            _targetUrl = targetUrl ?? string.Empty;
            _isVietnamese = isVietnamese;

            Title = isVietnamese ? "COPY CHAPTER WEBVIEW2" : "CHAPTER COPY WEBVIEW2";
            Width = 1280;
            Height = 880;
            MinWidth = 960;
            MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x12, 0x1F));

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            _statusText = new TextBlock
            {
                Margin = new Thickness(12),
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF)),
                FontWeight = FontWeights.Bold,
                Text = isVietnamese ? "Đang mở chapter..." : "Opening chapter..."
            };
            Grid.SetRow(_statusText, 0);
            root.Children.Add(_statusText);

            _webView = new WebView2();
            Grid.SetRow(_webView, 1);
            root.Children.Add(_webView);

            Content = root;
            Loaded += OnLoadedAsync;
        }

        internal static async Task<HakoChapterCaptureResult> CaptureAsync(Window owner, string targetUrl, bool isVietnamese, CancellationToken token)
        {
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var window = new HakoChapterCaptureWindow(targetUrl, isVietnamese)
                {
                    Owner = owner
                };

                CancellationTokenRegistration registration = default(CancellationTokenRegistration);
                if (token.CanBeCanceled)
                {
                    registration = token.Register(() =>
                    {
                        try
                        {
                            window.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (window.IsVisible)
                                {
                                    window.Close();
                                }
                            }));
                        }
                        catch
                        {
                        }
                    });
                }

                try
                {
                    window.ShowDialog();
                    return window.CaptureResult;
                }
                finally
                {
                    registration.Dispose();
                }
            }).Task;
        }

        private async void OnLoadedAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(
                    null,
                    Path.Combine(PortablePaths.WebView2UserDataFolder, "hako-chapter-copy"),
                    new CoreWebView2EnvironmentOptions());
                await _webView.EnsureCoreWebView2Async(env);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = true;
                _webView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;
                _webView.Source = new Uri(_targetUrl);
            }
            catch
            {
                Close();
            }
        }

        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || _captureFinished || _webView.CoreWebView2 == null)
            {
                return;
            }

            _statusText.Text = _isVietnamese ? "Đang bỏ chặn copy và lấy text..." : "Unlocking copy and extracting text...";

            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(@"
(() => {
  if (!document.getElementById('gmtpc-copy-unlock')) {
    const style = document.createElement('style');
    style.id = 'gmtpc-copy-unlock';
    style.textContent = '*{user-select:text !important;-webkit-user-select:text !important} #chapter-content,.chapter-content,.long-text{user-select:text !important;-webkit-user-select:text !important;}';
    document.documentElement.appendChild(style);
  }
})()");
            }
            catch
            {
            }

            for (int attempt = 0; attempt < 90; attempt++)
            {
                if (_captureFinished || _webView.CoreWebView2 == null)
                {
                    return;
                }

                try
                {
                    string json = await _webView.CoreWebView2.ExecuteScriptAsync(@"
(() => {
  const titleNode = document.querySelector('.title-top h4') || document.querySelector('.title-top h2') || document.querySelector('.title-top h6');
  const contentNode = document.querySelector('#chapter-content') || document.querySelector('.chapter-content') || document.querySelector('.long-text');
  const bookNode = document.querySelector('a[href^=""/truyen/""]') || document.querySelector('a[href^=""/sang-tac/""]');
  const html = document.documentElement ? document.documentElement.outerHTML : '';
  const bodyText = document.body ? document.body.innerText : '';
  const isChallenge = /cf-turnstile|cf-challenge|cloudflare|just a moment|captcha|xác minh/i.test(html) || /just a moment|captcha|xác minh/i.test(bodyText);
  return {
    title: titleNode ? (titleNode.innerText || '').trim() : '',
    contentHtml: contentNode ? contentNode.innerHTML : '',
    bookTitle: bookNode ? (bookNode.innerText || '').trim() : '',
    isChallenge: isChallenge
  };
})()");

                    HakoChapterCaptureResult result = DeserializeJson<HakoChapterCaptureResult>(json);
                    if (result != null && !string.IsNullOrWhiteSpace(result.ContentHtml))
                    {
                        CaptureResult = result;
                        _captureFinished = true;
                        Close();
                        return;
                    }

                    if (result != null && result.IsChallenge)
                    {
                        _statusText.Text = _isVietnamese
                            ? "Phát hiện captcha. Hãy vượt captcha, xong app tự đóng chap."
                            : "Captcha detected. Solve it, then app auto closes chapter.";
                    }
                    else
                    {
                        _statusText.Text = _isVietnamese
                            ? $"Đang chờ nội dung chapter... ({attempt + 1}/90)"
                            : $"Waiting chapter content... ({attempt + 1}/90)";
                    }
                }
                catch
                {
                }

                await Task.Delay(500);
            }

            Close();
        }

        private static T DeserializeJson<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return serializer.ReadObject(stream) as T;
            }
        }
    }
}
