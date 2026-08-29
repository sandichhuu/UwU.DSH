# DeepSeek Harness & Antigravity Proxy Launcher (Siêu Nhẹ)

Một ứng dụng Desktop Native WebView2 **siêu nhẹ** (chỉ ~1.3MB), khởi động tức thì, thiết kế riêng cho việc chạy ngầm dịch vụ DeepSeek Harness & Antigravity Proxy và tích hợp giao diện 2 Tab mượt mà.

---

## ✨ Tính Năng Nổi Bật

1. **Khởi động siêu nhanh & Siêu nhẹ (~1.3 MB)**
   - Sử dụng Windows Native WebView2 runtime (có sẵn trên Windows 10/11), không tốn RAM hay bộ nhớ như Electron (~100MB+).
   - Mở ứng dụng gần như tức thì (< 0.1s).

2. **Kiểm Tra Môi Trường & Cấu Hình Tự Động**
   - **Kiểm tra Node.js**: Kiểm tra xem máy đã cài Node.js hay chưa. Nếu chưa cài đặt, hiển thị hộp thoại thông báo kèm link tải (`https://nodejs.org/`). Khi bấm tải (hoặc link), ứng dụng tự động mở trình duyệt và tự đóng app.
   - **Cấu hình khởi chạy lần đầu**: Tự động gọi lệnh `npm config set allow-scripts=better-sqlite3 --location=user` trong lần chạy đầu tiên.

3. **Chạy Ngầm Dịch Vụ Tự Động**
   - **Tự động cài đặt / cập nhật**: Tự động chạy lệnh `npm install -g antigravity-claude-proxy@latest` ngầm.
   - **Tự động kích hoạt dịch vụ**: Tiếp đó chạy lệnh ngầm:
     ```bash
     antigravity-claude-proxy start & npx -y @deepseek-ai/dsh web --no-open
     ```
   - **Dọn dẹp tiến trình**: Khi đóng ứng dụng, tất cả tiến trình Node/Cmd con được tự động dọn dẹp (kill process tree), không gây tốn tài nguyên hệ thống.

4. **Giao Diện 2 Tab Hiện Đại (Dark Mode)**
   - **Tab 1 (Mặc định)**: `Deepseek Harness` -> Webview hiển thị `http://127.0.0.1:3080/`
   - **Tab 2**: `Antigravity Proxy` -> Webview hiển thị `http://localhost:8080/`
   - **Thanh Status ở Footer**: Đã được đưa xuống đáy ứng dụng, hiển thị trạng thái kết nối & thông tin cổng DSH: 3080 | Proxy: 8080.
   - **Mặc định ẩn Header**: Thanh Header phía trên mặc định ẩn đi khi mở ứng dụng để tối ưu không gian hiển thị WebView.
   - **Nút Toggle ở Footer**: Nút **`👁️ Show Header`** / **`👁️ Hide Header`** ở góc dưới bên phải thanh Footer giúp bật/tắt hiển thị Header bất kỳ lúc nào.
   - Nút **🛑 Stop Services**: Dừng tiến trình `dsh` ngầm và thực thi `antigravity-claude-proxy stop`.
   - Nút **🔄 Reload** và **⚡ Restart Services** tiện lợi.

---

## 🚀 Hướng Dẫn Sử Dụng

### Cách 1: Chạy trực tiếp qua file `run.bat`
Click đúp vào file `run.bat` trong thư mục project để chạy nhanh ứng dụng.

### Cách 2: Chạy từ Terminal / Command Line
```bash
dotnet run --project DshAntigravityLauncher.csproj
```

### Cách 3: Chạy file `.exe` đã build sẵn
- Mở thư mục `dist\DshAntigravityLauncher.exe` (File `.exe` siêu nhẹ ~1.3 MB).
- Mở thư mục `Release\Standalone\DshAntigravityLauncher.exe` (File Single-File Standalone độc lập).

---

## 🛠️ Hướng Dẫn Build

Để build lại ứng dụng:
- Đơn giản nhất: Click đúp vào file `build.bat`.
- Hoặc chạy lệnh sau trong terminal:

```bash
# Build bản siêu nhẹ (cần máy có sẵn .NET runtime)
dotnet publish DshAntigravityLauncher.csproj -c Release -r win-x64 --no-self-contained -o dist

# Build bản Single-File Standalone độc lập (Release/Standalone)
dotnet publish DshAntigravityLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o Release/Standalone

# Build bản Độc lập Standalone (dist-standalone)
dotnet publish DshAntigravityLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist-standalone
```

---

## 📁 Cấu Trúc Project

- `MainWindow.xaml`: Định nghĩa giao diện 2 Tab & Webview với chủ đề Dark Mode.
- `MainWindow.xaml.cs`: Kiểm tra môi trường Node.js, cấu hình lần đầu, chạy ngầm lệnh `npm install` và `npx dsh web | antigravity-claude-proxy start`, quản lý tiến trình.
- `NodeDownloadWindow.xaml` / `.xaml.cs`: Cửa sổ thông báo tải Node.js khi hệ thống chưa có Node.js.
- `DshAntigravityLauncher.csproj`: File cấu hình project .NET 10 WPF + Microsoft.Web.WebView2.
- `dist/DshAntigravityLauncher.exe`: File thực thi siêu nhẹ.
- `Release/Standalone/DshAntigravityLauncher.exe`: File thực thi Single-File Standalone đầy đủ.
