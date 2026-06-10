using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (gridMainContent == null) return;

            if (e.NewSize.Width < 1120)
            {
                // Portrait / stacked mode
                gridMainContent.ColumnDefinitions.Clear();
                gridMainContent.RowDefinitions.Clear();

                gridMainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                gridMainContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                gridMainContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12, GridUnitType.Pixel) });
                gridMainContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                gridMainContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12, GridUnitType.Pixel) });
                gridMainContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                Grid.SetColumn(headerPanel, 0);
                Grid.SetRow(headerPanel, 0);
                Grid.SetRowSpan(headerPanel, 1);

                Grid.SetColumn(leftPanelHost, 0);
                Grid.SetRow(leftPanelHost, 2);
                Grid.SetRowSpan(leftPanelHost, 1);

                Grid.SetColumn(tabLeftPanel, 0);
                Grid.SetRow(tabLeftPanel, 0);

                Grid.SetColumn(borderRightPanel, 0);
                Grid.SetRow(borderRightPanel, 4);
                Grid.SetRowSpan(borderRightPanel, 1);
            }
            else
            {
                // Landscape / side-by-side mode
                gridMainContent.ColumnDefinitions.Clear();
                gridMainContent.RowDefinitions.Clear();

                gridMainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(520, GridUnitType.Pixel) });
                gridMainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18, GridUnitType.Pixel) });
                gridMainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                gridMainContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                gridMainContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12, GridUnitType.Pixel) });
                gridMainContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                Grid.SetColumn(headerPanel, 0);
                Grid.SetRow(headerPanel, 0);
                Grid.SetRowSpan(headerPanel, 1);

                Grid.SetColumn(leftPanelHost, 0);
                Grid.SetRow(leftPanelHost, 2);
                Grid.SetRowSpan(leftPanelHost, 1);

                Grid.SetColumn(tabLeftPanel, 0);
                Grid.SetRow(tabLeftPanel, 0);

                Grid.SetColumn(borderRightPanel, 0);
                Grid.SetRow(borderRightPanel, 0);
                Grid.SetColumn(borderRightPanel, 2);
                Grid.SetRowSpan(borderRightPanel, 3);
            }

            ApplyAdaptiveLayout(e.NewSize);
        }

        private void ApplyAdaptiveLayout(Size size)
        {
            bool compactWidth = size.Width < 1450;
            bool compactHeight = size.Height < 820;
            bool compactMode = compactWidth || compactHeight;
            bool ultraCompact = size.Width < 1380 || size.Height < 780;
            bool lowResolutionMode = size.Width <= 1360 || size.Height <= 768;

            if (rootLayout != null)
            {
                rootLayout.Margin = ultraCompact
                    ? new Thickness(12)
                    : compactMode
                        ? new Thickness(14)
                        : new Thickness(18);
            }

            if (headerPanel != null)
            {
                headerPanel.Padding = ultraCompact
                    ? new Thickness(10, 8, 10, 8)
                    : compactMode
                        ? new Thickness(12, 10, 12, 10)
                        : new Thickness(16, 14, 16, 14);
                headerPanel.Margin = new Thickness(0);
            }

            if (txtHeaderTitle != null)
            {
                txtHeaderTitle.FontSize = ultraCompact ? 22 : compactMode ? 25 : 28;
            }

            if (txtHeaderSubtitle != null)
            {
                txtHeaderSubtitle.Visibility = ultraCompact ? Visibility.Collapsed : Visibility.Visible;
                txtHeaderSubtitle.FontSize = compactMode ? 12 : 13;
            }

            if (headerStepsPanel != null)
            {
                headerStepsPanel.Visibility = ultraCompact ? Visibility.Collapsed : Visibility.Visible;
            }

            if (headerActionsPanel != null)
            {
                headerActionsPanel.Margin = ultraCompact
                    ? new Thickness(0, 2, 0, 0)
                    : compactMode
                        ? new Thickness(0, 4, 0, 0)
                        : new Thickness(0, 8, 0, 0);
            }

            if (headerUtilityPanel != null)
            {
                headerUtilityPanel.Width = lowResolutionMode ? 176 : ultraCompact ? 190 : compactMode ? 206 : 220;
            }

            if (leftPanelScrollViewer != null)
            {
                leftPanelScrollViewer.Padding = new Thickness(0);
            }

            if (languageCard != null)
            {
                languageCard.Padding = ultraCompact
                    ? new Thickness(8, 6, 8, 6)
                    : new Thickness(10, 7, 10, 7);
            }

            if (gridMainContent != null)
            {
                gridMainContent.Margin = new Thickness(0);

                if (gridMainContent.ColumnDefinitions.Count >= 3)
                {
                    double leftWidth = lowResolutionMode ? 392 : ultraCompact ? 430 : compactMode ? 470 : 520;
                    gridMainContent.ColumnDefinitions[0].Width = new GridLength(leftWidth, GridUnitType.Pixel);
                    gridMainContent.ColumnDefinitions[1].Width = new GridLength(lowResolutionMode ? 10 : ultraCompact ? 12 : 18, GridUnitType.Pixel);
                }

                if (gridMainContent.RowDefinitions.Count >= 2)
                {
                    gridMainContent.RowDefinitions[1].Height = new GridLength(lowResolutionMode ? 8 : 12, GridUnitType.Pixel);
                }
            }

            if (borderRightPanel != null)
            {
                borderRightPanel.Padding = ultraCompact
                    ? new Thickness(14)
                    : compactMode
                        ? new Thickness(16)
                        : new Thickness(20);
            }

            if (txtResultsHeader != null)
            {
                txtResultsHeader.FontSize = ultraCompact ? 16 : 18;
            }

            if (btnStartDownload != null)
            {
                btnStartDownload.FontSize = ultraCompact ? 11 : 12;
                btnStartDownload.MinWidth = ultraCompact ? 82 : 92;
            }

            SetLayoutScale(headerPanel, lowResolutionMode ? 0.92 : compactHeight ? 0.97 : 1.0);
            SetLayoutScale(leftPanelHost, lowResolutionMode ? 0.80 : ultraCompact ? 0.90 : compactMode ? 0.96 : 1.0);
            SetLayoutScale(tabLeftPanel, lowResolutionMode ? 0.94 : ultraCompact ? 0.97 : 1.0);
            SetLayoutScale(tabManga, lowResolutionMode ? 0.92 : ultraCompact ? 0.96 : 1.0);
            SetLayoutScale(tabHentai, lowResolutionMode ? 0.80 : ultraCompact ? 0.90 : 1.0);

            ApplyTabSizing(tabLeftPanel, lowResolutionMode ? 10.0 : ultraCompact ? 10.5 : 11.0,
                lowResolutionMode ? new Thickness(10, 5, 10, 5) : ultraCompact ? new Thickness(11, 6, 11, 6) : new Thickness(12, 7, 12, 7));
            ApplyTabSizing(tabManga, lowResolutionMode ? 9.5 : ultraCompact ? 10.0 : 11.0,
                lowResolutionMode ? new Thickness(8, 4, 8, 4) : ultraCompact ? new Thickness(9, 5, 9, 5) : new Thickness(12, 7, 12, 7));
            ApplyTabSizing(tabHentai, lowResolutionMode ? 8.5 : ultraCompact ? 9.0 : 11.0,
                lowResolutionMode ? new Thickness(6, 4, 6, 4) : ultraCompact ? new Thickness(8, 4, 8, 4) : new Thickness(12, 7, 12, 7));
        }

        private static void SetLayoutScale(FrameworkElement element, double scale)
        {
            if (element == null)
            {
                return;
            }

            if (Math.Abs(scale - 1.0) < 0.001)
            {
                element.LayoutTransform = Transform.Identity;
                return;
            }

            element.LayoutTransform = new ScaleTransform(scale, scale);
        }

        private static void ApplyTabSizing(TabControl tabControl, double fontSize, Thickness padding)
        {
            if (tabControl == null)
            {
                return;
            }

            foreach (var item in tabControl.Items)
            {
                if (item is TabItem tabItem)
                {
                    tabItem.FontSize = fontSize;
                    tabItem.Padding = padding;
                    tabItem.Margin = new Thickness(0, 0, 3, 0);
                }
            }
        }
    }
}
