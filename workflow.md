# Hướng Dẫn Phát Triển & Quy Tắc Code - Dự Án "get link manga"

Tài liệu này chứa toàn bộ quy tắc code, cấu trúc dự án, hướng dẫn thiết kế giao diện và quy trình làm việc. **Tất cả các Agent AI (như Antigravity, Claude, Cursor, Copilot...) BẮT BUỘC phải đọc file này trước khi thực hiện bất kỳ chỉnh sửa nào.**

---

## 0. Mindmap Workflow Cho Agent
```text
WORKFLOW AGENT
|
+-- 1. ĐỌC LUẬT GỐC
|   |
|   +-- Đọc toàn bộ workflow.md trước khi sửa gì
|   +-- Trả lời tiếng Việt có dấu
|   +-- Tôn trọng theme Cyberpunk + quy tắc control/UI
|   +-- Nhớ: không nhét logic mới vào MainWindow.xaml.cs
|
+-- 2. XÁC ĐỊNH LOẠI VIỆC
|   |
|   +-- Sửa UI/XAML?
|   |   `-- MainWindow.xaml / resource đúng chuẩn
|   +-- Sửa logic hệ thống?
|   |   `-- MainWindow.System*.cs
|   +-- Sửa logic tab/site?
|   |   `-- MainWindow.Tab*.cs
|   +-- Sửa hành vi UI?
|       `-- MainWindow.UI*.cs
|
+-- 3. CHECK RÀNG BUỘC KỸ THUẬT
|   |
|   +-- Web/I-O dùng async/await
|   +-- Dùng static _httpClient có sẵn
|   +-- Vòng lặp dài hỗ trợ CancellationToken
|   +-- Đụng UI từ luồng phụ => Dispatcher.Invoke(...)
|   +-- Decode tiêu đề bằng WebUtility.HtmlDecode()
|   +-- Mở folder => chỉ qua MainWindow.SystemExplorer.cs
|   +-- Book list mọi cột phải sort xuôi / ngược được
|   +-- Subfolder là đích tải thật, không được xem là chỗ chứa tmp
|   `-- `.tmp` luôn cố định trong root download
|
+-- 4. NẾU THÊM TAB CÀO MỚI
|   |
|   +-- Thêm TabItem đúng nhóm Manga/Hentai
|   +-- Đặt tên control đúng tiền tố
|   +-- Tạo file MainWindow.Tab[SiteName].cs
|   +-- Viết Analyze + Scrape + log lỗi
|   +-- Chap/file/folder có số < 10 phải zero-pad
|   +-- Nối chuẩn Explorer chung
|
+-- 5. NẾU ĐỤNG FILE MARKDOWN
|   |
|   +-- Xuất bảng markdown chuẩn
|   +-- Escape ký tự |
|   `-- Khi load chỉ parse dòng hợp lệ có http
|
+-- 6. BUILD CHỐT BẮT BUỘC
|   |
|   +-- Chạy .\build.bat
|   +-- Hoặc Release + AutoStampBuildInfo=true + AutoPublishRelease=true
|   +-- Không dừng khi còn warning/error
|   `-- Lặp sửa -> build đến 0 warning, 0 error
|
+-- 7. SAU BUILD THÀNH CÔNG
|   |
|   +-- Kiểm tra restore point Git
|   +-- Confirm exe mới nằm đúng release\Comic-GMTPC\
|   +-- Update log git commit.md
|   |   `-- Commit mới nhất đặt trên cùng
|   `-- Commit + push GitHub
|
`-- 8. BÀN GIAO
    |
    +-- Nêu rõ đã sửa gì
    +-- Nêu rõ đã build sạch chưa
    `-- Nếu còn rủi ro/test thiếu thì nói thẳng
```

### Sơ đồ quyết định nhanh
```text
Bắt đầu
  |
  v
