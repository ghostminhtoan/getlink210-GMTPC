# Hướng Dẫn Phát Triển & Quy Tắc Code - Dự Án "get link manga"

Tài liệu này chứa quy tắc code, cấu trúc project, hướng dẫn UI và quy trình làm việc cho hai lane:
- **Lane Picture (Manga/Hentai)**
- **Lane Novel (Light Novel)**

**Mọi Agent AI phải đọc file này trước khi sửa code.**

---

## 0. Mindmap Workflow Cho Agent
```text
+------------------+     +--------------------------+     +---------------------------+
| 1. ĐỌC LUẬT GỐC  | --> | 2. XÁC ĐỊNH LOẠI VIỆC    | --> | 3. CHECK RÀNG BUỘC KỸ THUẬT |
+------------------+     +--------------------------+     +---------------------------+
| - Đọc workflow   |     | - UI/XAML -> MainWindow  |     | - async/await cho I/O     |
| - Trả lời TV     |     | - System -> MainWindow   |     | - dùng static _httpClient |
| - Giữ theme UI   |     |   .System*.cs            |     | - hỗ trợ CancellationToken|
| - Không code vào |     | - UI chung ->            |     | - UI thread -> Dispatcher |
| - Terminal chuẩn |     |   MainWindow.UI*.cs      |     | - terminal ưu tiên Git    |
|   Git Bash/Python|     | - Picture ->             |     |   Bash hoặc Python        |
|   MainWindow     |     |   MainWindow.Tab*.cs     |     | - HtmlDecode title        |
|   .xaml.cs       |     | - Picture ->             |     | - Explorer qua System     |
+------------------+     | - Novel -> LightNovel/   |     | - sort 2 chiều mọi cột    |
                         |   TabHako/HakoCapture    |     | - `.tmp` nằm ở root       |
                         +--------------------------+     | - zero-pad chap < 10      |
                                                          +---------------------------+

+---------------------------+    +-----------------------+    +---------------------------+
| 4. THÊM TAB MỚI?          | -> | 5. ĐỤNG MARKDOWN?     | -> | 6. BUILD CHỐT BẮT BUỘC    |
+---------------------------+    +-----------------------+    +---------------------------+
| - Thêm TabItem đúng nhóm  |    | - Xuất bảng chuẩn     |    | - Chạy .\build.bat        |
| - Đặt tên control chuẩn   |    | - Escape ký tự `|`    |    | - Hoặc build Release đủ   |
| - Tạo MainWindow.Tab*.cs  |    | - Chỉ parse dòng http |    |   AutoStamp/AutoPublish   |
| - Viết Analyze/Scrape     |    +-----------------------+    | - Sửa tới khi 0 warning   |
| - Nối Explorer chung      |                                 |   0 error                 |
+---------------------------+                                 +---------------------------+

+---------------------------+    +----------------------------+
| 7. SAU BUILD THÀNH CÔNG   | -> | 8. BÀN GIAO               |
+---------------------------+    +----------------------------+
| - Check restore point Git |    | - Nêu rõ sửa lane nào     |
| - Check exe ở release\... |    | - Nêu rõ build sạch chưa  |
| - Update log git commit   |    | - Nói thẳng test/rủi ro   |
| - Commit + push GitHub    |    +----------------------------+
+---------------------------+
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
Xác định lane / công việc:
  |-- Lane Novel (Light Novel) --> Đọc thêm Phần 5
  |-- Lane Picture (Manga) ------> Theo luật chung / Phần 8
  `-- Giao diện / Hệ thống ------> Sửa file tương ứng
        |
        v
Có vi phạm rule kỹ thuật?
  |-- có -> đổi hướng làm
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
- **Quy tắc trả lời:** Luôn trả lời bằng tiếng Việt có dấu.
- **Quy tắc mã hóa:** Luôn dùng UTF-8.
- **Quy tắc terminal cho mọi agent:** Áp dụng cho cả **Codex** lẫn **Antigravity**. Terminal mặc định phải là **Git Bash** hoặc **Python**. Không dùng **PowerShell** cho workflow thường ngày, trừ khi người dùng yêu cầu rõ hoặc chỉ còn đúng lựa chọn đó.
- **Tên dự án:** `get link manga`
- **Loại ứng dụng:** Windows Desktop Application (WPF)
- **Framework:** .NET Framework 4.8
- **Theme UI:** Cyberpunk

