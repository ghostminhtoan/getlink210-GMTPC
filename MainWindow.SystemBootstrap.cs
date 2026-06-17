using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace get_link_manga
{
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
        private bool _usePagePathSegment;
        internal ObservableCollection<GalleryItem> _scrapedItems = new ObservableCollection<GalleryItem>();
        internal ObservableCollection<GalleryItem> _lightNovelItems = new ObservableCollection<GalleryItem>();
        internal DuplicateWindow _duplicateWindowInstance;
        internal BookmarkHistoryManager _bookmarkManager = new BookmarkHistoryManager();
        private BookmarkHistoryWindow _bookmarkHistoryWindowInstance;
        private readonly System.Windows.Controls.ProgressBar progressBar = new System.Windows.Controls.ProgressBar();
        private bool _startupArchivePromptShown;
        private int _currentMaxParallelBooks = 2;
        private DynamicSemaphore _activeBookSemaphore;

        static MainWindow()
        {
            InitializeHttpClientState();
        }

        public MainWindow()
        {
            UnfreezeApplicationBrushes();
            InitializeComponent();
            InitializeWorkspaceShell();
            HookDisplaySettingsChanged();
            PreviewMouseWheel += MainWindow_PreviewMouseWheel;
            Loaded += (s, e) => ApplyAdaptiveLayout(new Size(ActualWidth, ActualHeight));
            _isVietnameseUi = true;
            ApplyCurrentUiLanguage();
            InitializeGalleryListAutosave();
            ApplyBuildInfoText();
            WirePauseButtonToggle();
            InitializeLogPanels();
            dgResults.ItemsSource = _scrapedItems;

            try
            {
                txtDownloadPath.Text = PortablePaths.DefaultDownloadRoot;
            }
            catch
            {
            }

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
                    _lightNovelFloatingControlWindow.ShowWithoutActivationSafe();
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
