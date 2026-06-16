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
        private static readonly RoutedUICommand StartLightNovelAutoCopyCommand =
            new RoutedUICommand("Start light novel auto copy", "StartLightNovelAutoCopy", typeof(MainWindow));
        private static readonly RoutedUICommand StopLightNovelAutoCopyCommand =
            new RoutedUICommand("Stop light novel auto copy", "StopLightNovelAutoCopy", typeof(MainWindow));
        private static CookieContainer _cookieContainer;
        private static HttpClientHandler _httpHandler;
        private static HttpClient _httpClient;
        private static readonly string _defaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        private static readonly SemaphoreSlim _captchaSemaphore = new SemaphoreSlim(1, 1);
        private static volatile bool _isCaptchaWindowActive = false;
        private bool _hakoCaptchaSessionReady;
        private bool _displaySettingsHooked;
        internal string _truyenqqPreferredBaseUrl;
        private CancellationTokenSource _cts;
        private int _detectedMaxPage = 1;
        private bool _usePagePathSegment = false;
        internal ObservableCollection<GalleryItem> _scrapedItems = new ObservableCollection<GalleryItem>();
        internal ObservableCollection<GalleryItem> _lightNovelItems = new ObservableCollection<GalleryItem>();
        internal DuplicateWindow _duplicateWindowInstance;
        internal BookmarkHistoryManager _bookmarkManager = new BookmarkHistoryManager();
        private BookmarkHistoryWindow _bookmarkHistoryWindowInstance;
        private readonly System.Windows.Controls.ProgressBar progressBar = new System.Windows.Controls.ProgressBar();
        private bool _startupArchivePromptShown;

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
            InitializeWorkspaceShell();
            HookDisplaySettingsChanged();
            PreviewMouseWheel += MainWindow_PreviewMouseWheel;
            Loaded += (s, e) => ApplyAdaptiveLayout(new Size(ActualWidth, ActualHeight));
            _isVietnameseUi = true;
            ApplyCurrentUiLanguage();
            InitializeGalleryListAutosave();
            ApplyBuildInfoText();
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
                txtDownloadPath.Text = PortablePaths.DefaultDownloadRoot;
            }
            catch {}

            Log("System initialized. Ready for commands.");

            Loaded += (s, e) =>
            {
                StyleComboBoxPopup(cmbCreateSubfolderDomain);
                StyleComboBoxPopup(cmbNhentaiSort);
                StyleComboBoxPopup(cmbConnections);
                StyleComboBoxPopup(cmbMultiDownload);

                CommandBindings.Add(new CommandBinding(ApplicationCommands.New, WindowNew_Executed));
                CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, WindowSave_Executed));
                CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, WindowOpen_Executed));
                CommandBindings.Add(new CommandBinding(StartLightNovelAutoCopyCommand, BtnStartLightNovelCopy_Click));
                CommandBindings.Add(new CommandBinding(StopLightNovelAutoCopyCommand, BtnStopLightNovelCopy_Click));
                InputBindings.Add(new KeyBinding(ApplicationCommands.New, new KeyGesture(Key.N, ModifierKeys.Control)));
                InputBindings.Add(new KeyBinding(ApplicationCommands.Save, new KeyGesture(Key.S, ModifierKeys.Control)));
                InputBindings.Add(new KeyBinding(ApplicationCommands.Open, new KeyGesture(Key.O, ModifierKeys.Control)));
                InputBindings.Add(new KeyBinding(StartLightNovelAutoCopyCommand, new KeyGesture(Key.F2, ModifierKeys.Control)));
                InputBindings.Add(new KeyBinding(StopLightNovelAutoCopyCommand, new KeyGesture(Key.F2, ModifierKeys.Alt)));

                var view = ResultsView;
                if (view != null && view.SortDescriptions.Count == 0)
                {
                    view.SortDescriptions.Add(new SortDescription("OriginalIndex", ListSortDirection.Ascending));
                }

                EnsureLightNovelFloatingControlWindow();
                if (_lightNovelFloatingControlWindow != null && !_lightNovelFloatingControlWindow.IsVisible)
                {
                    _lightNovelFloatingControlWindow.Show();
                }
                UpdateLightNovelFloatingControlState();

            };

            Closing += (s, e) =>
            {
                UnhookDisplaySettingsChanged();
                DisposeLightNovelFocusTrayIcon();
                _lightNovelFloatingControlWindow?.Close();
                SaveActiveGalleryListSnapshot();
            };
        }

        private void HookDisplaySettingsChanged()
        {
            if (_displaySettingsHooked)
            {
                return;
            }

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            _displaySettingsHooked = true;
        }

        private void UnhookDisplaySettingsChanged()
        {
            if (!_displaySettingsHooked)
            {
                return;
            }

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
            _displaySettingsHooked = false;
        }

        private void StyleComboBoxPopup(System.Windows.Controls.ComboBox comboBox)
        {
            if (comboBox == null)
            {
                return;
            }

            comboBox.ApplyTemplate();
            if (comboBox.Template == null)
            {
                return;
            }

            var popup = comboBox.Template.FindName("Popup", comboBox) as Popup;
            if (popup != null)
            {
                popup.Opened += (sender, args) =>
                {
                    if (popup.Child is System.Windows.Controls.Border border)
                    {
                        border.Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x12, 0x1F));
                    }
                };
            }
        }

        // Scroll a TextBox/RichTextBox to the end WITHOUT needing keyboard focus.
        private static void ScrollTextBoxToEnd(TextBoxBase textBox)
        {
            // Auto scroll disabled.
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
                if (txtHakoLog != null)
                {
                    AppendLogLine(txtHakoLog, logLine, isError);
                }
                if (txtTruyenggvnLog != null)
                {
                    AppendLogLine(txtTruyenggvnLog, logLine, isError);
                    if (chkAutoScrollTruyenggvnLog?.IsChecked == true)
                        ScrollTextBoxToEnd(txtTruyenggvnLog);
                }
                if (txtHentaieraLog != null)
                {
                    AppendLogLine(txtHentaieraLog, logLine, isError);
                    if (chkAutoScrollHentaieraLog?.IsChecked == true)
                        ScrollTextBoxToEnd(txtHentaieraLog);
                }

                if (isError)
                {
                    string source = "GENERAL";
                    string bookName = "-";
                    string chapterName = "-";
                    int pageNumber = 0;
                    string imageUrl = null;
                    RecordCheckError(source, bookName, chapterName, pageNumber, message, imageUrl);
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

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            ClearLogPanel(txtLog);
        }

        private void BtnClearCheckErrors_Click(object sender, RoutedEventArgs e)
        {
            ClearCheckErrors();
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

        private void BtnClearTruyenggvnLog_Click(object sender, RoutedEventArgs e)
        {
            ClearLogPanel(txtTruyenggvnLog);
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
            else if (btnHakoFetchCaptcha != null && btn == btnHakoFetchCaptcha) url = txtHakoTagUrl.Text;
            else if (btn == btnTruyenggvnFetchCaptcha) url = txtTruyenggvnTagUrl.Text;
            else if (btnHentaieraFetchCaptcha != null && btn == btnHentaieraFetchCaptcha) url = txtHentaieraTagUrl.Text;

            if (string.IsNullOrWhiteSpace(url))
            {
                ShowWarning("Vui lòng nhập TARGET TAG URL trước.", "Thông báo");
                return;
            }

            if (btn == btnNhentaiFetchCaptcha)
            {
                ResetCookiesForCaptcha(url);
                ShowInfo("Đã xóa cookie cho nhentai.xxx. Site này không cần captcha nữa.", "Thông báo");
                return;
            }

            ResetCookiesForCaptcha(url);

            var captchaWin = new CaptchaWindow(url, autoDeleteCookiesOnLoad: true)
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

                    if (captchaWin.BypassWasNeeded)
                    {
                        Log("Đồng bộ cookie và user-agent từ CaptchaWindow thành công sau khi bypass captcha.");
                    }
                    else
                    {
                        Log("Đồng bộ cookie và user-agent từ CaptchaWindow thành công. Không phát hiện captcha thật.");
                    }
                }
                catch (Exception ex)
                {
                    ShowError($"Lỗi lưu cookie: {ex.Message}", "Lỗi");
                }
            }
        }

        private void ResetCookiesForCaptcha(string url)
        {
            try
            {
                InitializeHttpClientState();
                _hakoCaptchaSessionReady = false;
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
            var toRemove = _scrapedItems
                .Where(item => item != null && item.IsSuccessfullyCompleted())
                .ToList();

            if (toRemove.Count == 0)
            {
                ShowInfo("Không có truyện nào hoàn thành để xóa.", "Thông báo");
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

        private void WindowNew_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            BtnNewList_Click(sender, new RoutedEventArgs());
        }

        private void WindowSave_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            BtnSaveCustom_Click(sender, new RoutedEventArgs());
        }

        private void WindowOpen_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            BtnLoadCustom_Click(sender, new RoutedEventArgs());
        }
    }
}
