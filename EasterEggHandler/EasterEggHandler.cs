// EasterEgg/EasterEggHandler.cs
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;
using System.Windows;

namespace TodoListApp.EasterEgg
{

    public delegate void KonamiActivatedEventHandler(object sender, EventArgs e);
    public static class EasterEggHandler
    {
        // Danh sách các liên kết Easter Egg
        private static readonly string[] EasterEggLinks = {
            "https://youtu.be/dQw4w9WgXcQ?si=wWrdE66EEmRDK53g",
            "https://youtube.com/shorts/h-rBf88Jhdw?si=X3lonWaJemIbVIcY",
            "https://youtu.be/BeXsAWYABW0?si=thKMyv4IX3gGk6h-"
        };

        private static readonly Random Random = new Random();

        // --- Biến trạng thái cho các Easter Egg ---
        private static bool _isEasterEggTAMKHOAActivated = false;
        private static bool _isSpeedModeActive = false;
        private static bool _isUltraDarkModeActive = false;

        // Tổng số lần click đã nhận
        private static int _totalClicks = 0;
        // Số lần click cần thiết cho từng vị trí (100, 200, 300)
        private static readonly int[] _treasureTargets = { 100, 200, 300 };
        // Đánh dấu xem từng vị trí đã được tìm thấy chưa
        private static readonly bool[] _treasureFound = new bool[3];
        // Chỉ số vị trí tiếp theo cần hoàn thành (0, 1, 2). Nếu = 3 thì tất cả đã hoàn thành.
        private static int _nextTreasureToFind = 0;

        // --- Thêm event cho Konami ---
        public static event KonamiActivatedEventHandler KonamiActivated;

        // --- Easter Egg "TAMKHOA" ---
        public static bool ProcessEasterEggTAMKHOA(string input)
        {
            if (string.Equals(input, "TAMKHOA", StringComparison.Ordinal))
            {
                _isEasterEggTAMKHOAActivated = true;
                return true;
            }
            return false;
        }

        public static bool IsEasterEggTAMKHOAActivated()
        {
            return _isEasterEggTAMKHOAActivated;
        }

        public static void OpenRandomLink()
        {
            if (_isEasterEggTAMKHOAActivated)
            {
                try
                {
                    string randomLink = EasterEggLinks[Random.Next(EasterEggLinks.Length)];
                    Process.Start(new ProcessStartInfo(randomLink) { UseShellExecute = true });
                    System.Diagnostics.Debug.WriteLine($"[EasterEgg] Đang mở: {randomLink}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EasterEgg] Lỗi mở link: {ex.Message}");
                }
                _isEasterEggTAMKHOAActivated = false; // Reset sau khi mở
            }
        }

        /// <summary>
        /// Lấy màu nền cho SearchTextBox khi chế độ Bóng tối cực sâu đang bật.
        /// </summary>
        /// <returns>Màu nền.</returns>
        public static Brush GetSearchTextBoxBackground()
        {
            return _isUltraDarkModeActive ? new SolidColorBrush(Color.FromRgb(0, 0, 0)) : null; // Trả về màu đen nếu bật, null nếu không
        }

        /// <summary>
        /// Lấy màu chữ cho SearchTextBox khi chế độ Bóng tối cực sâu đang bật.
        /// </summary>
        /// <returns>Màu chữ.</returns>
        public static Brush GetSearchTextBoxForeground()
        {
            return _isUltraDarkModeActive ? new SolidColorBrush(Color.FromRgb(255, 0, 0)) : null; // Trả về màu đỏ nếu bật, null nếu không
        }

        // --- Easter Egg "Konami Code" ---
        /// <summary>
        /// Kích hoạt hiệu ứng cho Easter Egg Konami Code.
        /// </summary>
        public static void TriggerKonamiEffect()
        {
            // Gọi sự kiện để thông báo cho MainWindow
            KonamiActivated?.Invoke(null, EventArgs.Empty); // Gọi sự kiện
        }

        public static bool IsLegendaryTask(TodoTask task)
        {
            // Điều kiện để một task là "huyền thoại"
            return task.Title == "Lucky";
        }

        public static void ActivateLegendaryTaskEffect(TodoTask task)
        {
            // Hiển thị MessageBox hoặc phát âm thanh (nếu có)
            MessageBox.Show($"Chúc mừng! Bạn đã tìm thấy task huyền thoại '{task.Title}'. Hãy giữ nó để có may mắn!", "Easter Egg Huyền Thoại");
            // Có thể thay đổi icon hoặc màu sắc tạm thời ở đây nếu cần
        }
        public static bool ProcessSpeedMode(string input)
        {
            if (string.Equals(input, "TANGTOCDO", StringComparison.Ordinal))
            {
                _isSpeedModeActive = !_isSpeedModeActive; // Bật/tắt
                return true;
            }
            return false;
        }