Đọc workflow.md chưa?
  |-- chưa -> đọc hết rồi mới làm
  `-- rồi
        |
        v
Việc này thuộc nhóm nào?
  |-- UI/XAML ----------> sửa MainWindow.xaml
  |-- Logic hệ thống ---> sửa MainWindow.System*.cs
  |-- Logic tab/site ---> sửa MainWindow.Tab*.cs
  `-- UI behavior -----> sửa MainWindow.UI*.cs
        |
        v
Có vi phạm rule kỹ thuật?
  |-- có -> đổi hướng làm cho đúng rule
  `-- không
        |
        v
Build Release chốt
  |-- còn warning/error -> quay lại sửa
  `-- sạch 0/0
        |
        v
Update log git commit -> commit -> push -> bàn giao
```

---

## 1. Tổng Quan Dự Án
- **Quy tắc trả lời:** Luôn luôn trả lời bằng tiếng Việt. Câu trả lời phải thường xuyên chèn emotion, emotion không được đơn điệu mà phải đa dạng, phong phú và sáng tạo.
- **Quy tắc mã hóa:** Luôn luôn dùng tiếng Việt có dấu, UTF-8.
- **Tên dự án:** get link manga
- **Loại ứng dụng:** Windows Desktop Application (WPF)
- **Framework:** .NET Framework 4.8 (C#)
- **Thư viện UI:** HandyControl (v3.5.0)
- **Chủ đề giao diện (Theme):** Cyberpunk (Tông màu tối, hiệu ứng phát sáng Neon)
- **Tính năng chính:** Cào liên kết truyện tranh từ trang HentaiForce, hiển thị danh sách kết quả, sao chép kết quả vào clipboard (chỉ link hoặc dạng bảng), lưu/đọc kết quả từ file Markdown.

---

## 2. Quy Tắc Code & Lập Trình C#
- **Không tạo mới `HttpClient` mỗi nơi:** Bắt buộc sử dụng instance static `_httpClient` được định nghĩa sẵn trong [MainWindow.xaml.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.xaml.cs). Nó được cấu hình tự động giải nén (GZip, Deflate) và User-Agent phù hợp.
- **Xử lý bất đồng bộ (Async/Await):** Luôn dùng `async/await` cho các tác vụ I/O (cào web, đọc ghi file) để tránh đơ giao diện.
- **Cơ chế dừng cào (Cancellation):** Các vòng lặp cào dữ liệu phải hỗ trợ `CancellationToken` từ `CancellationTokenSource` (`_cts`). Kiểm tra `token.IsCancellationRequested` để dừng tác vụ kịp thời và đưa nút bấm về trạng thái ban đầu trong khối `finally`.
- **An toàn đa luồng (UI Thread Safety):** Khi muốn tương tác với các control giao diện từ luồng phụ, sử dụng `Dispatcher.Invoke(() => { ... })`.
- **Regex & Trích xuất dữ liệu:**
  - Viết Regex tối ưu và sử dụng các tùy chọn `RegexOptions.IgnoreCase` hoặc `RegexOptions.Singleline` phù hợp.
  - **Luôn luôn giải mã HTML Entity của tiêu đề truyện bằng `WebUtility.HtmlDecode()`**.

- **Quy tắc phân tách file (MainWindow partial classes):** Tuyệt đối KHÔNG viết code mới hay bổ sung logic trực tiếp vào file [MainWindow.xaml.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.xaml.cs). File này chỉ giữ lại định nghĩa khung lớp, các trường dữ liệu (fields) và hàm khởi tạo (constructors). Mỗi logic/tính năng mới hoặc thay đổi phải được đặt trong các file partial tương ứng theo quy chuẩn đặt tên:
  - Logic hệ thống chung (Save, Load, Copy, Clear...): `MainWindow.System*.cs` (ví dụ: [MainWindow.SystemActions.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemActions.cs))
  - Logic Explorer / mở thư mục / chọn folder active / cooldown chống spam Explorer: **bắt buộc** đặt trong `MainWindow.SystemExplorer.cs`. Không được tự ý gọi `Process.Start`, `explorer.exe`, hay mở folder trực tiếp từ từng tab.
  - Logic quản lý thư mục tải / subfolder / `.tmp` dùng chung: **bắt buộc** đặt trong `MainWindow.System*.cs` phù hợp. `subfolder` là thư mục đích thật để chứa chap/truyện đã tải; tuyệt đối không được dùng `subfolder` làm nơi chứa file tạm. Mọi file/thư mục tạm cho quy trình tải phải luôn nằm trong `root\.tmp`.
  - Logic theo Tab: `MainWindow.Tab**.cs` (ví dụ: [MainWindow.TabHentaiforce.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.TabHentaiforce.cs))
  - Logic giao diện (Responsive, DataGrid events, context menu...): `MainWindow.UI***.cs` (ví dụ: [MainWindow.UIResponsive.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.UIResponsive.cs), [MainWindow.UIResultsGrid.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.UIResultsGrid.cs))
- **Quy tắc đặt tên chap/số thứ tự tải:** Khi hiển thị, lưu file, tạo thư mục, log, hoặc build chuỗi tên chap có số nhỏ hơn `10`, bắt buộc thêm số `0` ở trước để đồng bộ thứ tự tự nhiên, ví dụ `chap 01`, `chap 02`, `chap 03`. Quy tắc này áp dụng cả với số thập phân: ví dụ `1.5` phải thành `01.5`, `2.25` phải thành `02.25`.

- **Quy tắc Build:** Luôn luôn cấu hình, biên dịch và chạy dự án ở chế độ **Release** để tối ưu hóa hiệu năng và đảm bảo tính tương thích của HandyControl DLL. Không build hoặc phân phối bản Debug. Khi build mà phát sinh bất kỳ **Warning** hay **Error** nào, Agent BẮT BUỘC phải tự động tìm hiểu, tự sửa code và biên dịch lại liên tục cho tới khi sạch hoàn toàn lỗi và cảnh báo (0 warnings, 0 errors) mới bàn giao.
- **NuGet packages:** Tuyệt đối không trỏ `RestorePackagesPath` hay `LocalPackageRoot` về thư mục `.nuget` trong repo. Luôn luôn dùng cache chung tại `%UserProfile%\.nuget\packages`. Thư mục `.nuget` trong repo là dư thừa và không được commit.
- **Kiểm tra restore point Git:** Trước khi build chốt hoặc chuẩn bị commit, bắt buộc chạy kiểm tra snapshot Git qua script `tools\Verify-GitRestorePoint.ps1` (đã được `build.bat` tự gọi). Nếu script báo có file nguồn/XAML/tài nguyên quan trọng chưa được Git track hoặc đang bị xóa khỏi working tree thì phải xử lý dứt điểm trước khi coi commit đó là một restore point an toàn.
- **Luồng build chốt bắt buộc:** Sau khi code xong, luôn luôn chạy `build.bat` hoặc build Release với đúng 2 property `/p:AutoStampBuildInfo=true /p:AutoPublishRelease=true`. Không được build chốt bằng lệnh Release thiếu 2 property này vì sẽ làm mất tự động hóa timestamp và publish artifact.
- **Build timestamp tự động:** File `BuildInfo.cs` là file sinh tự động bởi MSBuild target `GenerateBuildInfo` ở build Release chốt. Không sửa tay timestamp trong file này. File partial [MainWindow.SystemBuild.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemBuild.cs) chịu trách nhiệm đổ timestamp lên UI.
- **Publish Release tự động:** Sau mỗi build Release chốt thành công, MSBuild target `PublishReleaseArtifact` phải tự động:
  1. Giữ nguyên thư mục `release\Comic-GMTPC`
  2. Xóa sạch toàn bộ dữ liệu con bên trong thư mục đó
  3. Copy duy nhất file `Comic-GMTPC.exe` mới build vào lại thư mục đó
  Không xóa rồi tạo lại thư mục gốc bằng tay, không copy tay sau build.

---

## 3. Quy Tắc Thiết Kế Giao Diện (WPF / XAML)
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
- **Book list / DataGrid kết quả:** Mọi cột trong danh sách book list bắt buộc hỗ trợ sort hai chiều `tăng dần / giảm dần`. Agent không được chỉ bật sort cho một vài cột rồi bỏ sót cột còn lại. Khi thêm cột mới sau này, phải tự nối cột đó vào cơ chế sort chung.
- **Quy tắc phong cách ComboBox (Style & File code-behind):**
  - Mọi ComboBox trên giao diện bắt buộc phải sử dụng Style `{StaticResource CyberpunkComboBox}` để đồng bộ giao diện.
  - Khi chưa drop down (ComboBox đóng): Nền mặc định bắt buộc là màu trắng (`White`), chữ có màu xanh đậm (`#163A61`). Khi đã drop down (mở menu): Chữ hiển thị sáng theo đúng quy chuẩn cyberpunk.
  - Toàn bộ các phương thức xử lý sự kiện `SelectionChanged` của toàn bộ các ComboBox trên ứng dụng bắt buộc phải đặt trong file partial tách biệt [MainWindow.SystemComboBox.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemComboBox.cs). Không rải rác code ở các file logic khác.
