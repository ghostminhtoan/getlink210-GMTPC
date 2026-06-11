using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        internal enum ReaderFitMode
        {
            FitWidth,
            FitHeight,
            ActualSize,
            VerticalScroll,
            HorizontalScrollLTR,
            HorizontalScrollRTL
        }

        private sealed class ReaderSortKeyComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                if (x == null)
                {
                    return -1;
                }

                if (y == null)
                {
                    return 1;
                }

                string[] leftParts = Regex.Split(x, "(\\d+)");
                string[] rightParts = Regex.Split(y, "(\\d+)");
                int max = Math.Max(leftParts.Length, rightParts.Length);

                for (int i = 0; i < max; i++)
                {
                    if (i >= leftParts.Length)
                    {
                        return -1;
                    }

                    if (i >= rightParts.Length)
                    {
                        return 1;
                    }

                    bool leftIsNumber = int.TryParse(leftParts[i], out int leftNumber);
                    bool rightIsNumber = int.TryParse(rightParts[i], out int rightNumber);

                    int result;
                    if (leftIsNumber && rightIsNumber)
                    {
                        result = leftNumber.CompareTo(rightNumber);
                    }
                    else
                    {
                        result = string.Compare(leftParts[i], rightParts[i], StringComparison.OrdinalIgnoreCase);
                    }

                    if (result != 0)
                    {
                        return result;
                    }
                }

                return 0;
            }
        }

        private readonly ReaderSortKeyComparer _readerSortComparer = new ReaderSortKeyComparer();
        private List<ReaderMangaItem> _readerLibrary = new List<ReaderMangaItem>();
        private ReaderMangaItem _currentReaderManga;
        private ReaderChapterItem _currentReaderChapter;
        private ReaderPageItem _currentReaderPage;
        internal ReaderFitMode _readerFitMode = ReaderFitMode.FitWidth;
        private ReaderFullscreenWindow _fullscreenWindow = null;
        private string _lastReaderLibraryRoot;
        private bool _readerWebViewReady;
        private bool _readerSelectionGuard;
        private WebView2 _readerWebView;
        private ListBox _readerMangaList;
        private ListBox _readerChapterList;
        private ComboBox _readerPageCombo;
        private TextBlock _readerSummaryText;
        private TextBlock _readerStatusText;
        private TextBlock _readerCurrentTitleText;
        private Button _readerPrevChapterButton;
        private Button _readerPrevPageButton;
        private Button _readerNextPageButton;
        private Button _readerNextChapterButton;
        private ComboBox _readerFitCombo;
        private Button _readerFullscreenButton;
        private Border _readerSidebarBorder;
        private Grid _readerRootGrid;
        private bool _isReaderFullscreen = false;
        private Button _readerHistoryOpenButton;

        private FrameworkElement CreateWatchSection()
        {
            _readerRootGrid = new Grid();
            _readerRootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });
            _readerRootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            _readerRootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _readerSidebarBorder = new Border
            {
                Background = (Brush)TryFindResource("CyberpunkCardBrush") ?? new SolidColorBrush(Color.FromRgb(0x0D, 0x12, 0x1F)),
                BorderBrush = (Brush)TryFindResource("CyberpunkBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14)
            };

            var sidebarGrid = new Grid();
            sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _readerSidebarBorder.Child = sidebarGrid;

            var watchToolbar = new WrapPanel();
            watchToolbar.Children.Add(CreateReaderMiniButton("Refresh library", (sender, args) => RefreshReaderLibraryIfNeeded(forceRefresh: true)));
            watchToolbar.Children.Add(CreateReaderMiniButton("Open root", BtnOpenFolder_Click));
            _readerHistoryOpenButton = CreateReaderMiniButton("Open latest history", OpenLatestHistoryInReader);
            watchToolbar.Children.Add(_readerHistoryOpenButton);

            _readerSummaryText = new TextBlock
            {
                Foreground = (Brush)TryFindResource("CyberpunkMutedTextBrush"),
                FontSize = 10,
                Margin = new Thickness(0, 10, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };

            _readerMangaList = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x09, 0x0D, 0x16)),
                BorderBrush = (Brush)TryFindResource("CyberpunkBorderBrush"),
                BorderThickness = new Thickness(1),
                Foreground = (Brush)TryFindResource("CyberpunkTextBrush"),
                DisplayMemberPath = "DisplayLabel",
                MinHeight = 180
            };
            _readerMangaList.SelectionChanged += ReaderMangaList_SelectionChanged;

            _readerChapterList = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x09, 0x0D, 0x16)),
                BorderBrush = (Brush)TryFindResource("CyberpunkBorderBrush"),
                BorderThickness = new Thickness(1),
                Foreground = (Brush)TryFindResource("CyberpunkTextBrush"),
                DisplayMemberPath = "DisplayLabel",
                MinHeight = 180
            };
            _readerChapterList.SelectionChanged += ReaderChapterList_SelectionChanged;

            Grid.SetRow(watchToolbar, 0);
            Grid.SetRow(_readerSummaryText, 1);
            Grid.SetRow(_readerMangaList, 2);
            Grid.SetRow(_readerChapterList, 4);
            sidebarGrid.Children.Add(watchToolbar);
            sidebarGrid.Children.Add(_readerSummaryText);
            sidebarGrid.Children.Add(_readerMangaList);
            sidebarGrid.Children.Add(_readerChapterList);

            var viewerCard = new Border
            {
                Background = (Brush)TryFindResource("CyberpunkCardBrush") ?? new SolidColorBrush(Color.FromRgb(0x0D, 0x12, 0x1F)),
                BorderBrush = (Brush)TryFindResource("CyberpunkBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14)
            };

            var viewerGrid = new Grid();
            viewerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            viewerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            viewerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            viewerCard.Child = viewerGrid;

            var topToolbar = new Grid();
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // << Chap
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // < Page
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Page selector
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Fit mode combo
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Page >
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Chap >>
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Fullscreen
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title text

            _readerPrevChapterButton = CreateReaderMiniButton("<< Chap", ReaderPrevChapter_Click, 54);
            _readerPrevPageButton = CreateReaderMiniButton("< Page", ReaderPrevPage_Click, 54);
            _readerPageCombo = new ComboBox
            {
                Style = TryFindResource("CyberpunkComboBox") as Style,
                ItemContainerStyle = TryFindResource("CyberpunkComboBoxItemStyle") as Style,
                Width = 110,
                Height = 22,
                Margin = new Thickness(0, 0, 6, 4),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                DisplayMemberPath = "DisplayLabel"
            };
            _readerPageCombo.SelectionChanged += ReaderPageCombo_SelectionChanged;

            _readerCurrentTitleText = new TextBlock
            {
                Foreground = (Brush)TryFindResource("CyberpunkTextBrush"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(8, 0, 8, 4)
            };

            _readerFitCombo = new ComboBox
            {
                Style = TryFindResource("CyberpunkComboBox") as Style,
                ItemContainerStyle = TryFindResource("CyberpunkComboBoxItemStyle") as Style,
                Width = 92,
                Height = 22,
                Margin = new Thickness(0, 0, 6, 4),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            _readerFitCombo.Items.Add("Fit width");
            _readerFitCombo.Items.Add("Fit height");
            _readerFitCombo.Items.Add("Actual");
            _readerFitCombo.Items.Add("Cuộn dọc");
            _readerFitCombo.Items.Add("left → right");
            _readerFitCombo.Items.Add("left ← right");
            _readerFitCombo.SelectedIndex = 0;
            _readerFitCombo.SelectionChanged += ReaderFitCombo_SelectionChanged;

            _readerNextPageButton = CreateReaderMiniButton("Page >", ReaderNextPage_Click, 54);
            _readerNextChapterButton = CreateReaderMiniButton("Chap >>", ReaderNextChapter_Click, 54);
            _readerFullscreenButton = CreateReaderMiniButton("Fullscreen", ReaderFullscreen_Click, 72);

            Grid.SetColumn(_readerPrevChapterButton, 0);
            Grid.SetColumn(_readerPrevPageButton, 1);
            Grid.SetColumn(_readerPageCombo, 2);
            Grid.SetColumn(_readerFitCombo, 3);
            Grid.SetColumn(_readerNextPageButton, 4);
            Grid.SetColumn(_readerNextChapterButton, 5);
            Grid.SetColumn(_readerFullscreenButton, 6);
            Grid.SetColumn(_readerCurrentTitleText, 7);

            topToolbar.Children.Add(_readerPrevChapterButton);
            topToolbar.Children.Add(_readerPrevPageButton);
            topToolbar.Children.Add(_readerPageCombo);
            topToolbar.Children.Add(_readerFitCombo);
            topToolbar.Children.Add(_readerNextPageButton);
            topToolbar.Children.Add(_readerNextChapterButton);
            topToolbar.Children.Add(_readerFullscreenButton);
            topToolbar.Children.Add(_readerCurrentTitleText);

            _readerWebView = new WebView2
            {
                Margin = new Thickness(0, 14, 0, 14),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _readerStatusText = new TextBlock
            {
                Foreground = (Brush)TryFindResource("CyberpunkMutedTextBrush"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };

            Grid.SetRow(topToolbar, 0);
            Grid.SetRow(_readerWebView, 1);
            Grid.SetRow(_readerStatusText, 2);
            viewerGrid.Children.Add(topToolbar);
            viewerGrid.Children.Add(_readerWebView);
            viewerGrid.Children.Add(_readerStatusText);

            Grid.SetColumn(_readerSidebarBorder, 0);
            Grid.SetColumn(viewerCard, 2);
            _readerRootGrid.Children.Add(_readerSidebarBorder);
            _readerRootGrid.Children.Add(viewerCard);

            UpdateReaderStatus(_isVietnameseUi
                ? "Bấm Refresh library để quét thư mục tải và bắt đầu đọc."
                : "Use Refresh library to scan the download root and start reading.");
            UpdateReaderNavigationState();

            return _readerRootGrid;
        }

        private Button CreateReaderMiniButton(string text, RoutedEventHandler clickHandler, double minWidth = 54)
        {
            var button = new Button
            {
                Content = text,
                Style = TryFindResource("CompactCyanButton") as Style,
                Margin = new Thickness(0, 0, 6, 4),
                MinWidth = minWidth
            };

            button.Click += clickHandler;
            return button;
        }

        private async void EnsureReaderReady()
        {
            if (_readerWebViewReady || _readerWebView == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(PortablePaths.WebView2UserDataFolder);
                var env = await CoreWebView2Environment.CreateAsync(
                    null,
                    PortablePaths.WebView2UserDataFolder,
                    new CoreWebView2EnvironmentOptions());
                await _readerWebView.EnsureCoreWebView2Async(env);
                if (_readerWebView.CoreWebView2 != null)
                {
                    _readerWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                    _readerWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                    _readerWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                    _readerWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                }

                _readerWebViewReady = true;
                if (_currentReaderPage != null)
                {
                    RenderReaderPage();
                }
            }
            catch (Exception ex)
            {
                UpdateReaderStatus((_isVietnameseUi ? "Không thể khởi tạo WebView2: " : "Failed to initialize WebView2: ") + ex.Message);
            }
        }

        private async void RefreshReaderLibraryIfNeeded(bool forceRefresh)
        {
            if (_readerMangaList == null)
            {
                return;
            }

            string root = txtDownloadPath != null && !string.IsNullOrWhiteSpace(txtDownloadPath.Text)
                ? txtDownloadPath.Text.Trim()
                : PortablePaths.DefaultDownloadRoot;

            if (!forceRefresh && string.Equals(root, _lastReaderLibraryRoot, StringComparison.OrdinalIgnoreCase) && _readerLibrary.Count > 0)
            {
                return;
            }

            UpdateReaderStatus(_isVietnameseUi ? "Đang quét thư mục manga..." : "Scanning manga library...");

            List<ReaderMangaItem> library = await Task.Run(() => ScanReaderLibrary(root));
            _lastReaderLibraryRoot = root;
            _readerLibrary = library;
            _readerMangaList.ItemsSource = _readerLibrary;
            _readerSummaryText.Text = _isVietnameseUi
                ? $"Root: {root}\nTìm thấy {_readerLibrary.Count} manga."
                : $"Root: {root}\nFound {_readerLibrary.Count} manga.";

            if (_readerLibrary.Count == 0)
            {
                _readerChapterList.ItemsSource = null;
                _readerPageCombo.ItemsSource = null;
                _currentReaderManga = null;
                _currentReaderChapter = null;
                _currentReaderPage = null;
                RenderReaderPlaceholder();
                UpdateReaderNavigationState();
                UpdateReaderStatus(_isVietnameseUi
                    ? "Chưa tìm thấy thư mục manga hợp lệ trong download root."
                    : "No valid manga folders were found in the current download root.");
                return;
            }

            ReaderMangaItem selectedManga = _currentReaderManga != null
                ? _readerLibrary.FirstOrDefault(item => string.Equals(item.FolderPath, _currentReaderManga.FolderPath, StringComparison.OrdinalIgnoreCase))
                : _readerLibrary.First();

            OpenReaderManga(selectedManga, keepChapterSelection: true);
        }

        private List<ReaderMangaItem> ScanReaderLibrary(string root)
        {
            var result = new List<ReaderMangaItem>();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return result;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string firstLevelDir in Directory.GetDirectories(root))
            {
                TryAddReaderBook(firstLevelDir, null, seen, result);

                foreach (string secondLevelDir in Directory.GetDirectories(firstLevelDir))
                {
                    TryAddReaderBook(secondLevelDir, Path.GetFileName(firstLevelDir), seen, result);
                }
            }

            return result
                .OrderBy(item => item.SourceGroup ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, _readerSortComparer)
                .ToList();
        }

        private void TryAddReaderBook(string folderPath, string sourceGroup, ISet<string> seen, ICollection<ReaderMangaItem> result)
        {
            if (seen.Contains(folderPath))
            {
                return;
            }

            bool folderHasImages = DirectoryContainsImages(folderPath);
            bool hasChapterDirectories = Directory.GetDirectories(folderPath).Any(DirectoryContainsImages);
            if (!folderHasImages && !hasChapterDirectories)
            {
                return;
            }

            ReaderMangaItem book = BuildReaderMangaItem(folderPath, sourceGroup);
            if (book == null || book.Chapters.Count == 0)
            {
                return;
            }

            seen.Add(folderPath);
            result.Add(book);
        }

        private ReaderMangaItem BuildReaderMangaItem(string folderPath, string sourceGroup)
        {
            var chapters = new List<ReaderChapterItem>();

            if (DirectoryContainsImages(folderPath))
            {
                chapters.Add(BuildReaderChapterItem(folderPath, Path.GetFileName(folderPath)));
            }
            else
            {
                foreach (string chapterDir in Directory.GetDirectories(folderPath).OrderBy(path => Path.GetFileName(path), _readerSortComparer))
                {
                    if (!DirectoryContainsImages(chapterDir))
                    {
                        continue;
                    }

                    var chapter = BuildReaderChapterItem(chapterDir, Path.GetFileName(chapterDir));
                    if (chapter != null)
                    {
                        chapters.Add(chapter);
                    }
                }
            }

            if (chapters.Count == 0)
            {
                return null;
            }

            return new ReaderMangaItem
            {
                Name = Path.GetFileName(folderPath),
                SourceGroup = sourceGroup,
                FolderPath = folderPath,
                Chapters = chapters
            };
        }

        private ReaderChapterItem BuildReaderChapterItem(string folderPath, string chapterName)
        {
            string[] pageFiles = Directory.GetFiles(folderPath)
                .Where(IsSupportedReaderImageFile)
                .OrderBy(path => Path.GetFileNameWithoutExtension(path), _readerSortComparer)
                .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (pageFiles.Length == 0)
            {
                return null;
            }

            var pages = pageFiles
                .Select((path, index) => new ReaderPageItem
                {
                    Index = index,
                    Name = Path.GetFileName(path),
                    FilePath = path
                })
                .ToList();

            return new ReaderChapterItem
            {
                Name = chapterName,
                FolderPath = folderPath,
                Pages = pages
            };
        }

        private static bool DirectoryContainsImages(string folderPath)
        {
            try
            {
                return Directory.EnumerateFiles(folderPath).Any(IsSupportedReaderImageFile);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSupportedReaderImageFile(string filePath)
        {
            string ext = Path.GetExtension(filePath);
            return string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ext, ".gif", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ext, ".bmp", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ext, ".webp", StringComparison.OrdinalIgnoreCase);
        }

        private void ReaderMangaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_readerSelectionGuard || !(_readerMangaList.SelectedItem is ReaderMangaItem manga))
            {
                return;
            }

            OpenReaderManga(manga, keepChapterSelection: false);
        }

        private void ReaderChapterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_readerSelectionGuard || !(_readerChapterList.SelectedItem is ReaderChapterItem chapter))
            {
                return;
            }

            OpenReaderChapter(chapter, 0);
        }

        private void ReaderPageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_readerSelectionGuard || !(_readerPageCombo.SelectedItem is ReaderPageItem page))
            {
                return;
            }

            OpenReaderPage(page);
        }

        private void OpenReaderManga(ReaderMangaItem manga, bool keepChapterSelection)
        {
            if (manga == null)
            {
                return;
            }

            _currentReaderManga = manga;
            _readerSelectionGuard = true;
            _readerMangaList.SelectedItem = manga;
            _readerChapterList.ItemsSource = manga.Chapters;

            ReaderChapterItem nextChapter = keepChapterSelection && _currentReaderChapter != null
                ? manga.Chapters.FirstOrDefault(item => string.Equals(item.FolderPath, _currentReaderChapter.FolderPath, StringComparison.OrdinalIgnoreCase))
                : manga.Chapters.FirstOrDefault();

            _readerSelectionGuard = false;
            OpenReaderChapter(nextChapter ?? manga.Chapters.FirstOrDefault(), 0);
        }

        private void OpenReaderChapter(ReaderChapterItem chapter, int pageIndex)
        {
            if (chapter == null)
            {
                return;
            }

            _currentReaderChapter = chapter;
            _readerSelectionGuard = true;
            _readerChapterList.SelectedItem = chapter;
            _readerPageCombo.ItemsSource = chapter.Pages;
            _readerSelectionGuard = false;

            int safePageIndex = Math.Max(0, Math.Min(chapter.Pages.Count - 1, pageIndex));
            OpenReaderPage(chapter.Pages[safePageIndex]);
        }

        private void OpenReaderPage(ReaderPageItem page)
        {
            if (page == null)
            {
                return;
            }

            _currentReaderPage = page;
            _readerSelectionGuard = true;
            _readerPageCombo.SelectedItem = page;
            _readerSelectionGuard = false;

            _readerCurrentTitleText.Text = $"{_currentReaderManga?.Name} · {_currentReaderChapter?.Name}";
            RenderReaderPage();
            UpdateReaderNavigationState();
            UpdateReaderStatus(BuildReaderStatusText());
        }

        private string BuildReaderStatusText()
        {
            if (_currentReaderManga == null || _currentReaderChapter == null || _currentReaderPage == null)
            {
                return _isVietnameseUi ? "Chưa mở trang nào." : "No page is currently open.";
            }

            int chapterIndex = _currentReaderManga.Chapters.FindIndex(item => ReferenceEquals(item, _currentReaderChapter)) + 1;
            return _isVietnameseUi
                ? $"{_currentReaderManga.Name} · chap {chapterIndex}/{_currentReaderManga.Chapters.Count} · trang {_currentReaderPage.Index + 1}/{_currentReaderChapter.Pages.Count}"
                : $"{_currentReaderManga.Name} · chapter {chapterIndex}/{_currentReaderManga.Chapters.Count} · page {_currentReaderPage.Index + 1}/{_currentReaderChapter.Pages.Count}";
        }

        private void UpdateReaderStatus(string text)
        {
            if (_readerStatusText != null)
            {
                _readerStatusText.Text = text;
            }
        }

        private void RenderReaderPage()
        {
            if (!_readerWebViewReady || _readerWebView == null)
            {
                EnsureReaderReady();
                return;
            }

            if (_currentReaderPage == null)
            {
                RenderReaderPlaceholder();
                return;
            }

            try
            {
                string tempDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".tmp");
                if (!System.IO.Directory.Exists(tempDir))
                {
                    System.IO.Directory.CreateDirectory(tempDir);
                }
                string tempFile = System.IO.Path.Combine(tempDir, "reader.html");
                
                string html;
                bool isScrollMode = _readerFitMode == ReaderFitMode.VerticalScroll ||
                                   _readerFitMode == ReaderFitMode.HorizontalScrollLTR ||
                                   _readerFitMode == ReaderFitMode.HorizontalScrollRTL;

                if (isScrollMode && _currentReaderChapter != null)
                {
                    var pageUris = new List<string>();
                    foreach (var p in _currentReaderChapter.Pages)
                    {
                        pageUris.Add(new Uri(p.FilePath).AbsoluteUri);
                    }
                    html = BuildReaderHtmlForChapter(pageUris, _readerFitMode);
                }
                else
                {
                    string pageUri = new Uri(_currentReaderPage.FilePath).AbsoluteUri;
                    html = BuildReaderHtml(pageUri, _readerFitMode);
                }

                System.IO.File.WriteAllText(tempFile, html, System.Text.Encoding.UTF8);
                _readerWebView.CoreWebView2.Navigate(new Uri(tempFile).AbsoluteUri);

                if (_fullscreenWindow != null)
                {
                    _fullscreenWindow.RenderPage();
                }
            }
            catch
            {
                bool isScrollMode = _readerFitMode == ReaderFitMode.VerticalScroll ||
                                   _readerFitMode == ReaderFitMode.HorizontalScrollLTR ||
                                   _readerFitMode == ReaderFitMode.HorizontalScrollRTL;

                if (isScrollMode && _currentReaderChapter != null)
                {
                    var pageUris = new List<string>();
                    foreach (var p in _currentReaderChapter.Pages)
                    {
                        pageUris.Add(new Uri(p.FilePath).AbsoluteUri);
                    }
                    _readerWebView.NavigateToString(BuildReaderHtmlForChapter(pageUris, _readerFitMode));
                }
                else
                {
                    string pageUri = new Uri(_currentReaderPage.FilePath).AbsoluteUri;
                    _readerWebView.NavigateToString(BuildReaderHtml(pageUri, _readerFitMode));
                }

                if (_fullscreenWindow != null)
                {
                    _fullscreenWindow.RenderPage();
                }
            }
        }

        private void RenderReaderPlaceholder()
        {
            if (!_readerWebViewReady || _readerWebView == null)
            {
                return;
            }

            string message = _isVietnameseUi
                ? "Chọn manga và chapter ở cột trái để bắt đầu đọc."
                : "Choose a manga and chapter from the left panel to start reading.";
            _readerWebView.NavigateToString(BuildReaderPlaceholderHtml(message));
        }

        private static string BuildReaderPlaceholderHtml(string message)
        {
            return "<html><body style=\"margin:0;background:#0d121f;color:#d7e2ec;font-family:Segoe UI;display:flex;align-items:center;justify-content:center;height:100vh;\">" +
                   "<div style=\"max-width:460px;text-align:center;line-height:1.6;padding:24px;\">" +
                   EscapeHtml(message).Replace("\n", "<br/>") +
                   "</div></body></html>";
        }

        private static string BuildReaderHtml(string pageUri, ReaderFitMode fitMode)
        {
            string imageStyle;
            switch (fitMode)
            {
                case ReaderFitMode.FitHeight:
                    imageStyle = "height:calc(100vh - 24px); width:auto; max-width:none;";
                    break;
                case ReaderFitMode.ActualSize:
                    imageStyle = "width:auto; height:auto; max-width:none;";
                    break;
                default:
                    imageStyle = "width:min(100%, 100vw - 24px); height:auto; max-width:none;";
                    break;
            }

            return "<html><body style=\"margin:0;background:#06090f;overflow:auto;display:flex;justify-content:center;align-items:flex-start;\">" +
                   $"<img src=\"{EscapeHtml(pageUri)}\" style=\"display:block;{imageStyle}padding:12px 0;\"/>" +
                   "</body></html>";
        }

        private static string EscapeHtml(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private void UpdateReaderNavigationState()
        {
            bool hasCurrentChapter = _currentReaderChapter != null;
            bool hasCurrentPage = _currentReaderPage != null;

            if (_readerPrevChapterButton != null)
            {
                _readerPrevChapterButton.IsEnabled = hasCurrentChapter && GetAdjacentChapter(-1) != null;
            }

            if (_readerNextChapterButton != null)
            {
                _readerNextChapterButton.IsEnabled = hasCurrentChapter && GetAdjacentChapter(1) != null;
            }

            if (_readerPrevPageButton != null)
            {
                _readerPrevPageButton.IsEnabled = hasCurrentPage && (GetAdjacentPage(-1) != null || GetAdjacentChapter(-1) != null);
            }

            if (_readerNextPageButton != null)
            {
                _readerNextPageButton.IsEnabled = hasCurrentPage && (GetAdjacentPage(1) != null || GetAdjacentChapter(1) != null);
            }

            UpdateReaderFitButtons();
        }

        private void UpdateReaderFitButtons()
        {
            if (_readerFitCombo == null)
            {
                return;
            }
            int index = 0;
            switch (_readerFitMode)
            {
                case ReaderFitMode.FitWidth:
                    index = 0;
                    break;
                case ReaderFitMode.FitHeight:
                    index = 1;
                    break;
                case ReaderFitMode.ActualSize:
                    index = 2;
                    break;
                case ReaderFitMode.VerticalScroll:
                    index = 3;
                    break;
                case ReaderFitMode.HorizontalScrollLTR:
                    index = 4;
                    break;
                case ReaderFitMode.HorizontalScrollRTL:
                    index = 5;
                    break;
            }
            if (_readerFitCombo.SelectedIndex != index)
            {
                _readerFitCombo.SelectedIndex = index;
            }
        }

        private ReaderPageItem GetAdjacentPage(int direction)
        {
            if (_currentReaderChapter == null || _currentReaderPage == null)
            {
                return null;
            }

            int nextIndex = _currentReaderPage.Index + direction;
            if (nextIndex < 0 || nextIndex >= _currentReaderChapter.Pages.Count)
            {
                return null;
            }

            return _currentReaderChapter.Pages[nextIndex];
        }

        private ReaderChapterItem GetAdjacentChapter(int direction)
        {
            if (_currentReaderManga == null || _currentReaderChapter == null)
            {
                return null;
            }

            int chapterIndex = _currentReaderManga.Chapters.FindIndex(item => ReferenceEquals(item, _currentReaderChapter));
            if (chapterIndex < 0)
            {
                return null;
            }

            int nextIndex = chapterIndex + direction;
            if (nextIndex < 0 || nextIndex >= _currentReaderManga.Chapters.Count)
            {
                return null;
            }

            return _currentReaderManga.Chapters[nextIndex];
        }

        private void ReaderPrevChapter_Click(object sender, RoutedEventArgs e)
        {
            ReaderChapterItem chapter = GetAdjacentChapter(-1);
            if (chapter != null)
            {
                OpenReaderChapter(chapter, 0);
            }
        }

        private void ReaderNextChapter_Click(object sender, RoutedEventArgs e)
        {
            ReaderChapterItem chapter = GetAdjacentChapter(1);
            if (chapter != null)
            {
                OpenReaderChapter(chapter, 0);
            }
        }

        private void ReaderPrevPage_Click(object sender, RoutedEventArgs e)
        {
            MoveReaderPage(-1);
        }

        private void ReaderNextPage_Click(object sender, RoutedEventArgs e)
        {
            MoveReaderPage(1);
        }

        private void MoveReaderPage(int direction)
        {
            ReaderPageItem page = GetAdjacentPage(direction);
            if (page != null)
            {
                OpenReaderPage(page);
                return;
            }

            ReaderChapterItem chapter = GetAdjacentChapter(direction);
            if (chapter == null)
            {
                return;
            }

            int pageIndex = direction < 0 ? Math.Max(0, chapter.Pages.Count - 1) : 0;
            OpenReaderChapter(chapter, pageIndex);
        }

        private void ReaderFitWidth_Click(object sender, RoutedEventArgs e)
        {
            _readerFitMode = ReaderFitMode.FitWidth;
            UpdateReaderFitButtons();
            RenderReaderPage();
        }

        private void ReaderFitHeight_Click(object sender, RoutedEventArgs e)
        {
            _readerFitMode = ReaderFitMode.FitHeight;
            UpdateReaderFitButtons();
            RenderReaderPage();
        }

        private void ReaderActualSize_Click(object sender, RoutedEventArgs e)
        {
            _readerFitMode = ReaderFitMode.ActualSize;
            UpdateReaderFitButtons();
            RenderReaderPage();
        }

        private void ReaderFitCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_readerFitCombo == null) return;
            switch (_readerFitCombo.SelectedIndex)
            {
                case 0:
                    _readerFitMode = ReaderFitMode.FitWidth;
                    break;
                case 1:
                    _readerFitMode = ReaderFitMode.FitHeight;
                    break;
                case 2:
                    _readerFitMode = ReaderFitMode.ActualSize;
                    break;
                case 3:
                    _readerFitMode = ReaderFitMode.VerticalScroll;
                    break;
                case 4:
                    _readerFitMode = ReaderFitMode.HorizontalScrollLTR;
                    break;
                case 5:
                    _readerFitMode = ReaderFitMode.HorizontalScrollRTL;
                    break;
            }
            RenderReaderPage();
        }

        private void ReaderFullscreen_Click(object sender, RoutedEventArgs e)
        {
            ToggleReaderFullscreen();
        }

        public void ToggleReaderFullscreen()
        {
            _isReaderFullscreen = !_isReaderFullscreen;

            if (_isReaderFullscreen)
            {
                if (_readerFullscreenButton != null)
                {
                    _readerFullscreenButton.Content = _isVietnameseUi ? "Thoát Full" : "Exit Full";
                }

                _fullscreenWindow = new ReaderFullscreenWindow(this);
                _fullscreenWindow.Show();
            }
            else
            {
                if (_fullscreenWindow != null)
                {
                    _fullscreenWindow.Close();
                }
                if (_readerFullscreenButton != null)
                {
                    _readerFullscreenButton.Content = "Fullscreen";
                }
            }
        }

        internal void OnFullscreenClosed()
        {
            _fullscreenWindow = null;
            _isReaderFullscreen = false;
            if (_readerFullscreenButton != null)
            {
                _readerFullscreenButton.Content = "Fullscreen";
            }
        }

        internal void HandleFullscreenHotkey(KeyEventArgs e)
        {
            HandleReaderHotkeys(e);
        }

        private static string BuildReaderHtmlForChapter(List<string> pageUris, ReaderFitMode fitMode)
        {
            var sb = new StringBuilder();
            if (fitMode == ReaderFitMode.HorizontalScrollLTR)
            {
                sb.Append("<html><body style=\"margin:0;background:#06090f;overflow-y:hidden;overflow-x:auto;display:flex;flex-direction:row;align-items:center;height:100vh;\">");
                foreach (var pageUri in pageUris)
                {
                    sb.Append($"<img src=\"{EscapeHtml(pageUri)}\" style=\"display:block;height:100vh;width:auto;margin:0;padding:0;\"/>");
                }
                sb.Append("</body></html>");
            }
            else if (fitMode == ReaderFitMode.HorizontalScrollRTL)
            {
                sb.Append("<html><body style=\"margin:0;background:#06090f;overflow-y:hidden;overflow-x:auto;display:flex;flex-direction:row-reverse;align-items:center;height:100vh;\">");
                foreach (var pageUri in pageUris)
                {
                    sb.Append($"<img src=\"{EscapeHtml(pageUri)}\" style=\"display:block;height:100vh;width:auto;margin:0;padding:0;\"/>");
                }
                sb.Append("<script>window.onload = function() { window.scrollTo(document.body.scrollWidth, 0); };</script>");
                sb.Append("</body></html>");
            }
            else
            {
                sb.Append("<html><body style=\"margin:0;background:#06090f;overflow-y:auto;overflow-x:hidden;display:flex;flex-direction:column;align-items:center;\">");
                foreach (var pageUri in pageUris)
                {
                    sb.Append($"<img src=\"{EscapeHtml(pageUri)}\" style=\"display:block;width:min(100%, 100vw - 24px);height:auto;margin:0 auto;padding:0;\"/>");
                }
                sb.Append("</body></html>");
            }
            return sb.ToString();
        }

        private void OpenLatestHistoryInReader(object sender, RoutedEventArgs e)
        {
            var latest = _bookmarkManager
                .GetHistory()
                .Where(item => !string.IsNullOrWhiteSpace(item.DownloadPath) && Directory.Exists(item.DownloadPath))
                .OrderByDescending(item => item.DownloadedAt)
                .FirstOrDefault();

            if (latest == null)
            {
                UpdateReaderStatus(_isVietnameseUi
                    ? "Không tìm thấy thư mục history hợp lệ để mở nhanh."
                    : "No valid history folder was found for quick open.");
                return;
            }

            SelectAppSection(AppSection.Watch);
            RefreshReaderLibraryIfNeeded(forceRefresh: true);

            ReaderMangaItem match = _readerLibrary.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(item.Name) &&
                latest.Name != null &&
                item.Name.IndexOf(latest.Name, StringComparison.OrdinalIgnoreCase) >= 0);

            if (match != null)
            {
                OpenReaderManga(match, keepChapterSelection: false);
            }
        }

        private bool HandleReaderHotkeys(KeyEventArgs e)
        {
            if (_currentSection != AppSection.Watch)
            {
                return false;
            }

            if (Keyboard.Modifiers != ModifierKeys.None)
            {
                return false;
            }

            switch (e.Key)
            {
                case Key.Left:
                case Key.PageUp:
                    if (_readerFitMode == ReaderFitMode.VerticalScroll ||
                        _readerFitMode == ReaderFitMode.HorizontalScrollLTR ||
                        _readerFitMode == ReaderFitMode.HorizontalScrollRTL)
                    {
                        return false;
                    }
                    MoveReaderPage(-1);
                    e.Handled = true;
                    return true;

                case Key.Right:
                case Key.PageDown:
                case Key.Space:
                    if (_readerFitMode == ReaderFitMode.VerticalScroll ||
                        _readerFitMode == ReaderFitMode.HorizontalScrollLTR ||
                        _readerFitMode == ReaderFitMode.HorizontalScrollRTL)
                    {
                        return false;
                    }
                    MoveReaderPage(1);
                    e.Handled = true;
                    return true;

                case Key.Up:
                    ReaderPrevChapter_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    return true;

                case Key.Down:
                    ReaderNextChapter_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    return true;

                case Key.D1:
                    ReaderFitWidth_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    return true;

                case Key.D2:
                    ReaderFitHeight_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    return true;

                case Key.D3:
                    ReaderActualSize_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    return true;
            }

            return false;
        }

        private void UpdateReaderLanguage()
        {
            if (_readerHistoryOpenButton != null)
            {
                _readerHistoryOpenButton.Content = _isVietnameseUi ? "Mở history mới nhất" : "Open latest history";
            }

            if (_readerFullscreenButton != null)
            {
                _readerFullscreenButton.Content = _isReaderFullscreen 
                    ? (_isVietnameseUi ? "Thoát Full" : "Exit Full")
                    : "Fullscreen";
            }

            UpdateReaderFitButtons();
            if (_currentReaderPage == null)
            {
                UpdateReaderStatus(_isVietnameseUi
                    ? "Bấm Refresh library để quét thư mục tải và bắt đầu đọc."
                    : "Use Refresh library to scan the download root and start reading.");
            }
            else
            {
                UpdateReaderStatus(BuildReaderStatusText());
            }
        }
    }

    internal class ReaderFullscreenWindow : Window
    {
        private readonly MainWindow _mainWindow;
        private readonly WebView2 _webView;
        private bool _webViewReady;

        public ReaderFullscreenWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            Title = "Manga Reader - Fullscreen";
            Background = new SolidColorBrush(Color.FromRgb(0x06, 0x09, 0x0F));
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            Topmost = true;

            _webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var grid = new Grid();
            grid.Children.Add(_webView);
            Content = grid;

            Loaded += ReaderFullscreenWindow_Loaded;
            Closed += ReaderFullscreenWindow_Closed;
            KeyDown += ReaderFullscreenWindow_KeyDown;
        }

        private async void ReaderFullscreenWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(PortablePaths.WebView2UserDataFolder);
                var env = await CoreWebView2Environment.CreateAsync(
                    null,
                    PortablePaths.WebView2UserDataFolder,
                    new CoreWebView2EnvironmentOptions());
                await _webView.EnsureCoreWebView2Async(env);
                if (_webView.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                    _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                    _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                    _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                }

                _webView.PreviewKeyDown += (s, args) =>
                {
                    if (args.Key == Key.Escape)
                    {
                        Close();
                        args.Handled = true;
                    }
                    else
                    {
                        ReaderFullscreenWindow_KeyDown(this, args);
                    }
                };

                _webViewReady = true;
                RenderPage();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to initialize WebView2 in Fullscreen: " + ex.Message);
            }
        }

        public void RenderPage()
        {
            if (!_webViewReady) return;

            string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".tmp");
            string tempFile = Path.Combine(tempDir, "reader.html");
            if (File.Exists(tempFile))
            {
                _webView.CoreWebView2.Navigate(new Uri(tempFile).AbsoluteUri);
            }
        }

        private void ReaderFullscreenWindow_Closed(object sender, EventArgs e)
        {
            _mainWindow.OnFullscreenClosed();
        }

        private void ReaderFullscreenWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            _mainWindow.HandleFullscreenHotkey(e);
        }
    }
}
