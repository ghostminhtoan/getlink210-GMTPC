using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Input;

namespace get_link_manga
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static CookieContainer _cookieContainer;
        private static HttpClientHandler _httpHandler;
        private static HttpClient _httpClient;
        private static readonly string _defaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        private static readonly SemaphoreSlim _captchaSemaphore = new SemaphoreSlim(1, 1);
        private static volatile bool _isCaptchaWindowActive = false;
        internal string _truyenqqPreferredBaseUrl;
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
            InitializeHttpClientState();
        }

        private static void InitializeHttpClientState()
        {
            _cookieContainer = new CookieContainer();
            _httpHandler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer = _cookieContainer,
                UseCookies = true
            };
            _httpClient = new HttpClient(_httpHandler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", _defaultUserAgent);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public MainWindow()
        {
            InitializeComponent();
            void TogglePauseResume(System.Windows.Controls.Button button)
            {
                _isDownloadPaused = !_isDownloadPaused;

                var nextText = _isDownloadPaused ? "Resume all" : "Pause all";
                button.Content = nextText;
                button.Tag = _isDownloadPaused ? "resume" : "pause";
            }

            System.Windows.Controls.Button FindPauseButton()
            {
                System.Windows.Controls.Button found = null;

                void Walk(System.Windows.DependencyObject node)
                {
                    if (node == null || found != null)
                    {
                        return;
                    }

                    var button = node as System.Windows.Controls.Button;
                    if (button != null)
                    {
                        var contentBlock = button.Content as System.Windows.Controls.TextBlock;
                        var contentText = contentBlock != null
                            ? contentBlock.Text
                            : (button.Content == null ? null : button.Content.ToString());
                        var tagText = button.Tag == null ? null : button.Tag.ToString();
                        var nameText = button.Name;

                        if (string.Equals(contentText, "Pause all", System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(contentText, "Resume all", System.StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(contentText) && contentText.IndexOf("pause", System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (!string.IsNullOrEmpty(contentText) && contentText.IndexOf("resume", System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                            string.Equals(tagText, "pause", System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(tagText, "resume", System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(nameText, "BtnPauseDownload", System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(nameText, "BtnPauseAll", System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(nameText, "BtnResumeDownload", System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(nameText, "BtnResumeAll", System.StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(nameText) && nameText.IndexOf("pause", System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (!string.IsNullOrEmpty(nameText) && nameText.IndexOf("resume", System.StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            found = button;
                            return;
                        }
                    }

                    var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);
                    for (var i = 0; i < childCount; i++)
                    {
                        Walk(System.Windows.Media.VisualTreeHelper.GetChild(node, i));
                        if (found != null)
                        {
                            return;
                        }
                    }
                }

                Walk(this);
                return found;
            }

            Loaded += (sender, args) =>
            {
                var pauseButton = FindPauseButton();
                if (pauseButton == null)
                {
                    return;
                }

                pauseButton.PreviewMouseLeftButtonDown += (buttonSender, mouseArgs) =>
                {
                    mouseArgs.Handled = true;
                    TogglePauseResume((System.Windows.Controls.Button)buttonSender);
                };

                pauseButton.PreviewKeyDown += (buttonSender, keyArgs) =>
                {
                    if (keyArgs.Key != System.Windows.Input.Key.Enter && keyArgs.Key != System.Windows.Input.Key.Space)
                    {
                        return;
                    }

                    keyArgs.Handled = true;
                    TogglePauseResume((System.Windows.Controls.Button)buttonSender);
                };
            };
            InitializeLogPanels();
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

                if (btnPauseDownload != null)
                {
                    btnPauseDownload.PreviewMouseLeftButtonUp += (sender, args) =>
                    {
                        if (_isDownloadPaused)
                        {
                            ResumeAllDownloads();
                        }
                        else
                        {
                            PauseAllDownloads();
                        }

                        args.Handled = true;
                    };
                }

                CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, WindowSave_Executed));
                CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, WindowOpen_Executed));
                InputBindings.Add(new KeyBinding(ApplicationCommands.Save, new KeyGesture(Key.S, ModifierKeys.Control)));
                InputBindings.Add(new KeyBinding(ApplicationCommands.Open, new KeyGesture(Key.O, ModifierKeys.Control)));

                var view = ResultsView;
                if (view != null && view.SortDescriptions.Count == 0)
                {
                    view.SortDescriptions.Add(new SortDescription("OriginalIndex", ListSortDirection.Ascending));
                }
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

        // Scroll a TextBox/RichTextBox to the end WITHOUT needing keyboard focus.
        private static void ScrollTextBoxToEnd(TextBoxBase textBox)
        {
            if (textBox == null) return;
            // Walk visual tree to find the embedded ScrollViewer
            var sv = FindVisualChild<System.Windows.Controls.ScrollViewer>(textBox);
            if (sv != null)
            {
                sv.ScrollToEnd();
            }
            else
            {
                textBox.ScrollToEnd();
            }
        }

        public static double ExtractNumber(string input)
        {
            if (string.IsNullOrEmpty(input)) return 0.0;
            var matches = System.Text.RegularExpressions.Regex.Matches(input, @"\d+(?:\.\d+)?");
            if (matches.Count > 0)
            {
                if (double.TryParse(matches[matches.Count - 1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double result))
                {
                    return result;
                }
            }
            return 0.0;
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) return typed;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private bool IsErrorMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            string lower = message.ToLower();
            return lower.Contains("lỗi") ||
                   lower.Contains("error") ||
                   lower.Contains("exception") ||
                   lower.Contains("failed") ||
                   lower.Contains("timeout") ||
                   lower.Contains("forbidden") ||
                   lower.Contains("too many request") ||
                   lower.Contains("thất bại") ||
                   lower.Contains("không thể") ||
                   lower.Contains("403") ||
                   lower.Contains("503") ||
                   lower.Contains("429");
        }

        private void AppendLogLine(System.Windows.Controls.RichTextBox rtb, string text, bool isError)
        {
            AppendLogLineWithFilter(rtb, text, isError);
        }

        internal void Log(string message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string logLine = $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";
                bool isError = IsErrorMessage(message);
                if (txtLog != null)
                {
                    AppendLogLine(txtLog, logLine, isError);
                    if (chkAutoScrollLog?.IsChecked == true)
                        ScrollTextBoxToEnd(txtLog);
                }
                if (txtNhentaiLog != null)
                {
                    AppendLogLine(txtNhentaiLog, logLine, isError);
                    if (chkAutoScrollNhentaiLog?.IsChecked == true)
                        ScrollTextBoxToEnd(txtNhentaiLog);
                }
                if (txtTruyenqqLog != null)
                {
                    AppendLogLine(txtTruyenqqLog, logLine, isError);
                    if (chkAutoScrollTruyenqqLog?.IsChecked == true)
                        ScrollTextBoxToEnd(txtTruyenqqLog);
                }
                if (txtNettruyenLog != null)
                {
                    AppendLogLine(txtNettruyenLog, logLine, isError);
                    if (chkAutoScrollNettruyenLog?.IsChecked == true)
                        ScrollTextBoxToEnd(txtNettruyenLog);
                }
                if (txtHentaieraLog != null)
                {
                    AppendLogLine(txtHentaieraLog, logLine, isError);
                    if (chkAutoScrollHentaieraLog?.IsChecked == true)
                        ScrollTextBoxToEnd(txtHentaieraLog);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        private void BtnReverseOrder_Click(object sender, RoutedEventArgs e)
        {
            var view = ResultsView;
            if (view != null)
            {
                if (view.SortDescriptions.Count > 0)
                {
                    var currentSort = view.SortDescriptions[0];
                    string propertyName = currentSort.PropertyName;
                    var newDirection = currentSort.Direction == ListSortDirection.Ascending 
                        ? ListSortDirection.Descending 
                        : ListSortDirection.Ascending;

                    view.SortDescriptions.Clear();
                    view.SortDescriptions.Add(new SortDescription(propertyName, newDirection));
                    
                    if (propertyName == "HasNoChapters")
                    {
                        view.SortDescriptions.Add(new SortDescription("OriginalIndex", newDirection));
                    }

                    Log($"Đảo ngược chiều sắp xếp cho {propertyName} ({newDirection}).");
                }
                else
                {
                    view.SortDescriptions.Add(new SortDescription("OriginalIndex", ListSortDirection.Descending));
                    Log("Đảo ngược chiều sắp xếp cho OriginalIndex (Descending).");
                }
            }
        }

        private async void BtnRetryErrors_Click(object sender, RoutedEventArgs e)
        {
            var targetItems = _scrapedItems.Where(item => item.IsChecked && item.ErrorCount > 0).ToList();
            if (!targetItems.Any())
            {
                targetItems = _scrapedItems.Where(item => item.ErrorCount > 0).ToList();
            }

            if (!targetItems.Any())
            {
                MessageBox.Show("Không tìm thấy truyện nào có lỗi để tải lại.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            btnRetryErrors.IsEnabled = false;
            Log($"[Retry] Bắt đầu tải lại lỗi cho {targetItems.Count} truyện...");
            try
            {
                foreach (var item in targetItems)
                {
                    await RetryDownloadQueueItemErrorsAsync(item, showMessageBox: false);
                }
                MessageBox.Show($"Hoàn tất tải lại lỗi cho {targetItems.Count} truyện!", "Retry Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"[Retry Error] Lỗi khi chạy hàng đợi tải lại: {ex.Message}");
            }
            finally
            {
                btnRetryErrors.IsEnabled = true;
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            ClearLogPanel(txtLog);
        }

        private void BtnClearNhentaiLog_Click(object sender, RoutedEventArgs e)
        {
            ClearLogPanel(txtNhentaiLog);
        }

        private void BtnClearViHentaiLog_Click(object sender, RoutedEventArgs e)
        {
            ClearLogPanel(txtViHentaiLog);
        }

        private void BtnClearTruyenqqLog_Click(object sender, RoutedEventArgs e)
        {
            ClearLogPanel(txtTruyenqqLog);
        }

        private void BtnClearNettruyenLog_Click(object sender, RoutedEventArgs e)
        {
            ClearLogPanel(txtNettruyenLog);
        }

        private void BtnClearHentaieraLog_Click(object sender, RoutedEventArgs e)
        {
            ClearLogPanel(txtHentaieraLog);
        }

        private void BtnCaptcha_Click(object sender, RoutedEventArgs e)
        {
            string url = "";
            var btn = sender as System.Windows.Controls.Button;
            if (btn == btnFetchCaptcha) url = txtTagUrl.Text;
            else if (btn == btnNhentaiFetchCaptcha) url = txtNhentaiTagUrl.Text;
            else if (btn == btnViHentaiFetchCaptcha) url = txtViHentaiTagUrl.Text;
            else if (btn == btnTruyenqqFetchCaptcha) url = txtTruyenqqTagUrl.Text;
            else if (btn == btnNettruyenFetchCaptcha) url = txtNettruyenTagUrl.Text;
            else if (btnHentaieraFetchCaptcha != null && btn == btnHentaieraFetchCaptcha) url = txtHentaieraTagUrl.Text;

            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Vui lòng nhập TARGET TAG URL trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResetCookiesForCaptcha(url);

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

        private void ResetCookiesForCaptcha(string url)
        {
            try
            {
                InitializeHttpClientState();
                if (IsTruyenqqUrl(url))
                {
                    _truyenqqPreferredBaseUrl = null;
                }
                Log("Đã xóa cookie và khởi tạo lại phiên captcha.");
            }
            catch (Exception ex)
            {
                Log($"[Captcha] Không thể reset cookie: {ex.Message}");
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
                DeleteProcessMarkdownForItem(item);
                _scrapedItems.Remove(item);
            }

            Log($"Đã xóa {toRemove.Count} truyện hoàn thành khỏi danh sách.");
            lblLinkCount.Text = _scrapedItems.Count.ToString();
        }

        private int _currentMaxParallelBooks = 2;
        private DynamicSemaphore _activeBookSemaphore;

        private void WindowSave_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            BtnSave_Click(sender, new RoutedEventArgs());
        }

        private void WindowOpen_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            BtnLoad_Click(sender, new RoutedEventArgs());
        }
    }
}
