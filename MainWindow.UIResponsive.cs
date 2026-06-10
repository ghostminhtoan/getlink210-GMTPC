using System;
using System.Windows;
using System.Windows.Controls;

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
                if (gridMainContent.ColumnDefinitions.Count > 0)
                {
                    gridMainContent.ColumnDefinitions.Clear();
                }

                if (gridMainContent.RowDefinitions.Count == 0)
                {
                    gridMainContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(430, GridUnitType.Pixel) });
                    gridMainContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18, GridUnitType.Pixel) });
                    gridMainContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                }

                Grid.SetColumn(tabLeftPanel, 0);
                Grid.SetRow(tabLeftPanel, 0);

                Grid.SetColumn(borderRightPanel, 0);
                Grid.SetRow(borderRightPanel, 2);
            }
            else
            {
                // Landscape / side-by-side mode
                if (gridMainContent.RowDefinitions.Count > 0)
                {
                    gridMainContent.RowDefinitions.Clear();
                }

                if (gridMainContent.ColumnDefinitions.Count == 0)
                {
                    gridMainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(520, GridUnitType.Pixel) });
                    gridMainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18, GridUnitType.Pixel) });
                    gridMainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }

                Grid.SetColumn(tabLeftPanel, 0);
                Grid.SetRow(tabLeftPanel, 0);

                Grid.SetColumn(borderRightPanel, 2);
                Grid.SetRow(borderRightPanel, 0);
            }

            ApplyAdaptiveLayout(e.NewSize);
        }

        private void ApplyAdaptiveLayout(Size size)
        {
            bool compactWidth = size.Width < 1450;
            bool compactHeight = size.Height < 820;
            bool compactMode = compactWidth || compactHeight;
            bool ultraCompact = size.Width < 1380 || size.Height < 780;

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
                    ? new Thickness(14, 12, 14, 12)
                    : compactMode
                        ? new Thickness(16, 14, 16, 14)
                        : new Thickness(18);
                headerPanel.Margin = ultraCompact
                    ? new Thickness(0, 0, 0, 6)
                    : new Thickness(0, 0, 0, 8);
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
                    ? new Thickness(0, 4, 0, 0)
                    : compactMode
                        ? new Thickness(0, 6, 0, 0)
                        : new Thickness(0, 8, 0, 0);
            }

            if (headerUtilityPanel != null)
            {
                headerUtilityPanel.Width = ultraCompact ? 220 : compactMode ? 236 : 260;
            }

            if (languageCard != null)
            {
                languageCard.Padding = ultraCompact
                    ? new Thickness(10, 6, 10, 6)
                    : new Thickness(12, 8, 12, 8);
            }

            if (gridMainContent != null)
            {
                gridMainContent.Margin = ultraCompact
                    ? new Thickness(0, 2, 0, 0)
                    : compactMode
                        ? new Thickness(0, 4, 0, 0)
                        : new Thickness(0, 6, 0, 0);

                if (gridMainContent.ColumnDefinitions.Count >= 3)
                {
                    double leftWidth = ultraCompact ? 430 : compactMode ? 460 : 520;
                    gridMainContent.ColumnDefinitions[0].Width = new GridLength(leftWidth, GridUnitType.Pixel);
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
        }
    }
}