- **Quy tắc quản lý MessageBox (Hộp thoại thông báo & File code-behind):**
  - Tuyệt đối không gọi trực tiếp `MessageBox.Show` rải rác trong mã nguồn để tránh lỗi hiển thị sai encoding (mojibake).
  - Tất cả các hộp thoại thông báo bắt buộc phải đi qua các phương thức helper tập trung trong file [MainWindow.SystemMessageBox.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemMessageBox.cs) như `ShowInfo`, `ShowWarning`, `ShowError`, `ShowConfirm`.
  - Đảm bảo file [MainWindow.SystemMessageBox.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemMessageBox.cs) được lưu ở định dạng UTF-8 with BOM để hiển thị tiếng Việt chính xác 100%.

---

## 4. Quy Chuẩn Đọc/Ghi File Markdown (.md)
- **Xuất File:** Khi lưu kết quả, ghi dưới dạng bảng Markdown chuẩn:
  ```markdown
  | No. | Gallery Name | Gallery Link |
  | :--- | :--- | :--- |
  | 1 | Tên truyện | Link |
  ```
- **Xử lý ký tự đặc biệt:** Thay thế ký tự ống `|` trong tiêu đề truyện thành `\|` trước khi lưu vào bảng để tránh hỏng cấu trúc bảng Markdown. Khi load file lên, đổi ngược lại thành `|`.
- **Đọc File:** Bỏ qua các dòng tiêu đề và dòng phân cách bằng một cách an toàn. Chỉ parse những dòng hợp lệ có chứa link bắt đầu bằng `http`.

