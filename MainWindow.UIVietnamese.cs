using System.Windows;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private void ApplyVietnameseUiText()
        {
            Title = "Comic-GMTPC v1.0 - Tiếng Việt";

            if (txtHeaderTitle != null) txtHeaderTitle.Text = "Comic GMTPC - Tải truyện theo từng bước";
            if (txtLanguageLabel != null) txtLanguageLabel.Text = "ENG";
            if (txtLanguageTarget != null) txtLanguageTarget.Text = "VI";
            if (txtTotalBooksLabel != null) txtTotalBooksLabel.Text = "Tổng truyện: ";

            ApplyUiTextMappings(true);

            if (btnSaveList != null) btnSaveList.Content = "Lưu mặc định";
            if (btnLoadList != null) btnLoadList.Content = "Mở file mặc định";
            if (btnNewList != null) btnNewList.Content = "Danh sách mới";
            if (btnSaveCustomList != null) btnSaveCustomList.Content = "Lưu file khác";
            if (btnLoadCustomList != null) btnLoadCustomList.Content = "Mở file khác";
            if (btnRestoreOrder != null) btnRestoreOrder.Content = "Trả về thứ tự gốc";
            if (btnDuplicateName != null) btnDuplicateName.Content = "Tên trùng";
            if (btnNoLinkViHentai != null) btnNoLinkViHentai.Content = "Không có chapter";
            if (btnReverseOrder != null) btnReverseOrder.Content = "Đảo thứ tự";
            if (btnClearComplete != null) btnClearComplete.Content = "Ẩn truyện đã xong";

            UpdateLatestChapterButtonLabel();
        }
    }
}
