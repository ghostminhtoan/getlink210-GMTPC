using System.Windows;

namespace get_link_manga
{
    public partial class MainWindow : Window
    {
        private void ApplyVietnameseUiText()
        {
            Title = "Comic-GMTPC v1.0 - Tiếng Việt";

            if (txtHeaderTitle != null) txtHeaderTitle.Text = string.Empty;
            if (txtHeaderSubtitle != null) txtHeaderSubtitle.Text = string.Empty;
            if (txtHeaderStepPrimary != null) txtHeaderStepPrimary.Text = "BƯỚC 1";
            if (txtHeaderStepPrimaryTitle != null) txtHeaderStepPrimaryTitle.Text = "CHỌN NGUỒN HOẶC DÁN LINK";
            if (txtHeaderStepSecondary != null) txtHeaderStepSecondary.Text = "BƯỚC 2";
            if (txtHeaderStepSecondaryTitle != null) txtHeaderStepSecondaryTitle.Text = "TẢI VỀ";
            if (txtLanguageLabel != null) txtLanguageLabel.Text = "ENG";
            if (txtLanguageTarget != null) txtLanguageTarget.Text = "VI";
            if (txtTotalBooksLabel != null) txtTotalBooksLabel.Text = "Tổng truyện: ";
            if (txtResultsHeader != null) txtResultsHeader.Text = "DANH SÁCH CHỜ TẢI";

            if (btnShutdownMenu != null) btnShutdownMenu.ToolTip = "Tuy chon tat may";
            if (txtShutdownPopupHeader != null) txtShutdownPopupHeader.Text = "TUY CHON TAT MAY";
            if (chkShutdownAfterCompleted != null) chkShutdownAfterCompleted.Content = "tat may sau khi tai xong";
            if (txtShutdownCountdownLabel != null) txtShutdownCountdownLabel.Text = "tat may sau ngay:gio:phut:giay";
            if (txtShutdownDaysLabel != null) txtShutdownDaysLabel.Text = "NGAY";
            if (txtShutdownHoursLabel != null) txtShutdownHoursLabel.Text = "GIO";
            if (txtShutdownMinutesLabel != null) txtShutdownMinutesLabel.Text = "PHUT";
            if (txtShutdownSecondsLabel != null) txtShutdownSecondsLabel.Text = "GIAY";
            if (txtShutdownPopupHint != null) txtShutdownPopupHint.Text = "0:1:30:0 = 1 gio 30 phut. App se dung lenh shutdown cua Windows.";
            if (btnScheduleShutdownTimer != null) btnScheduleShutdownTimer.Content = "HEN GIO";
            if (btnCancelShutdownTimer != null) btnCancelShutdownTimer.Content = "HUY";
            if (btnCloseShutdownPopup != null) btnCloseShutdownPopup.Content = "DONG";

            ApplyUiTextMappings(true);
            ApplyResultsGridHeaderLanguage("CHI TIẾT TRUYỆN", "TRẠNG THÁI", "TIẾN TRÌNH", "XEM");

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