        public static bool IsSpeedModeActive()
        {
            return _isSpeedModeActive;
        }

        // Hàm để lấy thời gian tự động đóng thông báo (phụ thuộc chế độ)
        public static int GetNotificationAutoCloseSeconds()
        {
            return _isSpeedModeActive ? 5 : 600; // 5 giây nếu tốc độ, 600 giây (10 phút) nếu không
        }
        public static bool ProcessUltraDarkMode(string input)
        {
            if (string.Equals(input, "BONGTOI", StringComparison.Ordinal))
            {
                _isUltraDarkModeActive = !_isUltraDarkModeActive; // Bật/tắt
                return true;
            }
            return false;
        }

        public static bool IsUltraDarkModeActive()
        {
            return _isUltraDarkModeActive;
        }

        public static Brush GetWindowBackgroundBrush()
        {
            return _isUltraDarkModeActive ? new SolidColorBrush(Color.FromRgb(0, 0, 0)) : null; // Trả về màu đen nếu bật, null nếu không
        }

        public static Brush GetTextForegroundBrush()
        {
            if (_isUltraDarkModeActive)
            {
                // Ví dụ: màu xanh lam neon hoặc tím
                return new SolidColorBrush(Color.FromRgb(0, 255, 255)); // #00FFFF
                // return new SolidColorBrush(Color.FromRgb(218, 112, 214)); // #DA70D6
            }
            return null; // Trả về null nếu không áp dụng
        }

        // --- Easter Egg "Tìm kho báu" (Mới - Click bất kỳ đâu) ---
        /// <summary>
        /// Ghi nhận một lần click từ người dùng vào cửa sổ chính.
        /// </summary>
        public static void IncrementTreasureCount()
        {
            _totalClicks++;

            // Kiểm tra xem có cần hoàn thành vị trí kho báu nào không
            if (_nextTreasureToFind < _treasureFound.Length)
            {
                int targetClicks = _treasureTargets[_nextTreasureToFind];
                if (_totalClicks >= targetClicks && !_treasureFound[_nextTreasureToFind])
                {
                    _treasureFound[_nextTreasureToFind] = true; // Đánh dấu vị trí này đã hoàn thành
                    int treasureNumber = _nextTreasureToFind + 1;
                    MessageBox.Show($"Chúc mừng! Bạn đã tìm thấy kho báu thứ {treasureNumber} sau {_totalClicks} lần click tổng cộng!", "Easter Egg - Kho Báu");
                    System.Diagnostics.Debug.WriteLine($"[EasterEgg] Kho báu {_nextTreasureToFind + 1} đã được tìm thấy sau {_totalClicks} lần click tổng cộng.");

                    // Chuyển sang vị trí tiếp theo
                    _nextTreasureToFind++;

                    // Kiểm tra xem tất cả các kho báu đã được tìm chưa
                    if (_nextTreasureToFind >= _treasureFound.Length)
                    {
                        MessageBox.Show("Chúc mừng! Bạn đã tìm thấy tất cả các kho báu! Đây là phần thưởng đặc biệt!", "Easter Egg - Kho Báu");
                        OpenRandomLink();
                    }
                }
            }
            else
            {
                // Nếu tất cả kho báu đã được tìm, vẫn tăng số lần click nhưng không làm gì thêm
                System.Diagnostics.Debug.WriteLine($"[EasterEgg] Tất cả kho báu đã được tìm thấy. Tổng số click hiện tại: {_totalClicks}.");
            }
        }
        /// <summary>
        /// (Tùy chọn) Đặt lại trạng thái các kho báu và số lần click để chơi lại.
        /// </summary>
        public static void ResetTreasures()
        {
            _totalClicks = 0;
            for (int i = 0; i < _treasureFound.Length; i++)
            {
                _treasureFound[i] = false;
            }
            _nextTreasureToFind = 0;
            System.Diagnostics.Debug.WriteLine("[EasterEgg] Trạng thái kho báu đã được đặt lại.");
        }

        /// <summary>
        /// Lấy tổng số lần click đã thực hiện.
        /// </summary>
        /// <returns>Số lần click.</returns>
        public static int GetTotalClicks()
        {
            return _totalClicks;
        }
    }
}