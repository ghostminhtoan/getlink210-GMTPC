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
            bool isVi = _mainWindow._isVietnameseUi;

            Title = isVi ? "BÁO CÁO LỖI" : "ERROR REPORT";
            lblErrorTitle.Text = isVi ? $"BÁO CÁO LỖI — {queueItem.Name}" : $"ERROR REPORT — {queueItem.Name}";
            lblErrorSubtitle.Text = isVi
                ? $"Nguồn: {queueItem.SourceDomain} | Tổng lỗi theo trang: {uniqueErrors.Count}"
                : $"Source: {queueItem.SourceDomain} | Total errors by page: {uniqueErrors.Count}";

            // Localize column headers
            if (dgErrors.Columns.Count >= 4)
            {
                dgErrors.Columns[1].Header = isVi ? "CHƯƠNG" : "CHAPTER";
                dgErrors.Columns[2].Header = isVi ? "TRANG" : "PAGE";
                dgErrors.Columns[3].Header = isVi ? "LỖI" : "ERROR";
            }

            btnRetryFailed.Content = isVi ? "🔄 THỬ LẠI LỖI" : "🔄 RETRY FAILED";
            btnCopyErrors.Content = isVi ? "📋 SAO CHÉP LỖI" : "📋 COPY ERRORS";
            btnClose.Content = isVi ? "ĐÓNG" : "CLOSE";

            // Create display items from error details
            var displayItems = uniqueErrors.Select(e =>
            {
                string rawMsg = e.ErrorMessage ?? "Unknown error";
                string localizedMsg = rawMsg;
                if (!isVi)
                {
                    // Map common Vietnamese error messages to English
                    if (rawMsg.Contains("Trang bị thiếu (Missing page)"))
                        localizedMsg = "Missing page";
                    else if (rawMsg.Contains("File ảnh hỏng hoặc quá nhỏ"))
                        localizedMsg = "Image corrupt or too small";
                }
                else
                {
                    // Map common English error messages to Vietnamese
                    if (rawMsg.Contains("Missing page"))
                        localizedMsg = "Trang bị thiếu";
                    else if (rawMsg.Contains("Image corrupt or too small"))
                        localizedMsg = "File ảnh hỏng hoặc quá nhỏ";
                }

                return new ErrorDisplayItem
                {
                    Icon = "❌",
                    ChapterName = e.ChapterName ?? "N/A",
                    PageNumber = e.PageNumber,
                    ErrorMessage = localizedMsg,
                    ImageUrl = e.ImageUrl
                };
            }).ToList();

            dgErrors.ItemsSource = displayItems;
        }

        private async void BtnRetryFailed_Click(object sender, RoutedEventArgs e)
        {
            bool isVi = _mainWindow._isVietnameseUi;
            if (_queueItem == null || !_queueItem.GetUniqueErrors().Any())
            {
                MessageBox.Show(
                    isVi ? "Không có lỗi nào để thử lại." : "No errors to retry.",
                    isVi ? "Thông tin" : "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Close();
            await _mainWindow.RetryDownloadQueueItemErrorsAsync(_queueItem);
        }

        private void BtnCopyErrors_Click(object sender, RoutedEventArgs e)
        {
            bool isVi = _mainWindow._isVietnameseUi;
            var uniqueErrors = _queueItem.GetUniqueErrors();
            if (!uniqueErrors.Any())
            {
                MessageBox.Show(
                    isVi ? "Không có lỗi nào để sao chép." : "No errors to copy.",
                    isVi ? "Thông tin" : "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(isVi ? $"Báo cáo lỗi — {_queueItem.Name}" : $"Error Report — {_queueItem.Name}");
            sb.AppendLine(isVi ? $"Nguồn: {_queueItem.Link}" : $"Source: {_queueItem.Link}");
            sb.AppendLine(isVi ? $"Tổng số lỗi: {uniqueErrors.Count}" : $"Total errors: {uniqueErrors.Count}");
            sb.AppendLine(new string('-', 60));

            foreach (var error in uniqueErrors)
            {
                string rawMsg = error.ErrorMessage ?? "Unknown error";
                string localizedMsg = rawMsg;
                if (!isVi)
                {
                    if (rawMsg.Contains("Trang bị thiếu (Missing page)"))
                        localizedMsg = "Missing page";
                    else if (rawMsg.Contains("File ảnh hỏng hoặc quá nhỏ"))
                        localizedMsg = "Image corrupt or too small";
                }
                else
                {
                    if (rawMsg.Contains("Missing page"))
                        localizedMsg = "Trang bị thiếu";
                    else if (rawMsg.Contains("Image corrupt or too small"))
                        localizedMsg = "File ảnh hỏng hoặc quá nhỏ";
                }

                sb.AppendLine(isVi 
                    ? $"❌ {error.ChapterName}, Trang {error.PageNumber} — {localizedMsg}"
                    : $"❌ {error.ChapterName}, Page {error.PageNumber} — {localizedMsg}");

                if (!string.IsNullOrEmpty(error.ImageUrl))
                    sb.AppendLine($"   URL: {error.ImageUrl}");
            }

            Clipboard.SetText(sb.ToString());
            MessageBox.Show(
                isVi ? $"Đã sao chép {uniqueErrors.Count} lỗi vào clipboard." : $"Copied {uniqueErrors.Count} errors to clipboard.",
                isVi ? "Đã sao chép" : "Copied",
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
