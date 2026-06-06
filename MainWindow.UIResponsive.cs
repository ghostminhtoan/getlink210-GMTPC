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

            if (e.NewSize.Width < 900)
            {
                // Portrait / Stacked Mode: Stack Left and Right panels vertically
                if (gridMainContent.ColumnDefinitions.Count > 0)
                {
                    gridMainContent.ColumnDefinitions.Clear();
                }

                if (gridMainContent.RowDefinitions.Count == 0)
                {
                    gridMainContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(350, GridUnitType.Pixel) }); // Left config panel
                    gridMainContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15, GridUnitType.Pixel) });  // Spacing spacer
                    gridMainContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });   // Right output panel
                }

                Grid.SetColumn(tabLeftPanel, 0);
                Grid.SetRow(tabLeftPanel, 0);

                Grid.SetColumn(borderRightPanel, 0);
                Grid.SetRow(borderRightPanel, 2);
            }
            else
            {
                // Landscape / Side-by-Side Mode: Left and Right panels horizontal
                if (gridMainContent.RowDefinitions.Count > 0)
                {
                    gridMainContent.RowDefinitions.Clear();
                }

                if (gridMainContent.ColumnDefinitions.Count == 0)
                {
                    gridMainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(450, GridUnitType.Pixel) });
                    gridMainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15, GridUnitType.Pixel) });
                    gridMainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }

                Grid.SetColumn(tabLeftPanel, 0);
                Grid.SetRow(tabLeftPanel, 0);

                Grid.SetColumn(borderRightPanel, 2);
                Grid.SetRow(borderRightPanel, 0);
            }
        }
    }
}