### Hai lane chính
1. **Lane Picture**
   - Cào link truyện tranh/hentai
   - Hiển thị danh sách kết quả
   - Copy link hoặc bảng markdown
   - Tải ảnh
2. **Lane Novel**
   - Lấy `title + plain text + markdown`
   - Tách riêng logic novel, không trộn với lane ảnh

---

## 2. Quy Tắc Code & Cấu Trúc Partial

### Tooling Rules
- Áp dụng cho cả **Codex** lẫn **Antigravity**.
- Terminal mặc định: **Git Bash**.
- Nếu cần script nhanh hoặc xử lý text/file nhỏ: dùng **Python**.
- Không dùng **PowerShell** cho workflow thường ngày.
- Chỉ dùng **PowerShell** khi:
  - người dùng yêu cầu rõ
  - hoặc môi trường chỉ còn đúng lựa chọn đó
- Khi cần tìm file/text:
  - ưu tiên `rg`
  - fallback mới dùng công cụ khác
- Khi cần build:
  - ưu tiên `.\build.bat`
  - chỉ gọi lệnh build tay khi thật sự cần debug build
- `// ponytail: tooling rule exists to stop terminal drift across agents.`

### Quy tắc kỹ thuật
- Terminal mặc định phải là **Git Bash** hoặc **Python**. Không dùng **PowerShell** cho tác vụ thường ngày, script thường, build thường, grep thường, hay command workflow, trừ khi người dùng yêu cầu rõ hoặc chỉ có PowerShell mới chạy được.
- Không tạo `HttpClient` mới mỗi nơi. Dùng static `_httpClient` ở [MainWindow.SystemBootstrap.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemBootstrap.cs).
- Mọi I/O dài dùng `async/await`.
- Vòng lặp dài phải hỗ trợ `CancellationToken`.
- Đụng UI từ luồng phụ phải qua `Dispatcher`.
- Tiêu đề truyện phải decode bằng `WebUtility.HtmlDecode()`.

### Quy tắc MainWindow partial
- **Cấm code logic vào** [MainWindow.xaml.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.xaml.cs). File này chỉ giữ vỏ partial class.
- Bootstrap, constructor, command bindings, field chung, `_httpClient` đặt ở [MainWindow.SystemBootstrap.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemBootstrap.cs).
- Captcha, reset cookie/session đặt ở [MainWindow.SystemCaptcha.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemCaptcha.cs).
- Logic hệ thống chung đặt ở `MainWindow.System*.cs`.
- Logic Explorer bắt buộc đặt ở [MainWindow.SystemExplorer.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemExplorer.cs).
- Logic điều hướng tab theo URL đặt ở [MainWindow.TabRouting.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.TabRouting.cs).
- Logic từng nguồn manga/hentai đặt ở `MainWindow.Tab*.cs`.
- Logic novel đặt ở [MainWindow.LightNovelDesk.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.LightNovelDesk.cs) và [MainWindow.TabHako.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.TabHako.cs).
- UI helper/bootstrap đặt ở [MainWindow.UIBootstrap.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.UIBootstrap.cs).
- UI log đặt ở [MainWindow.UILogs.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.UILogs.cs).
- UI khác đặt ở `MainWindow.UI*.cs`.

### Quy tắc đặt tên file partial
- `MainWindow.System*.cs` cho system
- `MainWindow.UI*.cs` cho UI
- `MainWindow.Tab*.cs` cho từng tab/site
- `MainWindow.Section*.cs` cho section nếu có
- `MainWindow.Rail*.cs` cho rail nếu có

### Quy tắc thư mục tải
- `subfolder` là đích tải thật.
- Không dùng `subfolder` làm thư mục tạm.
- Thư mục tạm duy nhất là `root\.tmp`.

### Quy tắc tên chap
- Số thứ tự nhỏ hơn `10` phải zero-pad.
- Ví dụ: `01`, `02`, `03`, `01.5`, `02.25`.

### NuGet
- Dùng cache chung `%UserProfile%\.nuget\packages`.
- Không trỏ package về `.nuget` trong repo.

### Restore point Git
- Trước build chốt hoặc commit, chạy `tools\Verify-GitRestorePoint.ps1`.

---

## 3. Quy Tắc UI / XAML
- Giữ theme Cyberpunk đồng nhất.
- Dùng style có sẵn trong `App.xaml` và resource chung.

### Quy tắc tên control
- TextBox: `txt*`
- Button: `btn*`
- Label/TextBlock: `lbl*`
- ProgressBar: `progressBar`

### DataGrid / book list
- Mọi cột phải sort được hai chiều.

