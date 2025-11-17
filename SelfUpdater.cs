using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace TodoListApp
{
    public static class SelfUpdater
    {
        /// <summary>
        /// Khởi chạy PowerShell để giải nén file ZIP, sao chép toàn bộ nội dung vào thư mục chứa EXE hiện tại,
        /// và khởi động lại ứng dụng đã được cập nhật.
        /// </summary>
        public static void ApplyUpdateAndRestart(string downloadedZipPath)
        {
            string currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe))
            {
                currentExe = Assembly.GetExecutingAssembly().Location;
            }

            string targetDirectory = Path.GetDirectoryName(currentExe);
            if (string.IsNullOrEmpty(targetDirectory))
            {
                throw new InvalidOperationException("Không thể xác định thư mục chứa file EXE hiện tại.");
            }

            string targetExeName = Path.GetFileName(currentExe);
            string psScript = $@"
                Start-Sleep -Seconds 2
                try {{
                    # Đường dẫn thư mục chứa EXE hiện tại
                    $targetDir = '{targetDirectory}'

                    # Giải nén ZIP vào thư mục tạm
                    $tempExtractPath = Join-Path $env:TEMP 'TodoListApp_Extracted'
                    if (Test-Path $tempExtractPath) {{ Remove-Item -Path $tempExtractPath -Recurse -Force -ErrorAction SilentlyContinue }}
                    Expand-Archive -Path '{downloadedZipPath}' -DestinationPath $tempExtractPath -Force

                    # Sao chép toàn bộ nội dung từ thư mục giải nén vào thư mục đích
                    # -Recurse: Sao chép thư mục con
                    # -Force: Ghi đè file nếu tồn tại
                    # -ErrorAction: Tiếp tục nếu gặp lỗi (ví dụ, file bị khóa)
                    Get-ChildItem -Path $tempExtractPath -Force | ForEach-Object {{
                        $destinationPath = Join-Path $targetDir $_.Name
                        if ($_.PSIsContainer) {{
                            Copy-Item $_.FullName -Destination $destinationPath -Recurse -Force -ErrorAction SilentlyContinue
                        }} else {{
                            Copy-Item $_.FullName -Destination $destinationPath -Force -ErrorAction SilentlyContinue
                        }}
                    }}

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
                    # Bạn có thể quyết định có nên exit hay không nếu cập nhật thất bại
                    # Environment.Exit(1)
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
            Environment.Exit(0);
        }
    }
}