---

## 5. Lệnh Build & Chạy Dự Án
- **Khôi phục thư viện:** Dùng cache `%UserProfile%\.nuget\packages` thông qua `build.bat` hoặc `MSBuild /t:Restore`.
- **Build dự án:** Ưu tiên tuyệt đối:
  ```powershell
  .\build.bat
  ```
- **Luật bắt buộc cho mọi Agent:** Sau mỗi đợt sửa code, Agent phải tự chạy build chốt, tự đọc toàn bộ warning/error, tự sửa và build lại liên tục cho đến khi đạt đúng `0 Warning(s), 0 Error(s)` thì mới được phép dừng hoặc bàn giao. Không được dừng ở mức "đã giảm lỗi" hay "còn warning cũ".
- **Build dự án thủ công nếu thật sự cần:** Bắt buộc phải có đủ property tự động hóa:
  ```powershell
  "C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe" "C:\Users\Admin\source\repos\ghostminhtoan\get link manga\Comic-GMTPC.csproj" /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU /p:AutoStampBuildInfo=true /p:AutoPublishRelease=true
  ```
- **Đường dẫn file thực thi chính:** `bin\Release\Comic-GMTPC.exe`
- **Đường dẫn artifact phát hành sau build chốt:** `release\Comic-GMTPC\Comic-GMTPC.exe`
- **Build time:** Khi code xong và build chốt thành công thì ghi build time đúng thời điểm đó, không lấy lại theo các lần rebuild sau trong Visual Studio. Timestamp phải được sinh tự động ở build Release chốt. Format bắt buộc: `2026-06-07 ∕ 12.44.28 PM Sunday` (năm-tháng-ngày ∕ giờ.phút.giây AM/PM Thứ, trong đó `∕` là ký tự unicode riêng, không phải `/` trên bàn phím).

