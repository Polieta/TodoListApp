using ControlzEx.Theming;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace TodoListApp
{
    public partial class SettingsWindow : Window
    {
        public string SelectedSoundPath { get; set; }
        public string SelectedImagePath { get; set; } // Thêm thuộc tính mới

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Load theme
            if (Properties.Settings.Default.IsDarkMode)
                DarkThemeRadio.IsChecked = true;
            else
                LightThemeRadio.IsChecked = true;

            // Load sound
            SelectedSoundPath = Properties.Settings.Default.NotificationSound;
            SelectedSoundText.Text = string.IsNullOrEmpty(SelectedSoundPath)
                ? "Chưa chọn file"
                : Path.GetFileName(SelectedSoundPath);

            // Load image
            SelectedImagePath = Properties.Settings.Default.NotificationBackgroundImage; // Giả sử bạn đã thêm vào Settings
            SelectedImageText.Text = string.IsNullOrEmpty(SelectedImagePath)
                ? "Chưa chọn file"
                : Path.GetFileName(SelectedImagePath);
        }

        private void SelectSound_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Wave Files (*.wav)|*.wav",
                Title = "Chọn file âm thanh thông báo"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string sourcePath = ofd.FileName;
                    string fileName = Path.GetFileName(sourcePath);
                    string soundDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds");

                    // Đảm bảo thư mục tồn tại
                    Directory.CreateDirectory(soundDir);

                    string destinationPath = Path.Combine(soundDir, fileName);

                    // Copy file
                    File.Copy(sourcePath, destinationPath, true);

                    // Lưu vào Settings
                    SelectedSoundPath = destinationPath;
                    SelectedSoundText.Text = fileName;
                    Properties.Settings.Default.NotificationSound = destinationPath;
                    Properties.Settings.Default.Save();

                    MessageBox.Show("✅ Đã lưu file âm thanh!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SelectImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files (*.*)|*.*", // Thêm các định dạng ảnh phổ biến
                Title = "Chọn file ảnh nền thông báo"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string sourcePath = ofd.FileName;
                    string fileName = Path.GetFileName(sourcePath);
                    string imageDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images"); // Thư mục riêng cho ảnh

                    // Đảm bảo thư mục tồn tại
                    Directory.CreateDirectory(imageDir);

                    string destinationPath = Path.Combine(imageDir, fileName);

                    // Copy file
                    File.Copy(sourcePath, destinationPath, true);

                    // Lưu vào Settings
                    SelectedImagePath = destinationPath;
                    SelectedImageText.Text = fileName;
                    Properties.Settings.Default.NotificationBackgroundImage = destinationPath; // Cập nhật tên cài đặt
                    Properties.Settings.Default.Save();

                    MessageBox.Show("✅ Đã lưu file ảnh!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Contact_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "mailto:nqcuong13032003@gmail.com",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể mở email. Lỗi: " + ex.Message);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            bool isDarkMode = DarkThemeRadio.IsChecked == true;
            Properties.Settings.Default.IsDarkMode = isDarkMode;
            Properties.Settings.Default.Save();

            if (this.Owner is MainWindow mainWindow)
            {
                mainWindow.ApplyMahAppsTheme(isDarkMode);
            }

            this.Close();
        }
    }
}