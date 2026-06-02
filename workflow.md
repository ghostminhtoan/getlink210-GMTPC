# Hướng Dẫn Phát Triển & Quy Tắc Code - Dự Án "get link manga"

Tài liệu này chứa toàn bộ quy tắc code, cấu trúc dự án, hướng dẫn thiết kế giao diện và quy trình làm việc. **Tất cả các Agent AI (như Antigravity, Claude, Cursor, Copilot...) BẮT BUỘC phải đọc file này trước khi thực hiện bất kỳ chỉnh sửa nào.**

---

## 1. Tổng Quan Dự Án
- **Tên dự án:** get link manga
- **Loại ứng dụng:** Windows Desktop Application (WPF)
- **Framework:** .NET Framework 4.8 (C#)
- **Thư viện UI:** HandyControl (v3.5.0)
- **Chủ đề giao diện (Theme):** Cyberpunk (Tông màu tối, hiệu ứng phát sáng Neon)
- **Tính năng chính:** Cào liên kết truyện tranh từ trang HentaiForce, hiển thị danh sách kết quả, sao chép kết quả vào clipboard (chỉ link hoặc dạng bảng), lưu/đọc kết quả từ file Markdown.

---

## 2. Quy Tắc Code & Lập Trình C#
- **Không tự ý khởi tạo `HttpClient` mới:** Bắt buộc sử dụng instance static `_httpClient` được định nghĩa sẵn trong [MainWindow.xaml.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.xaml.cs). Nó đã được cấu hình tự động giải nén (GZip, Deflate) và User-Agent phù hợp.
- **Xử lý bất đồng bộ (Async/Await):** Luôn dùng `async/await` cho các tác vụ I/O (cào web, đọc ghi file) để tránh đơ giao diện.
- **Cơ chế dừng cào (Cancellation):** Các vòng lặp cào dữ liệu phải hỗ trợ `CancellationToken` từ `CancellationTokenSource` (`_cts`). Kiểm tra `token.IsCancellationRequested` để dừng tác vụ kịp thời và đưa nút bấm về trạng thái ban đầu trong khối `finally`.
- **An toàn đa luồng (UI Thread Safety):** Khi muốn tương tác với các control giao diện từ luồng phụ, sử dụng `Dispatcher.Invoke(() => { ... })`.
- **Regex & Trích xuất dữ liệu:**
  - Viết Regex tối ưu và sử dụng các tùy chọn `RegexOptions.IgnoreCase` hoặc `RegexOptions.Singleline` phù hợp.
  - Luôn luôn giải mã HTML Entity của tiêu đề truyện bằng `WebUtility.HtmlDecode()`.

- **Quy tắc phân tách file (MainWindow partial classes):** Tuyệt đối KHÔNG viết code mới hay bổ sung logic trực tiếp vào file [MainWindow.xaml.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.xaml.cs). File này chỉ giữ lại định nghĩa khung lớp, các trường dữ liệu (fields) và hàm khởi tạo (constructors). Mọi logic/tính năng mới hoặc thay đổi phải được đặt trong các file partial tương ứng theo quy chuẩn đặt tên:
  - Logic hệ thống chung (Save, Load, Copy, Clear...): `MainWindow.System*.cs` (ví dụ: [MainWindow.SystemActions.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemActions.cs))
  - Logic theo Tab: `MainWindow.Tab**.cs` (ví dụ: [MainWindow.TabHentaiforce.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.TabHentaiforce.cs))
  - Logic giao diện (Responsive, DataGrid events, context menu...): `MainWindow.UI***.cs` (ví dụ: [MainWindow.UIResponsive.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.UIResponsive.cs), [MainWindow.UIResultsGrid.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.UIResultsGrid.cs))

- **Quy tắc Build:** Luôn luôn cấu hình, biên dịch và chạy dự án ở chế độ **Release** để tối ưu hóa hiệu năng và đảm bảo tính tương thích của HandyControl DLL. Không build hoặc phân phối bản Debug. Khi build mà phát sinh bất kỳ **Warning** hay **Error** nào, Agent BẮT BUỘC phải tự động tìm hiểu, tự sửa code và biên dịch lại liên tục cho tới khi sạch hoàn toàn lỗi và cảnh báo (0 warnings, 0 errors) mới bàn giao.

---

## 3. Quy Tắc Thiết Giao Diện (WPF / XAML)
- **Theme Cyberpunk:** Sử dụng các tài nguyên màu sắc và hiệu ứng được định nghĩa trong [App.xaml](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/App.xaml):
  - **Màu nền:** `CyberpunkDarkBrush` (Màu tối chủ đạo `#0a0d14`)
  - **Màu khung/Panel:** `CyberpunkCardBrush` (`#121622`)
  - **Màu nhấn sáng (Cyan):** `CyberpunkCyanBrush` (`#00f0ff`)
  - **Màu nhấn phụ (Pink):** `CyberpunkPinkBrush` (`#ff007f`)
  - **Màu cảnh báo (Yellow):** `CyberpunkYellowBrush` (`#ffe600`)
  - **Hiệu ứng phát sáng (Neon Glow):** `CyberpunkCyanGlow` và `CyberpunkPinkGlow`.
- **Nút bấm (Button Styles):**
  - Nút màu xanh Cyan: `Style="{StaticResource CyberpunkButtonCyan}"`
  - Nút màu hồng Pink: `Style="{StaticResource CyberpunkButtonPink}"`
- **Khung bao quanh (Border/Cards):** Sử dụng `Style="{StaticResource CyberpunkCard}"` cho các Border bao quanh nhóm chức năng.
- **Tiêu đề chữ:** Sử dụng `Style="{StaticResource CyberpunkHeader}"` cho các tiêu đề văn bản.
- **Quy tắc đặt tên Control:**
  - TextBox: Tiền tố `txt` (Ví dụ: `txtTagUrl`, `txtResultLinks`)
  - Button: Tiền tố `btn` (Ví dụ: `btnScrape`, `btnFetchInfo`)
  - TextBlock / Label: Tiền tố `lbl` (Ví dụ: `lblStatus`, `lblLinkCount`)
  - ProgressBar: `progressBar`

---

## 4. Quy Chuẩn Đọc/Ghi File Markdown (.md)
- **Xuất File:** Khi lưu kết quả, ghi dưới dạng bảng Markdown chuẩn:
  ```markdown
  | No. | Gallery Name | Gallery Link |
  | :--- | :--- | :--- |
  | 1 | Tên truyện | Link |
  ```
- **Xử lý ký tự đặc biệt:** Thay thế ký tự ống `|` trong tiêu đề truyện thành `\|` trước khi lưu vào bảng để tránh hỏng cấu trúc bảng Markdown. Khi load file lên, đổi ngược lại thành `|`.
- **Đọc File:** Bỏ qua các dòng tiêu đề và dòng phân cách bảng một cách an toàn. Chỉ parse những dòng hợp lệ có chứa link bắt đầu bằng `http`.

---

## 5. Lệnh Build & Chạy Dự Án
- **Khôi phục thư viện:** `nuget restore`
- **Build dự án:** `msbuild "get link manga.csproj" /p:Configuration=Release` hoặc dùng Visual Studio.
- **Đường dẫn file thực thi:** `bin\Release\get link manga.exe`
