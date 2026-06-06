using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace get_link_manga
{
    public partial class ErrorReportWindow : Window
    {
        private readonly GalleryItem _queueItem;
        private readonly MainWindow _mainWindow;

        public ErrorReportWindow(GalleryItem queueItem, MainWindow mainWindow)
        {
            InitializeComponent();
            _queueItem = queueItem;
            _mainWindow = mainWindow;

            var uniqueErrors = queueItem.GetUniqueErrors();

            lblErrorTitle.Text = $"ERROR REPORT — {queueItem.Name}";
            lblErrorSubtitle.Text = $"Source: {queueItem.SourceDomain} | Tổng lỗi theo trang: {uniqueErrors.Count}";

            // Create display items from error details
            var displayItems = uniqueErrors.Select(e => new ErrorDisplayItem
            {
                Icon = "❌",
                ChapterName = e.ChapterName ?? "N/A",
                PageNumber = e.PageNumber,
                ErrorMessage = e.ErrorMessage ?? "Unknown error",
                ImageUrl = e.ImageUrl
            }).ToList();

            dgErrors.ItemsSource = displayItems;
        }

        private async void BtnRetryFailed_Click(object sender, RoutedEventArgs e)
        {
            if (_queueItem == null || !_queueItem.GetUniqueErrors().Any())
            {
                MessageBox.Show("Không có lỗi nào để thử lại.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Close();
            await _mainWindow.RetryDownloadQueueItemErrorsAsync(_queueItem);
        }

        private void BtnCopyErrors_Click(object sender, RoutedEventArgs e)
        {
            var uniqueErrors = _queueItem.GetUniqueErrors();
            if (!uniqueErrors.Any())
            {
                MessageBox.Show("Không có lỗi nào để sao chép.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Error Report — {_queueItem.Name}");
            sb.AppendLine($"Source: {_queueItem.Link}");
            sb.AppendLine($"Total errors: {uniqueErrors.Count}");
            sb.AppendLine(new string('-', 60));

            foreach (var error in uniqueErrors)
            {
                sb.AppendLine($"❌ {error.ChapterName}, Trang {error.PageNumber} — {error.ErrorMessage}");
                if (!string.IsNullOrEmpty(error.ImageUrl))
                    sb.AppendLine($"   URL: {error.ImageUrl}");
            }

            Clipboard.SetText(sb.ToString());
            MessageBox.Show($"Đã sao chép {uniqueErrors.Count} lỗi vào clipboard.", "Copied",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class ErrorDisplayItem
    {
        public string Icon { get; set; }
        public string ChapterName { get; set; }
        public int PageNumber { get; set; }
        public string ErrorMessage { get; set; }
        public string ImageUrl { get; set; }
    }
}
