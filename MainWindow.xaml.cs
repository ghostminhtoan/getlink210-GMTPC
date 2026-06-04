using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace get_link_manga
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly CookieContainer _cookieContainer;
        private static readonly HttpClientHandler _httpHandler;
        private static readonly HttpClient _httpClient;
        private CancellationTokenSource _cts;
        private int _detectedMaxPage = 1;
        private bool _usePagePathSegment = false;
        internal ObservableCollection<GalleryItem> _scrapedItems = new ObservableCollection<GalleryItem>();
        internal DuplicateWindow _duplicateWindowInstance;
        internal BookmarkHistoryManager _bookmarkManager = new BookmarkHistoryManager();
        private BookmarkHistoryWindow _bookmarkHistoryWindowInstance;
        private readonly System.Windows.Controls.ProgressBar progressBar = new System.Windows.Controls.ProgressBar();

        static MainWindow()
        {
            _cookieContainer = new CookieContainer();
            _httpHandler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer = _cookieContainer,
                UseCookies = true
            };
            _httpClient = new HttpClient(_httpHandler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

public MainWindow()
        {
            InitializeComponent();
            dgResults.ItemsSource = _scrapedItems;

            try
            {
                txtDownloadPath.Text = System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\Documents\image downloader GMTPC"));
            }
            catch {}

            Log("System initialized. Ready for commands.");

            Loaded += (s, e) =>
            {
                StyleComboBoxPopup(cmbNhentaiSort);
                StyleComboBoxPopup(cmbConnections);
                StyleComboBoxPopup(cmbMultiDownload);
            };
        }

        private void StyleComboBoxPopup(System.Windows.Controls.ComboBox comboBox)
        {
            comboBox.ApplyTemplate();
            var popup = comboBox.Template.FindName("Popup", comboBox) as Popup;
            if (popup != null)
            {
                popup.Opened += (sender, args) =>
                {
                    if (popup.Child is System.Windows.Controls.Border border)
                    {
                        border.Background = new SolidColorBrush(Color.FromRgb(0x12, 0x16, 0x22));
                    }
                };
            }
        }

        internal void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string logLine = $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";
                if (txtLog != null)
                {
                    txtLog.AppendText(logLine);
                    if (!txtLog.IsMouseOver)
                        txtLog.ScrollToEnd();
                }
                if (txtNhentaiLog != null)
                {
                    txtNhentaiLog.AppendText(logLine);
                    if (!txtNhentaiLog.IsMouseOver)
                        txtNhentaiLog.ScrollToEnd();
                }
                if (txtTruyenqqLog != null)
                {
                    txtTruyenqqLog.AppendText(logLine);
                    if (!txtTruyenqqLog.IsMouseOver)
                        txtTruyenqqLog.ScrollToEnd();
                }
            });
        }
            private void BtnReverseOrder_Click(object sender, RoutedEventArgs e)
        {
            var reversed = _scrapedItems.Reverse().ToList();
            for (int i = 0; i < reversed.Count; i++)
            {
                reversed[i].OriginalIndex = i;
            }
            _scrapedItems.Clear();
            foreach (var item in reversed)
            {
                _scrapedItems.Add(item);
            }
            Log("Order reversed.");
        }

        private void BtnViHentaiReverseOrder_Click(object sender, RoutedEventArgs e)
        {
            BtnReverseOrder_Click(sender, e);
            ViHentaiLog($"[Reverse] Đã đảo ngược thứ tự {_scrapedItems.Count} mục.");
        }

        private void BtnTruyenqqReverseOrder_Click(object sender, RoutedEventArgs e)
        {
            BtnReverseOrder_Click(sender, e);
            TruyenqqLog($"[Reverse] Đã đảo ngược thứ tự {_scrapedItems.Count} mục.");
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            if (txtLog != null) txtLog.Clear();
        }

        private void BtnClearNhentaiLog_Click(object sender, RoutedEventArgs e)
        {
            if (txtNhentaiLog != null) txtNhentaiLog.Clear();
        }

        private void BtnClearViHentaiLog_Click(object sender, RoutedEventArgs e)
        {
            if (txtViHentaiLog != null) txtViHentaiLog.Clear();
        }

        private void BtnClearTruyenqqLog_Click(object sender, RoutedEventArgs e)
        {
            if (txtTruyenqqLog != null) txtTruyenqqLog.Clear();
        }

        private void BtnCaptcha_Click(object sender, RoutedEventArgs e)
        {
            string url = "";
            var btn = sender as System.Windows.Controls.Button;
            if (btn == btnFetchCaptcha) url = txtTagUrl.Text;
            else if (btn == btnNhentaiFetchCaptcha) url = txtNhentaiTagUrl.Text;
            else if (btn == btnViHentaiFetchCaptcha) url = txtViHentaiTagUrl.Text;
            else if (btn == btnTruyenqqFetchCaptcha) url = txtTruyenqqTagUrl.Text;

            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Vui lòng nhập TARGET TAG URL trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var captchaWin = new CaptchaWindow(url)
            {
                Owner = this
            };

            if (captchaWin.ShowDialog() == true)
            {
                try
                {
                    var originalUri = new Uri(url);
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

                    Log("Đồng bộ cookie và user-agent từ CaptchaWindow thành công!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi lưu cookie: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnClearComplete_Click(object sender, RoutedEventArgs e)
        {
            var toRemove = _scrapedItems.Where(item => 
                item.Status == "Completed" || 
                item.Status == "Done" || 
                item.CurrentProcess == "Done"
            ).ToList();

            if (toRemove.Count == 0)
            {
                MessageBox.Show("Không có truyện nào hoàn thành để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in toRemove)
            {
                _scrapedItems.Remove(item);
            }
            Log($"Đã xóa {toRemove.Count} truyện hoàn thành khỏi danh sách.");
            lblLinkCount.Text = _scrapedItems.Count.ToString();
        }
    }
}