## 6. Quy tắc git commit
- **Sau khi build file exe ra release thành công, luôn luôn thực hiện git commit & git push lên repo github.**
- **Mỗi khi tạo commit mới, bắt buộc phải cập nhật lịch sử (Mã commit, Build Time, Nội dung) vào file [log git commit.md](file:///r:/HDD R/ZC SYMLINK/USERS/source/repos/ghostminhtoan/get link manga/log git commit.md) với quy tắc: Commit mới nhất luôn nằm ở trên cùng (dưới phần tiêu đề). Build Time phải là thời điểm build chốt, không phải thời điểm Visual Studio rebuild sau đó.**

---

## 7. Hướng Dẫn Tự Động Hóa Tạo Tab Cào Truyện Mới (Cho AI Agent)
Khi người dùng yêu cầu thêm một trang web cào truyện mới với các thông tin đầu vào sau:
- **Link domain:** Tên miền của trang web cần cào (ví dụ: `https://example.com`)
- **Link book:** Định dạng link của trang chi tiết truyện/danh sách chương (ví dụ: `https://example.com/manga/ten-truyen`)
- **Link chapter:** Định dạng link của trang đọc chương/ảnh (ví dụ: `https://example.com/chapter/123`)
- **Image redirect:** `yes` hoặc `no` (có cần chuyển hướng/cào tiếp trang con để lấy link ảnh trực tiếp hay không)
- **Tự động đếm số page:** Thuật toán hoặc quy tắc Regex/HTML selector để tự động tìm kiếm và trích xuất tổng số trang/chương từ trang chi tiết (ví dụ: tìm số trang lớn nhất trong thành phần trang pagination hoặc tự động check trạng thái `Page 1 of X`).

**AI Agent cần tự động thực hiện các bước sau:**

### Bước 1: Thiết kế giao diện (UI) trong [MainWindow.xaml](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.xaml)
1. Xác định trang web thuộc nhóm **MANGA** hay **HENTAI** để thêm thẻ `<TabItem>` vào đúng `TabControl` (`tabManga` hoặc `tabHentai`).
2. Sử dụng `Style="{StaticResource CyberpunkTabItem}"` và chọn một màu sáng độc nhất cho `<TabItem.Tag>` (ví dụ: `#00ff66` cho Green, `#ffe600` cho Yellow, `#00ffff` cho Cyan...).
3. Sao chép cấu trúc giao diện điều khiển cấu hình (Parameters Config) từ các tab có sẵn, đổi tên toàn bộ các Control (TextBox, Button, ToggleButton...) theo đúng tiền tố và tên miền mới (ví dụ: `txtExampleTagUrl`, `btnExampleFetchInfo`, `txtExampleLog`...).

### Bước 2: Tạo File Code Logic Mới `MainWindow.Tab[SiteName].cs`
Tạo một file partial class mới (ví dụ: `MainWindow.TabExample.cs`) để chứa toàn bộ logic xử lý cho trang web đó.
1. **Hàm Phân Tích (Analyze Target Page):**
   - Dùng `_httpClient` tải trang chi tiết (`link book`).
   - Sử dụng Regex hoặc HTML parser để lấy tổng số trang/chương, tiêu đề truyện.
   - Cập nhật thông tin lên giao diện thông qua `Dispatcher.Invoke()`.
2. **Hàm Cào (Start Crawling / Get Link):**
   - Hỗ trợ dừng cào bằng `CancellationToken` (`_cts.Token`).
   - Lặp qua các chương cần tải từ `txtExamplePageFrom` đến `txtExamplePageTo`.
   - Nếu **Image redirect = yes**: Cần cào tiếp trang con (`link chapter`) để tìm thẻ ảnh chứa link ảnh thực tế.
   - Nếu **Image redirect = no**: Lấy trực tiếp link ảnh hiển thị trên trang hiện tại.
   - Add danh sách link cào được vào DataGrid thông qua `GalleryItem`.
   - Nếu có cơ chế tải theo `subfolder`, phải có đường đi rõ ràng để chap/truyện được tải thật vào đúng `subfolder` người dùng chọn/apply; không được coi `subfolder` là vùng tạm.
   - Mọi dữ liệu tạm trong lúc tải (ảnh tạm, file ghép tạm, file đang xử lý, thư mục staging...) phải luôn nằm trong `root\.tmp`, dùng chung cho toàn app.
   - Tên chap sinh ra cho thư mục/file hiển thị phải zero-pad khi phần nguyên nhỏ hơn `10`, kể cả trường hợp số thập phân.

### Bước 3: Đăng ký Sự Kiện trong Code-behind
1. Gán các hàm sự kiện Click của Button (`BtnExampleFetchInfo_Click`, `BtnExampleScrape_Click`...) tương ứng vào file code logic.
2. Đảm bảo xử lý lỗi bằng khối `try-catch` và ghi log chi tiết ra ô cấu hình log tương ứng (`txtExampleLog`).
3. Nếu tab có nút `OPEN FOLDER`, `BROWSE`, `EXPLORER`, hoặc mở thư mục từ từng dòng DataGrid thì **không được viết logic riêng trong file tab**. Phải gọi helper chung trong `MainWindow.SystemExplorer.cs`, dùng `ShellFolderLauncher.TryOpenFolder(...)`, có cooldown chống mở lặp, mở đúng folder theo tab active, và ưu tiên folder sách cụ thể nếu đã tồn tại.

### Bước 4: Kiểm thử & Biên dịch (Build)
1. Chạy lệnh build để đảm bảo dự án biên dịch thành công mà không có lỗi hay cảnh báo.
2. Commit và push thay đổi lên Git theo đúng quy trình.

---

## 8. Chuẩn Stable Explorer (Bắt buộc cho mọi tab hiện tại và tương lai)
- Lấy hành vi mượt của `truyenqq` làm chuẩn chung cho toàn app.
- Mọi thao tác mở thư mục phải đi qua `MainWindow.SystemExplorer.cs` + `ShellFolderLauncher.TryOpenFolder(...)`.
- Không được mở root download một cách mù nếu đã xác định được folder site hoặc folder sách cụ thể.
- Nút `OPEN FOLDER` khu vực download phải mở đúng thư mục active của tab đang đứng, không tự spawn nhiều cửa sổ Explorer.
- Nếu UI có `subfolder`, agent phải hiểu đây là thư mục đích thật của dữ liệu đã tải. Không được gán vai trò thư mục tạm cho `subfolder`.
- Thư mục tạm duy nhất cho mọi workflow tải là `root\.tmp`. Không tạo `.tmp` rải rác trong từng `subfolder`, từng site folder, hay từng book folder trừ khi người dùng đổi luật sau này.
- Nút mở folder trong từng dòng DataGrid phải ưu tiên:
  1. folder sách cụ thể
  2. folder site
  3. root download
- Khi tạo tab mới sau này, AI Agent phải tự nối tab đó vào chuẩn Explorer này mà không chờ người dùng nhắc lại.
