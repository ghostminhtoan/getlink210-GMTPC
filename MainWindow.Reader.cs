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
        private enum ReaderFitMode
        {
            FitWidth,
            FitHeight,
            ActualSize
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
        private ReaderFitMode _readerFitMode = ReaderFitMode.FitWidth;
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
        private Button _readerFitWidthButton;
        private Button _readerFitHeightButton;
        private Button _readerActualSizeButton;
        private Button _readerHistoryOpenButton;

        private FrameworkElement CreateWatchSection()
        {
            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var sidebar = new Border
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
            sidebar.Child = sidebarGrid;

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
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _readerPrevChapterButton = CreateReaderMiniButton("<< Chap", ReaderPrevChapter_Click);
            _readerPrevPageButton = CreateReaderMiniButton("< Page", ReaderPrevPage_Click);
            _readerPageCombo = new ComboBox
            {
                Style = TryFindResource("CyberpunkComboBox") as Style,
                ItemContainerStyle = TryFindResource("CyberpunkComboBoxItemStyle") as Style,
                Width = 180,
                Height = 28,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                DisplayMemberPath = "DisplayLabel"
            };
            _readerPageCombo.SelectionChanged += ReaderPageCombo_SelectionChanged;

            _readerCurrentTitleText = new TextBlock
            {
                Foreground = (Brush)TryFindResource("CyberpunkTextBrush"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(12, 0, 12, 0)
            };

            var fitButtons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left };
            _readerFitWidthButton = CreateReaderMiniButton("Fit width", ReaderFitWidth_Click);
            _readerFitHeightButton = CreateReaderMiniButton("Fit height", ReaderFitHeight_Click);
            _readerActualSizeButton = CreateReaderMiniButton("Actual", ReaderActualSize_Click);
            fitButtons.Children.Add(_readerFitWidthButton);
            fitButtons.Children.Add(_readerFitHeightButton);
            fitButtons.Children.Add(_readerActualSizeButton);

            _readerNextPageButton = CreateReaderMiniButton("Page >", ReaderNextPage_Click);
            _readerNextChapterButton = CreateReaderMiniButton("Chap >>", ReaderNextChapter_Click);

            Grid.SetColumn(_readerPrevChapterButton, 0);
            Grid.SetColumn(_readerPrevPageButton, 1);
            Grid.SetColumn(_readerPageCombo, 2);
            Grid.SetColumn(fitButtons, 3);
            Grid.SetColumn(_readerNextPageButton, 4);
            Grid.SetColumn(_readerNextChapterButton, 5);
            Grid.SetColumn(_readerCurrentTitleText, 6);

            topToolbar.Children.Add(_readerPrevChapterButton);
            topToolbar.Children.Add(_readerPrevPageButton);
            topToolbar.Children.Add(_readerPageCombo);
            topToolbar.Children.Add(fitButtons);
            topToolbar.Children.Add(_readerNextPageButton);
            topToolbar.Children.Add(_readerNextChapterButton);
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

            Grid.SetColumn(sidebar, 0);
            Grid.SetColumn(viewerCard, 2);
            root.Children.Add(sidebar);
            root.Children.Add(viewerCard);

            UpdateReaderStatus(_isVietnameseUi
                ? "Bấm Refresh library để quét thư mục tải và bắt đầu đọc."
                : "Use Refresh library to scan the download root and start reading.");
            UpdateReaderNavigationState();

            return root;
        }

        private Button CreateReaderMiniButton(string text, RoutedEventHandler clickHandler)
        {
            var button = new Button
            {
                Content = text,
                Style = TryFindResource("CompactCyanButton") as Style,
                Margin = new Thickness(0, 0, 8, 8),
                MinWidth = 86
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

            string pageUri = new Uri(_currentReaderPage.FilePath).AbsoluteUri;
            try
            {
                string tempDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".tmp");
                if (!System.IO.Directory.Exists(tempDir))
                {
                    System.IO.Directory.CreateDirectory(tempDir);
                }
                string tempFile = System.IO.Path.Combine(tempDir, "reader.html");
                string html = BuildReaderHtml(pageUri, _readerFitMode);
                System.IO.File.WriteAllText(tempFile, html, System.Text.Encoding.UTF8);
                _readerWebView.CoreWebView2.Navigate(new Uri(tempFile).AbsoluteUri);
            }
            catch
            {
                _readerWebView.NavigateToString(BuildReaderHtml(pageUri, _readerFitMode));
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
            if (_readerFitWidthButton == null)
            {
                return;
            }

            ApplyReaderFitButtonState(_readerFitWidthButton, _readerFitMode == ReaderFitMode.FitWidth);
            ApplyReaderFitButtonState(_readerFitHeightButton, _readerFitMode == ReaderFitMode.FitHeight);
            ApplyReaderFitButtonState(_readerActualSizeButton, _readerFitMode == ReaderFitMode.ActualSize);
        }

        private void ApplyReaderFitButtonState(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            button.Background = isActive
                ? new SolidColorBrush(Color.FromRgb(0x12, 0x22, 0x38))
                : new SolidColorBrush(Color.FromRgb(0x12, 0x25, 0x38));
            button.BorderBrush = isActive
                ? (Brush)TryFindResource("CyberpunkYellowBrush")
                : (Brush)TryFindResource("CyberpunkBorderBrush");
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
                    MoveReaderPage(-1);
                    e.Handled = true;
                    return true;

                case Key.Right:
                case Key.PageDown:
                case Key.Space:
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
}