### ComboBox
- Dùng `{StaticResource CyberpunkComboBox}`.
- Handler `SelectionChanged` đặt ở [MainWindow.SystemComboBox.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemComboBox.cs).

### MessageBox
- Không gọi `MessageBox.Show` rải rác.
- Phải đi qua [MainWindow.SystemMessageBox.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemMessageBox.cs).

---

## 4. Quy Chuẩn Markdown
- Xuất bảng markdown chuẩn:

```markdown
| No. | Gallery Name | Gallery Link |
| :--- | :--- | :--- |
| 1 | Tên truyện | Link |
```

- Escape ký tự `|` trong tiêu đề thành `\|`.
- Khi load file, chỉ parse dòng hợp lệ có `http`.

---

## 5. Luật Riêng Cho Lane Novel

### File chính
- [MainWindow.LightNovelDesk.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.LightNovelDesk.cs)
- [MainWindow.TabHako.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.TabHako.cs)
- [MainWindow.SystemFirecrawl.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemFirecrawl.cs)
- [HakoChapterCaptureWindow.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/HakoChapterCaptureWindow.cs)
- [LightNovelModels.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/LightNovelModels.cs)

### Luồng novel chuẩn
1. Phân tích URL hoặc paste direct link
2. Lấy danh sách book
3. Lấy danh sách chapter
4. Giữ đúng thứ tự web
5. Copy text theo từng chapter
6. Xuất plain text + markdown

### Quy tắc trích xuất
- Title ưu tiên `.title-top h4`, fallback `h2`
- Content ưu tiên `#chapter-content`
- Fallback `.chapter-content`
- Fallback `.long-text`
- Gặp `403` thì skip, log lỗi, chạy tiếp

### UI novel
- 4 panel: Book, Chapter, Plain Text, Markdown
- Có `AUTO COPY TEXT`, `STOP COPY TEXT`
- Floating control phải luôn usable

---

## 6. Build & Phát Hành
- Build chuẩn:

```powershell
.\build.bat
```

- Hoặc build Release thủ công:

```powershell
"C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe" "C:\Users\Admin\source\repos\ghostminhtoan\get link manga\Comic-GMTPC.csproj" /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU /p:AutoStampBuildInfo=true /p:AutoPublishRelease=true
```

### Luật build bắt buộc
- Không dừng khi còn warning/error.
- Phải sửa và build lại tới `0 Warning(s), 0 Error(s)`.
- File exe chính: `bin\Release\Comic-GMTPC.exe`
- Artifact phát hành: `release\Comic-GMTPC\Comic-GMTPC.exe`

---

## 7. Quy Tắc Git Commit
- Bất kể agent nào (Codex hay Antigravity), khi commit và push đều phải thực hiện trên default branch là `main`. Không commit lên các branch phụ trừ trường hợp đặc biệt được yêu cầu riêng.
- Sau build release thành công, phải commit và push trực tiếp lên default branch `main`.
- Cập nhật [log git commit.md](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/log%20git%20commit.md).
- Commit mới nhất luôn nằm trên cùng.
- Không commit file rác debug, dump, html tạm.

---

## 8. Hướng Dẫn Thêm Tab Cào Truyện Mới

### Bước 1. Sửa UI
- Thêm `TabItem` đúng nhóm Manga/Hentai trong [MainWindow.xaml](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.xaml)
- Đặt tên control đúng tiền tố
- Dùng style cyberpunk có sẵn

### Bước 2. Tạo file logic
- Tạo `MainWindow.Tab[SiteName].cs`
- Viết:
  - Analyze target page
  - Scrape
  - Log lỗi
  - Hỗ trợ cancel

### Bước 3. Nối event
- Gắn click handler đúng nút
- Ghi log đúng ô log
- Mở folder phải đi qua Explorer helper chung

### Bước 4. Build
- Build sạch `0/0`
- Commit và push

---

## 9. Chuẩn Stable Explorer
- Hành vi Explorer lấy `truyenqq` làm chuẩn chung.
- Mọi thao tác mở thư mục phải qua [MainWindow.SystemExplorer.cs](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/get%20link%20manga/MainWindow.SystemExplorer.cs) và `ShellFolderLauncher.TryOpenFolder(...)`.
- Ưu tiên mở:
  1. folder sách cụ thể
  2. folder site
  3. root download
- Không spawn nhiều cửa sổ Explorer vô ích.
- Khi thêm tab mới, phải tự nối vào chuẩn Explorer này.
