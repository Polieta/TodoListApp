using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace TodoListApp
{
    public static class SelfUpdater
    {
        /// <summary>
        /// Khởi chạy PowerShell để thay thế file exe hiện tại bằng file mới
        /// </summary>
        public static void ApplyUpdateAndRestart(string downloadedExePath)
        {
            string currentExe = Assembly.GetExecutingAssembly().Location;
            string psScript = $@"
            Start-Sleep -Seconds 2
            Copy-Item -Path '{downloadedExePath}' -Destination '{currentExe}' -Force
            Remove-Item -Path '{downloadedExePath}' -ErrorAction SilentlyContinue
            Start-Process -FilePath '{currentExe}'
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