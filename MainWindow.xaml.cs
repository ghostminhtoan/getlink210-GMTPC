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
    }
}
