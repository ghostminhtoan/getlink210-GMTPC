using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace get_link_manga
{
    public partial class BookmarkHistoryWindow : Window
    {
        private readonly BookmarkHistoryManager _manager;

        public BookmarkHistoryWindow()
        {
            InitializeComponent();
            _manager = new BookmarkHistoryManager();
            RefreshBookmarks();
            RefreshHistory();
        }

        public void RefreshBookmarks()
        {
            var bookmarks = _manager.GetBookmarks();
            lstBookmarks.ItemsSource = bookmarks;
            lblBookmarkCount.Text = $"Tổng: {bookmarks.Count} bookmark(s)";
        }

        public void RefreshHistory()
        {
            var history = _manager.GetHistory();
            lstHistory.ItemsSource = history;
            lblHistoryCount.Text = $"Tổng: {history.Count} mục";
        }

        // ===== BOOKMARKS =====

        private void BtnOpenBookmarkLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string url && !string.IsNullOrEmpty(url))
            {
                OpenUrl(url);
            }
        }

        private void BtnDeleteBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string url && !string.IsNullOrEmpty(url))
            {
                _manager.RemoveBookmark(url);
                RefreshBookmarks();
            }
        }

        private void LstBookmarks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstBookmarks.SelectedItem is BookmarkEntry entry)
            {
                OpenUrl(entry.Url);
            }
        }

        // ===== HISTORY =====

        private void BtnOpenHistoryLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string url && !string.IsNullOrEmpty(url))
            {
                OpenUrl(url);
            }
        }

        private void BtnOpenHistoryFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path && !string.IsNullOrEmpty(path))
            {
                try
                {
                    if (System.IO.Directory.Exists(path))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
                    }
                    else
                    {
                        MessageBox.Show($"Thư mục không tồn tại:\n{path}", "Warning",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể mở thư mục: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Bạn có chắc muốn xóa toàn bộ lịch sử tải?", "Xác nhận",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _manager.ClearHistory();
                RefreshHistory();
            }
        }

        // ===== HELPERS =====

        private void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở link: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
