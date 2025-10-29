using System.Diagnostics;
using System.IO;
using System.IO.Compression; // Thêm thư viện này
using System.Reflection;

namespace TodoListApp
{
    public static class SelfUpdater
    {
        /// <summary>
        /// Khởi chạy PowerShell để giải nén file ZIP, tìm file EXE bên trong và thay thế file EXE hiện tại
        /// </summary>
        public static void ApplyUpdateAndRestart(string downloadedZipPath) // Nhận đường dẫn đến file ZIP đã tải
        {
            // Lấy đường dẫn đến file EXE hiện tại
            string currentExe = Environment.ProcessPath; // Dùng ProcessPath để đảm bảo lấy đúng .exe
            if (string.IsNullOrEmpty(currentExe))
            {
                // Fallback nếu ProcessPath không hoạt động (ít khi xảy ra)
                currentExe = Assembly.GetExecutingAssembly().Location;
            }

            // Thư mục chứa file EXE hiện tại (nơi sẽ giải nén và thay thế)
            string targetDirectory = Path.GetDirectoryName(currentExe);
            if (string.IsNullOrEmpty(targetDirectory))
            {
                throw new InvalidOperationException("Không thể xác định thư mục chứa file EXE hiện tại.");
            }

            // Tên file EXE mục tiêu (ví dụ: TodoListApp.exe)
            string targetExeName = Path.GetFileName(currentExe);

            // Tạo script PowerShell
            // - Giải nén ZIP vào thư mục tạm (vẫn cần thư mục tạm để giải nén, nhưng file ZIP có thể ở nơi khác)
            // - Tìm file EXE có tên trùng với targetExeName trong thư mục giải nén
            // - Ghi đè file EXE hiện tại bằng file EXE mới
            // - Xóa file ZIP đã tải và thư mục giải nén tạm
            // - Khởi động lại file EXE đã được cập nhật
            string psScript = $@"
                Start-Sleep -Seconds 2
                try {{
                    # Giải nén ZIP vào thư mục tạm
                    $tempExtractPath = Join-Path $env:TEMP 'TodoListApp_Extracted'
                    if (Test-Path $tempExtractPath) {{ Remove-Item -Path $tempExtractPath -Recurse -Force }}
                    Expand-Archive -Path '{downloadedZipPath}' -DestinationPath $tempExtractPath -Force

                    # Tìm file EXE mục tiêu trong thư mục giải nén
                    $newExePath = Get-ChildItem -Path $tempExtractPath -Recurse -Name '{targetExeName}' | ForEach-Object {{ Join-Path $tempExtractPath $_ }} | Select-Object -First 1

                    if (-not $newExePath -or -not (Test-Path $newExePath)) {{
                        Write-Error 'Không tìm thấy file EXE trong file ZIP.'
                        return
                    }}

                    # Ghi đè file EXE hiện tại
                    Copy-Item -Path $newExePath -Destination '{currentExe}' -Force

                    # Xóa thư mục giải nén tạm
                    Remove-Item -Path $tempExtractPath -Recurse -Force -ErrorAction SilentlyContinue

                    # Xóa file ZIP đã tải (cả ở thư mục gốc hoặc Downloads)
                    Remove-Item -Path '{downloadedZipPath}' -ErrorAction SilentlyContinue

                    # Khởi động lại ứng dụng đã được cập nhật
                    Start-Process -FilePath '{currentExe}'
                }}
                catch {{
                    Write-Error ""Lỗi trong quá trình cập nhật: $_""
                    # Có thể thêm logging vào đây nếu cần
                }}
            ";

            string tempPs = Path.Combine(Path.GetTempPath(), "TodoListApp_Update.ps1");
            File.WriteAllText(tempPs, psScript);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{tempPs}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(psi);
            Environment.Exit(0); // Đóng ứng dụng ngay để PowerShell có thể ghi đè
        }
    }
}