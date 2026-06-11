using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private enum AppSection
        {
            ChooseSource,
            Download,
            Watch,
            About,
            Update
        }

        private Grid _shellRootGrid;
        private Border _sectionContentBorder;
        private ContentControl _sectionContentHost;
        private StackPanel _sidebarToolsHost;
        private StackPanel _globalDownloadActionPanel;
        private StackPanel _headerCommandHost;
        private StackPanel _navigationButtonHost;
        private readonly Dictionary<AppSection, Button> _navigationButtons = new Dictionary<AppSection, Button>();
        private FrameworkElement _chooseSourceSection;
        private FrameworkElement _downloadSection;
        private FrameworkElement _watchSection;
        private FrameworkElement _aboutSection;
        private FrameworkElement _updateSection;
        private TextBlock _aboutContentText;
        private TextBlock _updateContentText;
        private AppSection _currentSection = AppSection.ChooseSource;
        private bool _workspaceShellInitialized;

        private void InitializeWorkspaceShell()
        {
            if (_workspaceShellInitialized || gridMainContent == null || headerPanel == null || leftPanelHost == null || borderRightPanel == null)
            {
                return;
            }

            ConfigureHeaderPanelLayout();
            BuildScalePresetCard();
            BuildGlobalDownloadToolbar();
            BuildModernShell();
            ApplyInitialWindowSizing();

            _workspaceShellInitialized = true;
            UpdateWorkspaceShellLanguage();
            SelectAppSection(AppSection.Download);
        }

        private void ConfigureHeaderPanelLayout()
        {
            if (!(headerPanel.Child is Grid headerGrid))
            {
                return;
            }

            txtHeaderTitle.Visibility = Visibility.Visible;

            if (txtHeaderSubtitle != null)
            {
                txtHeaderSubtitle.Visibility = Visibility.Visible;
            }

            if (headerStepsPanel != null)
            {
                headerStepsPanel.Visibility = Visibility.Collapsed;
            }

            while (headerGrid.RowDefinitions.Count < 3)
            {
                headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            if (_headerCommandHost == null)
            {
                _headerCommandHost = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(0, 12, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                };
            }
        }

        private void BuildScalePresetCard()
        {
            if (scaleCard == null)
            {
                return;
            }

            var stack = new StackPanel();

            var titleText = new TextBlock
            {
                Text = _isVietnameseUi ? "TỶ LỆ HIỂN THỊ" : "DISPLAY SCALE",
                Foreground = (Brush)TryFindResource("CyberpunkMutedTextBrush"),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(titleText);

            _dpiPresetCombo = new ComboBox
            {
                Name = "cmbDisplayDpi",
                Style = TryFindResource("CyberpunkComboBox") as Style,
                ItemContainerStyle = TryFindResource("CyberpunkComboBoxItemStyle") as Style,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                ItemsSource = UiZoomPresets.Select(percent => new UiZoomPreset(percent)).ToList()
            };

            _dpiPresetCombo.SelectionChanged += DpiPresetCombo_SelectionChanged;
            stack.Children.Add(_dpiPresetCombo);

            scaleCard.Child = stack;
            UpdateZoomDisplay();
        }

        private void BuildGlobalDownloadToolbar()
        {
            if (_globalDownloadActionPanel == null)
            {
                _globalDownloadActionPanel = new StackPanel
                {
                    Name = "headerDownloadActionsPanel",
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
            }

            if (floatingDownloadActionsHost != null)
            {
                RemoveFromParent(_globalDownloadActionPanel);
                if (!floatingDownloadActionsHost.Children.Contains(_globalDownloadActionPanel))
                {
                    floatingDownloadActionsHost.Children.Add(_globalDownloadActionPanel);
                }
            }

            MoveToolbarElement(txtBuildInfo, new Thickness(8, 0, 12, 0));
            MoveToolbarElement(btnStartDownload, new Thickness(0, 0, 6, 0));
            MoveToolbarElement(btnStopDownload, new Thickness(0, 0, 6, 0));
            MoveToolbarElement(btnRetryErrors, new Thickness(0, 0, 6, 0));
            MoveToolbarElement(btnRetryErrorLog, new Thickness(0, 0, 6, 0));
            MoveToolbarElement(btnShutdownMenu?.Parent as UIElement ?? btnShutdownMenu, new Thickness(0, 0, 6, 0));
        }

        private void CompactHeaderPanelButtons(Panel panel, bool isPrimaryRow)
        {
            // No-op or unused now
        }

        private void MoveToolbarElement(UIElement element, Thickness margin)
        {
            if (element == null || _globalDownloadActionPanel == null)
            {
                return;
            }

            if (element is FrameworkElement frameworkElement)
            {
                RemoveFromParent(frameworkElement);
                frameworkElement.Margin = margin;
                frameworkElement.VerticalAlignment = VerticalAlignment.Center;
                frameworkElement.HorizontalAlignment = HorizontalAlignment.Left;

                Button button = null;
                if (frameworkElement is Button btn)
                {
                    button = btn;
                }
                else if (frameworkElement is Panel p)
                {
                    button = p.Children.OfType<Button>().FirstOrDefault();
                }

                if (button != null)
                {
                    button.MinWidth = ReferenceEquals(button, btnShutdownMenu) ? 32 : 56;
                    button.Height = 20;
                    button.FontSize = ReferenceEquals(button, btnShutdownMenu) ? 12 : 9.0;
                    button.Padding = new Thickness(4, 0, 4, 0);
                    button.VerticalAlignment = VerticalAlignment.Center;
                }

                if (!_globalDownloadActionPanel.Children.Contains(frameworkElement))
                {
                    _globalDownloadActionPanel.Children.Add(frameworkElement);
                }
            }
        }

        private void BuildModernShell()
        {
            gridMainContent.Children.Clear();
            gridMainContent.RowDefinitions.Clear();
            gridMainContent.ColumnDefinitions.Clear();

            gridMainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            gridMainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            gridMainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            gridMainContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            gridMainContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            headerPanel.Visibility = Visibility.Collapsed;

            _shellRootGrid = new Grid();
            _shellRootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _shellRootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var sectionHeader = new Border
            {
                Background = (Brush)TryFindResource("CyberpunkCardBrush") ?? new SolidColorBrush(Color.FromRgb(0x0D, 0x12, 0x1F)),
                BorderBrush = (Brush)TryFindResource("CyberpunkBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var sectionHeaderStack = new StackPanel();
            _sectionTitleText = new TextBlock
            {
                Foreground = (Brush)TryFindResource("CyberpunkTextBrush"),
                FontSize = 18,
                FontWeight = FontWeights.Bold
            };
            _sectionHintText = new TextBlock
            {
                Foreground = (Brush)TryFindResource("CyberpunkMutedTextBrush"),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            sectionHeaderStack.Children.Add(_sectionTitleText);
            sectionHeaderStack.Children.Add(_sectionHintText);
            sectionHeader.Child = sectionHeaderStack;

            _sectionContentBorder = new Border
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };
            _sectionContentHost = new ContentControl();
            _sectionContentBorder.Child = _sectionContentHost;

            Grid.SetRow(sectionHeader, 0);
            Grid.SetRow(_sectionContentBorder, 1);
            _shellRootGrid.Children.Add(sectionHeader);
            _shellRootGrid.Children.Add(_sectionContentBorder);

            Grid.SetColumn(_shellRootGrid, 2);
            Grid.SetRow(_shellRootGrid, 1);
            gridMainContent.Children.Add(_shellRootGrid);

            BuildNavigationRail();
            BuildSectionViews();

            if (floatingDownloadActionsHost != null)
            {
                RemoveFromParent(floatingDownloadActionsHost);
                Grid.SetColumn(floatingDownloadActionsHost, 2);
                Grid.SetRow(floatingDownloadActionsHost, 1);
                Panel.SetZIndex(floatingDownloadActionsHost, 99);
                floatingDownloadActionsHost.Margin = new Thickness(0, 0, 18, 18);
                gridMainContent.Children.Add(floatingDownloadActionsHost);
            }
        }

        private void BuildNavigationRail()
        {
            var navBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x06, 0x09, 0x0F)),
                BorderBrush = (Brush)TryFindResource("CyberpunkBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 10, 12, 10),
                Margin = new Thickness(0)
            };

            var navStack = new StackPanel();
            navBorder.Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = false,
                Padding = new Thickness(0, 0, 4, 0),
                Content = navStack
            };

            var brandTitle = new TextBlock
            {
                Text = "MANGA DESK",
                Foreground = (Brush)TryFindResource("CyberpunkTextBrush"),
                FontSize = 15,
                FontWeight = FontWeights.Bold
            };

            _navigationButtonHost = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            _sidebarToolsHost = new StackPanel { Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };

            navStack.Children.Add(brandTitle);
            navStack.Children.Add(_navigationButtonHost);
            navStack.Children.Add(_sidebarToolsHost);

            AddNavigationButton(AppSection.ChooseSource, "Choose source of paste link");
            AddNavigationButton(AppSection.Download, "Download");
            AddNavigationButton(AppSection.Watch, "Watch");
            AddNavigationButton(AppSection.About, "About");
            AddNavigationButton(AppSection.Update, "Update");

            BuildSidebarToolSections();

            Grid.SetColumn(navBorder, 0);
            Grid.SetRow(navBorder, 0);
            Grid.SetRowSpan(navBorder, 2);
            gridMainContent.Children.Add(navBorder);
        }

        private void AddNavigationButton(AppSection section, string text)
        {
            var button = new Button
            {
                Width = 110,
                Height = 38,
                HorizontalAlignment = HorizontalAlignment.Center,
                Style = TryFindResource("SidebarMenuButton") as Style
            };

            button.Content = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                FontSize = 9.8
            };

            button.Click += (sender, args) => SelectAppSection(section);
            _navigationButtons[section] = button;
            _navigationButtonHost.Children.Add(button);
        }

        private void BuildSidebarToolSections()
        {
            if (_sidebarToolsHost == null)
            {
                return;
            }

            _sidebarToolsHost.Children.Clear();

            if (headerUtilityPanel != null)
            {
                RemoveFromParent(headerUtilityPanel);
                headerUtilityPanel.Margin = new Thickness(0, 0, 0, 8);
                headerUtilityPanel.MinWidth = 0;
                headerUtilityPanel.MaxWidth = double.PositiveInfinity;
                headerUtilityPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                _sidebarToolsHost.Children.Add(headerUtilityPanel);
            }

            if (languageCard != null)
            {
                languageCard.Width = 110;
                languageCard.MinWidth = 0;
                languageCard.HorizontalAlignment = HorizontalAlignment.Center;
            }

            if (scaleCard != null)
            {
                scaleCard.Width = 110;
                scaleCard.MinWidth = 0;
                scaleCard.HorizontalAlignment = HorizontalAlignment.Center;
            }

            if (headerActionsPanel != null)
            {
                RemoveFromParent(headerActionsPanel);
                headerActionsPanel.Margin = new Thickness(0, 0, 0, 8);
                headerActionsPanel.Orientation = Orientation.Vertical;
                headerActionsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                CompactHeaderPanelButtons(headerActionsPanel, true);
                _sidebarToolsHost.Children.Add(headerActionsPanel);
            }
        }

        private void BuildSectionViews()
        {
            _chooseSourceSection = CreateChooseSourceSection();
            _downloadSection = borderRightPanel;
            _watchSection = CreateWatchSection();
            _aboutSection = CreateAboutSection();
            _updateSection = CreateUpdateSection();
        }

        private FrameworkElement CreateChooseSourceSection()
        {
            RemoveFromParent(leftPanelHost);
            return leftPanelHost;
        }

        private FrameworkElement CreateAboutSection()
        {
            var border = new Border
            {
                Background = (Brush)TryFindResource("CyberpunkCardBrush") ?? new SolidColorBrush(Color.FromRgb(0x0D, 0x12, 0x1F)),
                BorderBrush = (Brush)TryFindResource("CyberpunkBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(18)
            };

            _aboutContentText = new TextBlock
            {
                Foreground = (Brush)TryFindResource("CyberpunkTextBrush"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };

            border.Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _aboutContentText
            };

            return border;
        }

        private FrameworkElement CreateUpdateSection()
        {
            var root = new StackPanel();

            var card = new Border
            {
                Background = (Brush)TryFindResource("CyberpunkCardBrush") ?? new SolidColorBrush(Color.FromRgb(0x0D, 0x12, 0x1F)),
                BorderBrush = (Brush)TryFindResource("CyberpunkBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(18)
            };

            _updateContentText = new TextBlock
            {
                Foreground = (Brush)TryFindResource("CyberpunkTextBrush"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            card.Child = _updateContentText;

            var buttonRow = new WrapPanel
            {
                Margin = new Thickness(0, 12, 0, 0)
            };
            buttonRow.Children.Add(CreatePathButton("Open app root", PortablePaths.AppRoot));
            buttonRow.Children.Add(CreatePathButton("Open download root", PortablePaths.DefaultDownloadRoot));

            root.Children.Add(card);
            root.Children.Add(buttonRow);

            return root;
        }

        private Button CreatePathButton(string text, string path)
        {
            var button = new Button
            {
                Content = text,
                Style = TryFindResource("CompactCyanButton") as Style,
                MinWidth = 136,
                Tag = path
            };

            button.Click += (sender, args) =>
            {
                string targetPath = button.Tag as string;
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    return;
                }

                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                if (!ShellFolderLauncher.TryOpenFolder(targetPath, out string error))
                {
                    MessageBox.Show($"Cannot open folder: {error}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            return button;
        }

        private void SelectAppSection(AppSection section)
        {
            _currentSection = section;
            UpdateNavigationSelection();
            UpdateSectionHeader();
            PrepareSectionLayout(section);

            switch (section)
            {
                case AppSection.ChooseSource:
                    _sectionContentHost.Content = _chooseSourceSection;
                    break;
                case AppSection.Download:
                    _sectionContentHost.Content = _downloadSection;
                    break;
                case AppSection.Watch:
                    _sectionContentHost.Content = _watchSection;
                    EnsureReaderReady();
                    RefreshReaderLibraryIfNeeded(forceRefresh: false);
                    break;
                case AppSection.About:
                    _sectionContentHost.Content = _aboutSection;
                    break;
                case AppSection.Update:
                    _sectionContentHost.Content = _updateSection;
                    break;
            }
        }

        private void PrepareSectionLayout(AppSection section)
        {
            if (section == AppSection.ChooseSource)
            {
                return;
            }

            RemoveFromParent(borderRightPanel);
        }

        private void UpdateNavigationSelection()
        {
            foreach (var pair in _navigationButtons)
            {
                bool isActive = pair.Key == _currentSection;
                pair.Value.Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(0x12, 0x22, 0x38))
                    : Brushes.Transparent;
                pair.Value.BorderBrush = isActive
                    ? (Brush)TryFindResource("CyberpunkCyanBrush")
                    : (Brush)TryFindResource("CyberpunkBorderBrush");
                pair.Value.Foreground = isActive
                    ? (Brush)TryFindResource("CyberpunkCyanBrush")
                    : (Brush)TryFindResource("CyberpunkTextBrush");
            }
        }

        private void UpdateSectionHeader()
        {
            if (_sectionTitleText == null || _sectionHintText == null)
            {
                return;
            }

            bool isVietnamese = _isVietnameseUi;

            switch (_currentSection)
            {
                case AppSection.ChooseSource:
                    _sectionTitleText.Text = isVietnamese ? "Chọn nguồn hoặc dán link" : "Choose source or paste link";
                    _sectionHintText.Text = isVietnamese
                        ? "Chọn web nguồn bằng thẻ nhanh hoặc dùng form site cũ bên dưới. Toàn bộ parser và paste flow hiện tại được giữ nguyên."
                        : "Pick a source with quick cards or keep using the proven site forms below. Existing parsers and direct-paste flows stay intact.";
                    break;
                case AppSection.Download:
                    _sectionTitleText.Text = isVietnamese ? "Hàng chờ tải" : "Download queue";
                    _sectionHintText.Text = isVietnamese
                        ? "Kiểm tra danh sách, chọn chapter, theo dõi trạng thái, rồi tải hàng loạt với cơ chế resume hiện có."
                        : "Review queue, set chapter filters, track status, and download in bulk with the existing resume-safe pipeline.";
                    break;
                case AppSection.Watch:
                    _sectionTitleText.Text = isVietnamese ? "Đọc truyện offline" : "Watch offline";
                    _sectionHintText.Text = isVietnamese
                        ? "Quét thư mục tải, đọc ảnh ngay trong app, và tự động nhảy qua chapter kế tiếp hoặc trước đó."
                        : "Scan your download root, read images inside the app, and auto-bridge to the next or previous chapter.";
                    break;
                case AppSection.About:
                    _sectionTitleText.Text = isVietnamese ? "Giới thiệu" : "About";
                    _sectionHintText.Text = isVietnamese
                        ? "Tóm tắt luồng dùng app, định dạng hỗ trợ, và các phím tắt chính."
                        : "Quick guide for workflow, supported formats, and main shortcuts.";
                    break;
                case AppSection.Update:
                    _sectionTitleText.Text = isVietnamese ? "Cập nhật" : "Update";
                    _sectionHintText.Text = isVietnamese
                        ? "Xem build hiện tại, thư mục app, và điểm kiểm tra trước khi đóng gói."
                        : "See current build info, app paths, and quick package-check details.";
                    break;
            }
        }

        private static void RemoveFromParent(FrameworkElement element)
        {
            if (element == null)
            {
                return;
            }

            switch (element.Parent)
            {
                case Panel panel:
                    panel.Children.Remove(element);
                    break;
                case Decorator decorator when decorator.Child == element:
                    decorator.Child = null;
                    break;
                case ContentControl contentControl when ReferenceEquals(contentControl.Content, element):
                    contentControl.Content = null;
                    break;
            }
        }

        private void ApplyInitialWindowSizing()
        {
            ApplyPreferredWindowSize();
        }

        private void ApplyPreferredWindowSize()
        {
            Rect workArea = SystemParameters.WorkArea;
            bool portrait = workArea.Height > workArea.Width;

            MinWidth = portrait ? 860 : 1180;
            MinHeight = 720;
            MaxWidth = workArea.Width;
            MaxHeight = workArea.Height;

            double targetWidth;
            double targetHeight;

            if (portrait)
            {
                targetWidth = Math.Min(workArea.Width - 24, 980);
                targetHeight = Math.Min(workArea.Height - 24, 1380);
            }
            else
            {
                targetWidth = Math.Min(workArea.Width - 40, 1440);
                targetHeight = Math.Min(workArea.Height - 30, 920);
            }

            WindowState = WindowState.Normal;
            Width = Math.Max(MinWidth, targetWidth);
            Height = Math.Max(MinHeight, targetHeight);
            Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2.0);
            Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2.0);
        }

        private void UpdateWorkspaceShellLanguage()
        {
            if (_navigationButtons.Count > 0)
            {
                _navigationButtons[AppSection.ChooseSource].Content = _isVietnameseUi ? "Chọn nguồn / dán link" : "Choose source of paste link";
                _navigationButtons[AppSection.Download].Content = _isVietnameseUi ? "Tải về" : "Download";
                _navigationButtons[AppSection.Watch].Content = _isVietnameseUi ? "Xem truyện" : "Watch";
                _navigationButtons[AppSection.About].Content = _isVietnameseUi ? "Giới thiệu" : "About";
                _navigationButtons[AppSection.Update].Content = _isVietnameseUi ? "Cập nhật" : "Update";
            }

            if (txtHeaderTitle != null)
            {
                txtHeaderTitle.Text = _isVietnameseUi ? "Manga Offline Desk" : "Manga Offline Desk";
            }

            if (txtHeaderSubtitle != null)
            {
                txtHeaderSubtitle.Text = _isVietnameseUi
                    ? "Giao diện mới tập trung vào dán link, tải truyện và đọc offline ngay trong app."
                    : "A rebuilt shell focused on paste-link workflows, bulk downloads, and offline reading inside the app.";
            }

            if (_aboutContentText != null)
            {
                _aboutContentText.Text = _isVietnameseUi
                    ? "Ứng dụng này có 3 trục chính:\n\n" +
                      "1. Chọn nguồn hoặc dán link để lấy danh sách truyện.\n" +
                      "2. Quản lý hàng chờ tải với temp path an toàn và progress log hiện có.\n" +
                      "3. Đọc ảnh offline ngay trong app với hỗ trợ jpg, jpeg, png, gif, bmp, webp.\n\n" +
                      "Phím tắt:\n" +
                      "- Ctrl + mouse wheel: đổi DPI preset\n\n" +
                      "Lưu ý:\n" +
                      "- 100% là mức tối ưu cho 1360x768.\n" +
                      "- Reader tự nhảy sang chapter sau hoặc trước ở biên trang.\n" +
                      "- Download root vẫn là nguồn quét mặc định cho Watch."
                    : "This rebuild now centers around three workflows:\n\n" +
                      "1. Choose a source or paste links to collect manga entries.\n" +
                      "2. Manage the download queue with the existing resume-safe temp paths and progress logs.\n" +
                      "3. Read offline images inside the app with jpg, jpeg, png, gif, bmp, and webp support.\n\n" +
                      "Shortcuts:\n" +
                      "- Ctrl + mouse wheel: change DPI preset\n\n" +
                      "Notes:\n" +
                      "- 100% is tuned for 1360x768.\n" +
                      "- Reader auto-bridges across chapter edges.\n" +
                      "- Download root remains the default scan source for Watch.";
            }

            if (_updateContentText != null)
            {
                _updateContentText.Text = (_isVietnameseUi ? "Build hiện tại" : "Current build") + $": {BuildInfo.DisplayText}\n\n" +
                                          (_isVietnameseUi ? "App root" : "App root") + $": {PortablePaths.AppRoot}\n" +
                                          (_isVietnameseUi ? "Download root mặc định" : "Default download root") + $": {PortablePaths.DefaultDownloadRoot}\n" +
                                          (_isVietnameseUi ? "WebView2 portable data" : "Portable WebView2 data") + $": {PortablePaths.WebView2UserDataFolder}\n\n" +
                                          (_isVietnameseUi
                                              ? "Checklist nhanh: build xong, quét Watch, mở thử vài chapter, rồi mới đóng gói."
                                              : "Quick checklist: build clean, refresh Watch, test a few chapter transitions, then package.");
            }

            UpdateReaderLanguage();
            UpdateSectionHeader();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized || WindowState == WindowState.Normal)
            {
                ApplyAdaptiveLayout(new Size(ActualWidth, ActualHeight));
            }
        }
    }
}

