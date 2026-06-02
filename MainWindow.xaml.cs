using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Threading;
using System.Windows;

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
        }

        internal void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string logLine = $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";
                if (txtLog != null)
                {
                    txtLog.AppendText(logLine);
                    txtLog.ScrollToEnd();
                }
                if (txtNhentaiLog != null)
                {
                    txtNhentaiLog.AppendText(logLine);
                    txtNhentaiLog.ScrollToEnd();
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
    }
}
