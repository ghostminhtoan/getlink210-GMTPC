using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<string, ObservableCollection<LightNovelChapterRecord>> _lightNovelChapterMap =
            new Dictionary<string, ObservableCollection<LightNovelChapterRecord>>(StringComparer.OrdinalIgnoreCase);
        private readonly ObservableCollection<LightNovelChapterRecord> _lightNovelChapterView =
            new ObservableCollection<LightNovelChapterRecord>();
        private readonly HashSet<string> _lightNovelChapterWarmupInFlight =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource _lightNovelCopyCts;
        private CancellationTokenSource _lightNovelPreviewCts;
        private CancellationTokenSource _lightNovelScrapeCts;
        private DataGrid _dgLightNovelBooks;
        private ListBox _lbLightNovelChapters;
        private TextBox _txtLightNovelTagUrl;
        private TextBox _txtLightNovelTotalPages;
        private TextBox _txtLightNovelPageFrom;
        private TextBox _txtLightNovelPageTo;
        private TextBox _txtLightNovelSelectedChapter;
        private TextBox _txtLightNovelPlainText;
        private TextBox _txtLightNovelMarkdown;
        private TextBlock _txtLightNovelCount;
        private GalleryItem _selectedLightNovelBook;
        private LightNovelChapterRecord _selectedLightNovelChapter;

        private FrameworkElement CreateLightNovelDownloadSection()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var topCard = new Border
            {
                Background = (Brush)TryFindResource("CyberpunkCardBrush") ?? new SolidColorBrush(Color.FromRgb(0x0D, 0x12, 0x1F)),
                BorderBrush = (Brush)TryFindResource("CyberpunkBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var topGrid = new Grid();
            for (int i = 0; i < 5; i++)
            {
                topGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var actionRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            actionRow.Children.Add(CreateLightNovelActionButton("ANALYZE TARGET PAGE", BtnLightNovelAnalyze_Click, "CompactCyanButton", 170));
            actionRow.Children.Add(CreateLightNovelActionButton("GET LINK", BtnLightNovelGetLink_Click, "CompactCyanButton", 96));
            actionRow.Children.Add(CreateLightNovelActionButton("GET MORE", BtnLightNovelGetMore_Click, "CompactCyanButton", 96));
            actionRow.Children.Add(CreateLightNovelActionButton("PASTE DIRECT LINK", BtnLightNovelPasteDirect_Click, "CompactCyanButton", 148));
            actionRow.Children.Add(CreateLightNovelActionButton("AUTO COPY TEXT CTRL F2", BtnStartLightNovelCopy_Click, "CompactPinkButton", 166));
            actionRow.Children.Add(CreateLightNovelActionButton("STOP COPY TEXT ALT F2", BtnStopLightNovelCopy_Click, "CompactCyanButton", 162));
            actionRow.Children.Add(CreateLightNovelActionButton("CLEAR", BtnClearLightNovelQueue_Click, "CompactCyanButton", 82));
            actionRow.Children.Add(CreateLightNovelActionButton("OPEN FOLDER", BtnOpenLightNovelFolder_Click, "CompactCyanButton", 108));
            Grid.SetRow(actionRow, 0);
            topGrid.Children.Add(actionRow);

            var targetRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            targetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            targetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            targetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            targetRow.Children.Add(new TextBlock
            {
                Text = "TARGET URL",
                Style = TryFindResource("InputLabelStyle") as Style,
                VerticalAlignment = VerticalAlignment.Center
            });
            _txtLightNovelTagUrl = new TextBox
            {
                Style = TryFindResource("SurfaceTextBox") as Style,
                Text = "https://docln.net/the-loai/action"
            };
            Grid.SetColumn(_txtLightNovelTagUrl, 2);
            targetRow.Children.Add(_txtLightNovelTagUrl);
            Grid.SetRow(targetRow, 1);
            topGrid.Children.Add(targetRow);

            var pageRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            pageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            pageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            pageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            pageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            pageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            pageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            pageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            pageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            pageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            pageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            AddPageField(pageRow, 0, "TOTAL", out _txtLightNovelTotalPages, "1");
            AddPageField(pageRow, 4, "FROM", out _txtLightNovelPageFrom, "1");
            AddPageField(pageRow, 8, "TO", out _txtLightNovelPageTo, "1");

            _txtLightNovelCount = new TextBlock
            {
                Foreground = (Brush)TryFindResource("CyberpunkYellowBrush"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_txtLightNovelCount, 12);
            pageRow.Children.Add(_txtLightNovelCount);
            Grid.SetRow(pageRow, 2);
            topGrid.Children.Add(pageRow);

            var pathRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathRow.Children.Add(new TextBlock
            {
                Text = "DOWNLOAD PATH",
                Style = TryFindResource("InputLabelStyle") as Style,
                VerticalAlignment = VerticalAlignment.Center
            });
            var pathBox = new TextBox
            {
                IsReadOnly = true,
                Style = TryFindResource("SurfaceTextBox") as Style
            };
            pathBox.SetBinding(TextBox.TextProperty, new Binding("Text")
            {
                Source = txtDownloadPath,
                Mode = BindingMode.OneWay
            });
            Grid.SetColumn(pathBox, 2);
            pathRow.Children.Add(pathBox);
            Grid.SetRow(pathRow, 3);
            topGrid.Children.Add(pathRow);

            var hint = new TextBlock
            {
                Text = "Text-only lane. Paste a Hako book link, get all child chapter links, copy title + content into plain text, then convert to .md.",
                Foreground = (Brush)TryFindResource("CyberpunkMutedTextBrush"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(hint, 4);
            topGrid.Children.Add(hint);

            topCard.Child = topGrid;
            Grid.SetRow(topCard, 0);
            root.Children.Add(topCard);

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var bookPanel = CreateLightNovelPanel("PANEL 1 · BOOK");
            _dgLightNovelBooks = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                EnableRowVirtualization = true,
                EnableColumnVirtualization = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                RowHeaderWidth = 0,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x3D)),
                VerticalGridLinesBrush = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x3D)),
                Foreground = (Brush)TryFindResource("CyberpunkCyanBrush"),
                ColumnHeaderStyle = TryFindResource("CyberpunkDataGridColumnHeader") as Style,
                RowStyle = TryFindResource("CyberpunkDataGridRow") as Style,
                CellStyle = TryFindResource("CyberpunkDataGridCell") as Style,
                Margin = new Thickness(0)
            };
            _dgLightNovelBooks.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "✓",
                Binding = new Binding(nameof(GalleryItem.IsChecked)),
                Width = 38
            });
            _dgLightNovelBooks.Columns.Add(new DataGridTextColumn
            {
                Header = "BOOK",
                Binding = new Binding(nameof(GalleryItem.Name)),
                ElementStyle = CreateLightNovelTextStyle("CyberpunkCyanBrush"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });
            _dgLightNovelBooks.Columns.Add(new DataGridTextColumn
            {
                Header = "STATUS",
                Binding = new Binding(nameof(GalleryItem.DisplayStatusText)),
                ElementStyle = CreateLightNovelTextStyle("CyberpunkCyanBrush"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            _dgLightNovelBooks.Columns.Add(new DataGridTextColumn
            {
                Header = "PROCESS",
                Binding = new Binding(nameof(GalleryItem.CurrentProcess)),
                ElementStyle = CreateLightNovelTextStyle("CyberpunkCyanBrush"),
                Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
            });
            _dgLightNovelBooks.ItemsSource = _lightNovelItems;
            _dgLightNovelBooks.SelectionChanged += DgLightNovelBooks_SelectionChanged;
            SetPanelContent(bookPanel, _dgLightNovelBooks);
            Grid.SetColumn(bookPanel, 0);
            Grid.SetRow(bookPanel, 0);
            bookPanel.Margin = new Thickness(0, 0, 5, 5);
            mainGrid.Children.Add(bookPanel);

            var chapterPanel = CreateLightNovelPanel("PANEL 2 · CHAPTER");
            _lbLightNovelChapters = new ListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = (Brush)TryFindResource("CyberpunkTextBrush"),
                ItemTemplate = BuildLightNovelChapterTemplate()
            };
            _lbLightNovelChapters.SelectionChanged += LbLightNovelChapters_SelectionChanged;
            _lbLightNovelChapters.ItemsSource = _lightNovelChapterView;
            SetPanelContent(chapterPanel, _lbLightNovelChapters);
            Grid.SetColumn(chapterPanel, 1);
            Grid.SetRow(chapterPanel, 0);
            chapterPanel.Margin = new Thickness(5, 0, 0, 5);
            mainGrid.Children.Add(chapterPanel);

            var plainPanel = CreateLightNovelPanel("PANEL 3 · PLAIN TEXT");
            var plainGrid = new Grid();
            plainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            plainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            plainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            _txtLightNovelSelectedChapter = new TextBox
            {
                IsReadOnly = true,
                Style = TryFindResource("SurfaceTextBox") as Style
            };
            Grid.SetRow(_txtLightNovelSelectedChapter, 0);
            plainGrid.Children.Add(_txtLightNovelSelectedChapter);

            _txtLightNovelPlainText = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x12, 0x1A)),
                BorderBrush = (Brush)TryFindResource("CyberpunkBorderBrush"),
                Foreground = (Brush)TryFindResource("CyberpunkTextBrush"),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Padding = new Thickness(10)
            };
            Grid.SetRow(_txtLightNovelPlainText, 2);
            plainGrid.Children.Add(_txtLightNovelPlainText);
            SetPanelContent(plainPanel, plainGrid);
            Grid.SetColumn(plainPanel, 0);
            Grid.SetRow(plainPanel, 1);
            plainPanel.Margin = new Thickness(0, 5, 5, 0);
            mainGrid.Children.Add(plainPanel);

            var markdownPanel = CreateLightNovelPanel("PANEL 4 · .MD");
            _txtLightNovelMarkdown = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x12, 0x1A)),
                BorderBrush = (Brush)TryFindResource("CyberpunkBorderBrush"),
                Foreground = (Brush)TryFindResource("CyberpunkYellowBrush"),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Padding = new Thickness(10)
            };
            SetPanelContent(markdownPanel, _txtLightNovelMarkdown);
            Grid.SetColumn(markdownPanel, 1);
            Grid.SetRow(markdownPanel, 1);
            markdownPanel.Margin = new Thickness(5, 5, 0, 0);
            mainGrid.Children.Add(markdownPanel);

            Grid.SetRow(mainGrid, 1);
            root.Children.Add(mainGrid);

            RefreshLightNovelSummary();
            return root;
        }

        private Button CreateLightNovelActionButton(string text, RoutedEventHandler onClick, string styleKey, double minWidth)
        {
            var button = new Button
            {
                Content = text,
                Style = TryFindResource(styleKey) as Style,
                MinWidth = minWidth,
                FontWeight = FontWeights.Bold
            };
            button.Click += onClick;
            return button;
        }

        private static void SetPanelContent(Border panel, UIElement content)
        {
            if (panel.Child is DockPanel dock)
            {
                dock.Children.Add(content);
            }
        }

        private static DataTemplate BuildLightNovelChapterTemplate()
        {
            var template = new DataTemplate(typeof(LightNovelChapterRecord));

            var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackFactory.SetValue(StackPanel.MarginProperty, new Thickness(0, 0, 0, 6));

            var titleFactory = new FrameworkElementFactory(typeof(TextBlock));
            titleFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(LightNovelChapterRecord.DisplayTitle)));
            titleFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            titleFactory.SetValue(TextBlock.ForegroundProperty, Application.Current.TryFindResource("CyberpunkCyanBrush"));
            stackFactory.AppendChild(titleFactory);

            var previewFactory = new FrameworkElementFactory(typeof(TextBlock));
            previewFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(LightNovelChapterRecord.PreviewText)));
            previewFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            previewFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            previewFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 2, 0, 0));
            stackFactory.AppendChild(previewFactory);

            template.VisualTree = stackFactory;
            return template;
        }

        private static Style CreateLightNovelTextStyle(string brushKey)
        {
            var style = new Style(typeof(TextBlock));
            if (Application.Current?.TryFindResource(brushKey) is Brush brush)
            {
                style.Setters.Add(new Setter(TextBlock.ForegroundProperty, brush));
            }

            return style;
        }

        private Border CreateLightNovelPanel(string title)
        {
            var border = new Border
            {
                BorderBrush = (Brush)TryFindResource("CyberpunkBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Background = (Brush)TryFindResource("CyberpunkCardBrush") ?? new SolidColorBrush(Color.FromRgb(0x0D, 0x12, 0x1F))
            };

            var root = new DockPanel();
            var titleBlock = new TextBlock
            {
                Text = title,
                Foreground = (Brush)TryFindResource("CyberpunkCyanBrush"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(titleBlock, Dock.Top);
            root.Children.Add(titleBlock);
            border.Child = root;
            return border;
        }

        private void AddPageField(Grid host, int startColumn, string label, out TextBox box, string defaultValue)
        {
            var labelBlock = new TextBlock
            {
                Text = label,
                Style = TryFindResource("InputLabelStyle") as Style,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelBlock, startColumn);
            host.Children.Add(labelBlock);

            box = new TextBox
            {
                Text = defaultValue,
                Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x12, 0x1A)),
                BorderBrush = (Brush)TryFindResource("CyberpunkBorderBrush"),
                Foreground = (Brush)TryFindResource("CyberpunkTextBrush"),
                CaretBrush = (Brush)TryFindResource("CyberpunkCyanBrush"),
                Height = 28,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(box, startColumn + 2);
            host.Children.Add(box);
        }

        internal bool AddLightNovelQueueItem(GalleryItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Link))
            {
                return false;
            }

            string key = GetLightNovelItemKey(item);
            if (_lightNovelItems.Any(existing => string.Equals(GetLightNovelItemKey(existing), key, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            item.IsChecked = true;
            item.Status = "Queued";
            item.SourceDomain = item.SourceDomain ?? "ln.hako.vn";
            item.OriginalIndex = _lightNovelItems.Count;
            _lightNovelItems.Add(item);
            RefreshLightNovelSummary();
            return true;
        }

        internal int AddLightNovelQueueItems(IEnumerable<GalleryItem> items)
        {
            if (items == null)
            {
                return 0;
            }

            var existingKeys = new HashSet<string>(
                _lightNovelItems.Select(GetLightNovelItemKey),
                StringComparer.OrdinalIgnoreCase);
            int added = 0;

            foreach (GalleryItem item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Link))
                {
                    continue;
                }

                string key = GetLightNovelItemKey(item);
                if (!existingKeys.Add(key))
                {
                    continue;
                }

                item.IsChecked = true;
                item.Status = "Queued";
                item.SourceDomain = item.SourceDomain ?? "ln.hako.vn";
                item.OriginalIndex = _lightNovelItems.Count;
                _lightNovelItems.Add(item);
                added++;
            }

            if (added > 0)
            {
                RefreshLightNovelSummary();
            }

            return added;
        }

        internal void ClearLightNovelQueue()
        {
            _lightNovelPreviewCts?.Cancel();
            _lightNovelPreviewCts?.Dispose();
            _lightNovelPreviewCts = null;
            _lightNovelItems.Clear();
            _lightNovelChapterMap.Clear();
            _lightNovelChapterView.Clear();
            _selectedLightNovelBook = null;
            _selectedLightNovelChapter = null;
            if (_txtLightNovelSelectedChapter != null) _txtLightNovelSelectedChapter.Text = string.Empty;
            if (_txtLightNovelPlainText != null) _txtLightNovelPlainText.Text = string.Empty;
            if (_txtLightNovelMarkdown != null) _txtLightNovelMarkdown.Text = string.Empty;
            RefreshLightNovelSummary();
        }

        internal void ResetLightNovelChapters(GalleryItem item)
        {
            if (item == null)
            {
                return;
            }

            _lightNovelChapterMap[GetLightNovelItemKey(item)] = new ObservableCollection<LightNovelChapterRecord>();
            if (ReferenceEquals(_selectedLightNovelBook, item))
            {
                RefreshLightNovelDetail(item);
            }
        }

        internal void RecordLightNovelChapterSnapshot(GalleryItem item, string chapterTitle, string plainText, string markdownText, string markdownFilePath)
        {
            if (item == null)
            {
                return;
            }

            string key = GetLightNovelItemKey(item);
            if (!_lightNovelChapterMap.TryGetValue(key, out ObservableCollection<LightNovelChapterRecord> chapters))
            {
                chapters = new ObservableCollection<LightNovelChapterRecord>();
                _lightNovelChapterMap[key] = chapters;
            }

            LightNovelChapterRecord existing = chapters.FirstOrDefault(record =>
                string.Equals(record.ChapterTitle, chapterTitle, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                chapters.Add(new LightNovelChapterRecord
                {
                    ChapterTitle = chapterTitle,
                    ChapterLink = string.Empty,
                    PlainText = plainText,
                    MarkdownText = markdownText,
                    MarkdownFilePath = markdownFilePath
                });
            }
            else
            {
                existing.PlainText = plainText;
                existing.MarkdownText = markdownText;
                existing.MarkdownFilePath = markdownFilePath;
            }

            if (ReferenceEquals(_selectedLightNovelBook, item))
            {
                RefreshLightNovelDetail(item);
            }

            RefreshLightNovelSummary();
        }

        private string GetLightNovelItemKey(GalleryItem item)
        {
            return item?.Link?.Trim() ?? string.Empty;
        }

        private void RefreshLightNovelSummary()
        {
            if (_txtLightNovelCount != null)
            {
                int chapterCount = _lightNovelChapterMap.Values.Sum(value => value.Count);
                _txtLightNovelCount.Text = $"Books:{_lightNovelItems.Count} | Chapters:{chapterCount}";
            }
        }

        private void RefreshLightNovelDetail(GalleryItem item)
        {
            _lightNovelChapterView.Clear();
            _selectedLightNovelChapter = null;

            if (item == null)
            {
                if (_txtLightNovelSelectedChapter != null) _txtLightNovelSelectedChapter.Text = string.Empty;
                if (_txtLightNovelPlainText != null) _txtLightNovelPlainText.Text = string.Empty;
                if (_txtLightNovelMarkdown != null) _txtLightNovelMarkdown.Text = string.Empty;
                return;
            }

            if (_lightNovelChapterMap.TryGetValue(GetLightNovelItemKey(item), out ObservableCollection<LightNovelChapterRecord> chapters))
            {
                foreach (LightNovelChapterRecord chapter in chapters)
                {
                    _lightNovelChapterView.Add(chapter);
                }
            }

            if (_lightNovelChapterView.Count == 0)
            {
                if (item == null)
                {
                    if (_txtLightNovelSelectedChapter != null) _txtLightNovelSelectedChapter.Text = string.Empty;
                    if (_txtLightNovelPlainText != null) _txtLightNovelPlainText.Text = string.Empty;
                    if (_txtLightNovelMarkdown != null) _txtLightNovelMarkdown.Text = string.Empty;
                }
            }
        }

        private void DgLightNovelBooks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedLightNovelBook = _dgLightNovelBooks?.SelectedItem as GalleryItem;
            RefreshLightNovelDetail(_selectedLightNovelBook);

            if (_selectedLightNovelBook != null)
            {
                _ = WarmLightNovelChapterCacheAsync(_selectedLightNovelBook);
            }
        }

        private async void LbLightNovelChapters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedLightNovelChapter = _lbLightNovelChapters?.SelectedItem as LightNovelChapterRecord;

            if (_txtLightNovelSelectedChapter != null)
            {
                _txtLightNovelSelectedChapter.Text = _selectedLightNovelChapter?.ChapterTitle ?? string.Empty;
            }

            if (_txtLightNovelPlainText != null)
            {
                _txtLightNovelPlainText.Text = _selectedLightNovelChapter?.PlainText ?? string.Empty;
            }

            if (_txtLightNovelMarkdown != null)
            {
                _txtLightNovelMarkdown.Text = _selectedLightNovelChapter?.MarkdownText ?? string.Empty;
            }

            if (_selectedLightNovelBook == null || _selectedLightNovelChapter == null)
            {
                return;
            }

            _lightNovelPreviewCts?.Cancel();
            _lightNovelPreviewCts?.Dispose();
            _lightNovelPreviewCts = new CancellationTokenSource();

            try
            {
                await EnsureSelectedLightNovelChapterPreviewAsync(_selectedLightNovelBook, _selectedLightNovelChapter, _lightNovelPreviewCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                HakoLog("Copy preview lỗi: " + ex.Message);
                lblStatus.Text = "Copy chapter thất bại.";
            }
        }

        private void BtnClearLightNovelQueue_Click(object sender, RoutedEventArgs e)
        {
            ClearLightNovelQueue();
            lblStatus.Text = _isVietnameseUi ? "Đã xóa hàng chờ light novel." : "Light novel queue cleared.";
        }

        private void BtnOpenLightNovelFolder_Click(object sender, RoutedEventArgs e)
        {
            string root = txtDownloadPath?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            Directory.CreateDirectory(root);
            if (!ShellFolderLauncher.TryOpenFolder(root, out string error))
            {
                ShowWarning("Không mở được thư mục: " + error, "Thông báo");
            }
        }

        private void BtnLightNovelPasteDirect_Click(object sender, RoutedEventArgs e)
        {
            var window = new DirectDownloadWindow(
                customTitle: "PASTE HAKO LINKS",
                customDescription: "Paste Hako book links or chapter links below. The system will normalize and import them automatically.",
                customExample: "Example:\nhttps://ln.hako.vn/truyen/23391-bi-kip-sinh-ton-tai-hoc-vien/\nhttps://ln.hako.vn/truyen/23391-bi-kip-sinh-ton-tai-hoc-vien/c227326-chuong-01")
            {
                Owner = this
            };

            window.OnImport = links => _ = ImportLightNovelDirectLinksAsync(links);
            window.ShowDialog();
        }

        private void SetLightNovelChapterList(GalleryItem item, IEnumerable<LightNovelChapterRecord> chapters)
        {
            if (item == null)
            {
                return;
            }

            var records = new ObservableCollection<LightNovelChapterRecord>(
                (chapters ?? Enumerable.Empty<LightNovelChapterRecord>())
                .GroupBy(record => record.ChapterLink ?? record.ChapterTitle ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()));

            _lightNovelChapterMap[GetLightNovelItemKey(item)] = records;

            if (ReferenceEquals(_selectedLightNovelBook, item))
            {
                RefreshLightNovelDetail(item);
            }

            RefreshLightNovelSummary();
        }

        private async Task WarmLightNovelChapterCacheAsync(GalleryItem item)
        {
            if (item == null)
            {
                return;
            }

            string key = GetLightNovelItemKey(item);
            lock (_lightNovelChapterWarmupInFlight)
            {
                if (_lightNovelChapterWarmupInFlight.Contains(key))
                {
                    return;
                }

                _lightNovelChapterWarmupInFlight.Add(key);
            }

            try
            {
                if (_lightNovelChapterMap.TryGetValue(key, out ObservableCollection<LightNovelChapterRecord> cached) &&
                    cached.Count > 0 &&
                    !string.IsNullOrWhiteSpace(cached[0].ChapterLink))
                {
                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    if (ReferenceEquals(_selectedLightNovelBook, item))
                    {
                        lblStatus.Text = $"Đang tải danh sách chapter cho {item.Name}";
                        if (_txtLightNovelSelectedChapter != null)
                        {
                            _txtLightNovelSelectedChapter.Text = "Đang tải danh sách chapter...";
                        }
                    }
                });

                List<LightNovelChapterRecord> loaded = await BuildLightNovelChapterRecordsAsync(item, CancellationToken.None, firecrawlOnly: true);
                Dispatcher.Invoke(() => SetLightNovelChapterList(item, loaded));

                Dispatcher.Invoke(() =>
                {
                    if (ReferenceEquals(_selectedLightNovelBook, item))
                    {
                        lblStatus.Text = loaded.Count > 0
                            ? $"Đã tải {loaded.Count} chapter cho {item.Name}"
                            : $"Không tìm thấy chapter cho {item.Name}";
                        if (_txtLightNovelSelectedChapter != null && _lightNovelChapterView.Count == 0)
                        {
                            _txtLightNovelSelectedChapter.Text = string.Empty;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                HakoLog("Warm chapter cache lỗi: " + ex.Message);
            }
            finally
            {
                lock (_lightNovelChapterWarmupInFlight)
                {
                    _lightNovelChapterWarmupInFlight.Remove(key);
                }
            }
        }

        private async Task<List<LightNovelChapterRecord>> BuildLightNovelChapterRecordsAsync(GalleryItem item, CancellationToken token, bool firecrawlOnly = false)
        {
            if (item == null)
            {
                return new List<LightNovelChapterRecord>();
            }

            if (TryParseHakoChapterUrl(item.Link, out _, out _, out _, out _, out string canonicalChapterUrl))
            {
                string chapterTitle = string.IsNullOrWhiteSpace(item.LinkCount) ? item.Name : item.LinkCount;
                return new List<LightNovelChapterRecord>
                {
                    new LightNovelChapterRecord
                    {
                        ChapterTitle = NormalizeChapterLabel(chapterTitle),
                        ChapterLink = canonicalChapterUrl,
                        SequenceIndex = 1
                    }
                };
            }

            string html = null;
            List<string> firecrawlLinks = null;
            if (firecrawlOnly)
            {
                FirecrawlPageSnapshot page = await TryFetchHakoPageByFirecrawlAsync(item.Link, token, preferFastChapterList: true);
                html = page?.Html;
                firecrawlLinks = page?.Links;
            }
            else
            {
                html = await FetchHakoHtmlAsync(item.Link, token);
            }
            if (string.IsNullOrWhiteSpace(html))
            {
                html = string.Empty;
            }
            string detectedTitle = ExtractHakoBookTitle(html);
            if (!string.IsNullOrWhiteSpace(detectedTitle))
            {
                item.Name = FormatGalleryTitle(detectedTitle);
            }

            List<HakoChapterInfo> chapters = ExtractHakoChapterLinks(html, item.Link);
            if ((chapters == null || chapters.Count == 0) && firecrawlLinks != null && firecrawlLinks.Count > 0)
            {
                chapters = ExtractHakoChapterLinksFromFirecrawlLinks(firecrawlLinks, item.Link);
            }

            if (chapters == null)
            {
                return new List<LightNovelChapterRecord>();
            }

            return chapters
                .Select(chapter => new LightNovelChapterRecord
                {
                    ChapterTitle = NormalizeChapterLabel(chapter.Title),
                    ChapterLink = chapter.Link,
                    VolumeTitle = chapter.VolumeTitle,
                    VolumeOrder = chapter.VolumeOrder,
                    SequenceIndex = chapter.SequenceIndex
                })
                .ToList();
        }

        private List<HakoChapterInfo> ExtractHakoChapterLinksFromFirecrawlLinks(IEnumerable<string> links, string bookUrl)
        {
            var chapters = new List<HakoChapterInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Uri baseUri = new Uri(NormalizeHakoUrl(bookUrl));
            string basePath = baseUri.AbsolutePath.TrimEnd('/');

            foreach (string rawLink in links ?? Enumerable.Empty<string>())
            {
                string link = NormalizeHakoUrl(rawLink);
                if (string.IsNullOrWhiteSpace(link))
                {
                    continue;
                }

                if (!TryParseHakoChapterUrl(link, out _, out _, out _, out _, out string canonicalChapterUrl))
                {
                    continue;
                }

                link = canonicalChapterUrl;
                Uri chapterUri = new Uri(link);
                if (!chapterUri.AbsolutePath.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!seen.Add(link))
                {
                    continue;
                }

                string chapterSlug = chapterUri.Segments.LastOrDefault()?.Trim('/');
                string title = HumanizeHakoSlug(chapterSlug);
                chapters.Add(new HakoChapterInfo
                {
                    BookTitle = ExtractHakoBookTitle(string.Empty),
                    Link = link,
                    Title = string.IsNullOrWhiteSpace(title) ? link : title,
                    ChapterNumber = TryExtractHakoChapterNumber(title, link),
                    SequenceIndex = chapters.Count + 1
                });
            }

            return chapters;
        }

        private async Task ImportLightNovelDirectLinksAsync(List<string> links)
        {
            if (links == null || links.Count == 0)
            {
                return;
            }

            int success = 0;
            int failed = 0;
            int total = links.Count;

            try
            {
                for (int i = 0; i < total; i++)
                {
                    string rawLink = links[i].Trim();
                    lblStatus.Text = $"[{i + 1}/{total}] Importing {rawLink}";

                    try
                    {
                        GalleryItem item = await BuildHakoDirectGalleryItemAsync(rawLink);
                        bool added = AddLightNovelQueueItem(item);
                        GalleryItem targetItem = added
                            ? item
                            : _lightNovelItems.FirstOrDefault(existing => string.Equals(existing.Link, item.Link, StringComparison.OrdinalIgnoreCase));

                        if (targetItem != null)
                        {
                            _ = WarmLightNovelChapterCacheAsync(targetItem);
                            if (_dgLightNovelBooks != null)
                            {
                                _dgLightNovelBooks.SelectedItem = targetItem;
                            }
                        }

                        success++;
                        lblStatus.Text = $"[{i + 1}/{total}] Imported {item.Name}";
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        HakoLog($"[Import] Lỗi với '{rawLink}': {ex.Message}");
                    }
                }

                lblStatus.Text = $"Import completed. Success: {success}, Failed: {failed}.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Import failed.";
                HakoLog("Import light novel lỗi: " + ex.Message);
            }
        }

        private async Task CopyLightNovelChapterAsync(GalleryItem item, LightNovelChapterRecord record, string rootFolder, CancellationToken token)
        {
            if (item == null || record == null || string.IsNullOrWhiteSpace(record.ChapterLink))
            {
                return;
            }

            string chapterUrl = NormalizeHakoUrl(record.ChapterLink);
            lblStatus.Text = $"Đang mở WebView2 cho {record.ChapterTitle}";
            string chapterHtml = await TryFetchHakoChapterHtmlViaWebViewAsync(chapterUrl, token);
            if (string.IsNullOrWhiteSpace(chapterHtml))
            {
                HakoLog("WebView2 không lấy được chapter html. Fallback sang Firecrawl/browser lane.");
                chapterHtml = await FetchHakoHtmlAsync(chapterUrl, token);
            }
            string chapterTitle = ExtractHakoTitleTopText(chapterHtml);
            if (string.IsNullOrWhiteSpace(chapterTitle))
            {
                chapterTitle = record.ChapterTitle;
            }

            string bookTitle = item.Name;
            string extractedBookTitle = ExtractHakoBookTitleFromChapterHtml(chapterHtml, chapterUrl);
            if (!string.IsNullOrWhiteSpace(extractedBookTitle))
            {
                bookTitle = extractedBookTitle;
                item.Name = FormatGalleryTitle(extractedBookTitle);
            }

            string plainText = BuildHakoChapterPlainText(chapterHtml);
            var chapterInfo = new HakoChapterInfo
            {
                BookTitle = bookTitle,
                Title = chapterTitle,
                Link = chapterUrl,
                ChapterNumber = TryExtractHakoChapterNumber(chapterTitle, chapterUrl)
            };
            string markdown = BuildHakoChapterMarkdown(item, chapterInfo, chapterHtml);

            string resolvedRoot = GetConfiguredDownloadRoot(rootFolder, item);
            string safeBookTitle = GetCanonicalBookFolderName(item, bookTitle, "hako-book");
            string targetFolder = Path.Combine(resolvedRoot, safeBookTitle);
            Directory.CreateDirectory(targetFolder);

            string normalizedChapterTitle = NormalizeChapterLabel(chapterTitle);
            string chapterFolder = GetHakoVolumeFolderPath(targetFolder, record.VolumeTitle, record.VolumeOrder);
            string chapterFilePath = Path.Combine(chapterFolder, BuildHakoChapterFileName(normalizedChapterTitle, record.SequenceIndex));
            File.WriteAllText(chapterFilePath, markdown, new System.Text.UTF8Encoding(true));

            record.ChapterTitle = normalizedChapterTitle;
            record.PlainText = plainText;
            record.MarkdownText = markdown;
            record.MarkdownFilePath = chapterFilePath;
            RecordLightNovelChapterSnapshot(item, normalizedChapterTitle, plainText, markdown, chapterFilePath);
        }

        private async Task EnsureSelectedLightNovelChapterPreviewAsync(GalleryItem item, LightNovelChapterRecord record, CancellationToken token)
        {
            if (item == null || record == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(record.ChapterLink))
            {
                if (ReferenceEquals(_selectedLightNovelChapter, record))
                {
                    _txtLightNovelPlainText.Text = string.Empty;
                    _txtLightNovelMarkdown.Text = string.Empty;
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(record.PlainText) && !string.IsNullOrWhiteSpace(record.MarkdownText))
            {
                if (ReferenceEquals(_selectedLightNovelChapter, record))
                {
                    _txtLightNovelPlainText.Text = record.PlainText;
                    _txtLightNovelMarkdown.Text = record.MarkdownText;
                }
                return;
            }

            if (_txtLightNovelPlainText != null)
            {
                _txtLightNovelPlainText.Text = "Đang copy text chapter...";
            }

            if (_txtLightNovelMarkdown != null)
            {
                _txtLightNovelMarkdown.Text = "Đang convert .md...";
            }

            lblStatus.Text = $"Đang copy {record.ChapterTitle}";

            string rootFolder = txtDownloadPath?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rootFolder))
            {
                rootFolder = PortablePaths.DefaultDownloadRoot;
            }

            await CopyLightNovelChapterAsync(item, record, rootFolder, token);
            token.ThrowIfCancellationRequested();

            if (ReferenceEquals(_selectedLightNovelChapter, record))
            {
                _txtLightNovelSelectedChapter.Text = record.ChapterTitle ?? string.Empty;
                _txtLightNovelPlainText.Text = record.PlainText ?? string.Empty;
                _txtLightNovelMarkdown.Text = record.MarkdownText ?? string.Empty;
                lblStatus.Text = $"Đã copy xong {record.ChapterTitle}";
            }
        }

        private async Task CopyLightNovelItemAsync(GalleryItem item, string rootFolder, CancellationToken token)
        {
            if (item == null)
            {
                return;
            }

            if (!_lightNovelChapterMap.TryGetValue(GetLightNovelItemKey(item), out ObservableCollection<LightNovelChapterRecord> chapters) ||
                chapters.Count == 0)
            {
                List<LightNovelChapterRecord> loaded = await BuildLightNovelChapterRecordsAsync(item, token, firecrawlOnly: true);
                if (loaded.Count == 0)
                {
                    loaded = await BuildLightNovelChapterRecordsAsync(item, token, firecrawlOnly: false);
                }
                SetLightNovelChapterList(item, loaded);
                chapters = _lightNovelChapterMap.TryGetValue(GetLightNovelItemKey(item), out ObservableCollection<LightNovelChapterRecord> created)
                    ? created
                    : new ObservableCollection<LightNovelChapterRecord>();
            }

            item.TotalChapters = chapters.Count;
            item.CompletedChapters = 0;
            item.CurrentProcess = $"0/{chapters.Count} chapters";

            for (int index = 0; index < chapters.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                LightNovelChapterRecord chapter = chapters[index];
                item.DownloadingChapter = chapter.ChapterTitle;
                item.DownloadingPageProgress = $"{index + 1}/{chapters.Count}";
                item.CurrentProcess = $"{index}/{chapters.Count} chapters";

                await CopyLightNovelChapterAsync(item, chapter, rootFolder, token);

                item.CompletedChapters = index + 1;
                item.CurrentProcess = $"{index + 1}/{chapters.Count} chapters";
            }
        }

        private async void BtnLightNovelAnalyze_Click(object sender, RoutedEventArgs e)
        {
            string rawUrl = _txtLightNovelTagUrl?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                ShowWarning("Vui lòng nhập URL tag của Hako.", "Thông báo");
                return;
            }

            try
            {
                lblStatus.Text = "Đang phân tích trang Hako...";
                string normalizedUrl = NormalizeHakoUrl(rawUrl);
                string html = await FetchHakoHtmlAsync(normalizedUrl, CancellationToken.None);
                int totalPages = ExtractHakoMaxPage(html, normalizedUrl);

                _txtLightNovelTagUrl.Text = normalizedUrl;
                _txtLightNovelTotalPages.Text = totalPages.ToString(CultureInfo.InvariantCulture);
                _txtLightNovelPageTo.Text = totalPages.ToString(CultureInfo.InvariantCulture);
                lblStatus.Text = $"Phân tích xong. Phát hiện {totalPages} trang.";
            }
            catch (Exception ex)
            {
                _txtLightNovelTotalPages.Text = "1";
                _txtLightNovelPageTo.Text = "1";
                lblStatus.Text = "Phân tích thất bại.";
                HakoLog("Lỗi khi phân tích light novel: " + ex.Message);
            }
        }

        private async void BtnLightNovelGetLink_Click(object sender, RoutedEventArgs e)
        {
            await ScrapeLightNovelAsync(true);
        }

        private async void BtnLightNovelGetMore_Click(object sender, RoutedEventArgs e)
        {
            await ScrapeLightNovelAsync(false);
        }

        private async Task ScrapeLightNovelAsync(bool clearExisting)
        {
            if (_lightNovelScrapeCts != null)
            {
                _lightNovelScrapeCts.Cancel();
                return;
            }

            string rawUrl = _txtLightNovelTagUrl?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                ShowWarning("Vui lòng nhập URL tag của Hako.", "Thông báo");
                return;
            }

            if (!int.TryParse(_txtLightNovelPageFrom?.Text, out int pageFrom) || pageFrom < 1)
            {
                ShowWarning("Trang bắt đầu không hợp lệ.", "Thông báo");
                return;
            }

            if (!int.TryParse(_txtLightNovelPageTo?.Text, out int pageTo) || pageTo < pageFrom)
            {
                ShowWarning("Trang kết thúc không hợp lệ.", "Thông báo");
                return;
            }

            _lightNovelScrapeCts = new CancellationTokenSource();
            CancellationToken token = _lightNovelScrapeCts.Token;

            try
            {
                if (clearExisting)
                {
                    ClearLightNovelQueue();
                }

                string baseUrl = NormalizeHakoUrl(rawUrl);
                _txtLightNovelTagUrl.Text = baseUrl;

                int totalPages = pageTo - pageFrom + 1;
                int totalAdded = 0;
                for (int page = pageFrom; page <= pageTo; page++)
                {
                    token.ThrowIfCancellationRequested();
                    string pageUrl = GetHakoTagPageUrl(baseUrl, page);
                    string html = await FetchHakoHtmlAsync(pageUrl, token);
                    int addedThisPage = AddLightNovelQueueItems(ParseHakoGalleryItemsFromHtml(html));
                    totalAdded += addedThisPage;

                    double progress = ((double)(page - pageFrom + 1) / totalPages) * 100;
                    lblStatus.Text = $"Đang quét trang {page}/{pageTo} ({progress:0}%) - +{totalAdded}";
                    await Task.Yield();
                }

                RefreshLightNovelSummary();
                lblStatus.Text = $"Cào Hako hoàn tất. Đã thêm {totalAdded} item.";
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "Đã hủy cào Hako.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Cào Hako thất bại.";
                HakoLog("Lỗi khi cào light novel: " + ex.Message);
            }
            finally
            {
                _lightNovelScrapeCts.Dispose();
                _lightNovelScrapeCts = null;
            }
        }

        private async void BtnStartLightNovelCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_lightNovelCopyCts != null)
            {
                lblStatus.Text = _isVietnameseUi
                    ? "Auto copy text đang chạy. Bấm STOP COPY TEXT ALT F2 để dừng."
                    : "Auto copy text already running. Use STOP COPY TEXT ALT F2 to stop.";
                return;
            }

            string rootFolder = txtDownloadPath?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rootFolder))
            {
                ShowWarning("Vui lòng chọn thư mục lưu trước.", "Thông báo");
                return;
            }

            List<GalleryItem> itemsToCopy = _lightNovelItems.Where(item => item.IsChecked).ToList();
            if (itemsToCopy.Count == 0 && _selectedLightNovelBook != null)
            {
                itemsToCopy.Add(_selectedLightNovelBook);
            }

            if (itemsToCopy.Count == 0)
            {
                ShowWarning("Chưa có book light novel nào để copy text.", "Thông báo");
                return;
            }

            _lightNovelCopyCts = new CancellationTokenSource();
            CancellationToken token = _lightNovelCopyCts.Token;

            try
            {
                lblStatus.Text = _isVietnameseUi ? "Đang copy text light novel..." : "Copying light novel text...";

                foreach (GalleryItem item in itemsToCopy)
                {
                    token.ThrowIfCancellationRequested();
                    item.Errors.Clear();
                    item.ErrorCount = 0;
                    item.Status = "Copying";
                    item.CurrentProcess = "Preparing chapters";

                    try
                    {
                        await CopyLightNovelItemAsync(item, rootFolder, token);
                        item.Status = "Completed";
                        item.DownloadPath = ResolveBestFolderForGalleryItem(item);
                    }
                    catch (OperationCanceledException)
                    {
                        item.Status = "Cancelled";
                        item.CurrentProcess = "Cancelled";
                        throw;
                    }
                    catch (Exception ex)
                    {
                        item.Status = "Error";
                        item.CurrentProcess = ex.Message;
                        item.AddError(item.Name, 0, ex.Message);
                        HakoLog("Copy text lỗi: " + ex.Message);
                    }
                }

                lblStatus.Text = _isVietnameseUi ? "Copy text light novel hoàn tất." : "Light novel text copy completed.";
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = _isVietnameseUi ? "Đã dừng copy text light novel." : "Light novel text copy cancelled.";
            }
            finally
            {
                _lightNovelCopyCts.Dispose();
                _lightNovelCopyCts = null;
                RefreshLightNovelSummary();
            }
        }

        private void BtnStopLightNovelCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_lightNovelCopyCts == null)
            {
                lblStatus.Text = _isVietnameseUi
                    ? "Chưa có tiến trình auto copy text để dừng."
                    : "No auto copy text process is running.";
                return;
            }

            _lightNovelCopyCts.Cancel();
            lblStatus.Text = _isVietnameseUi
                ? "Đang dừng auto copy text..."
                : "Stopping auto copy text...";
        }
    }
}
