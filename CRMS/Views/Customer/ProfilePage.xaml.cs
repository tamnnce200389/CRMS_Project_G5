
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CRMS.Data;
using CRMS.Models;
using CRMS.Services;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Views.Customer
{
    public partial class ProfilePage : Page
    {
        private long _userId;

        public ProfilePage()
        {
            InitializeComponent();
        }

        private async void Page_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (!CurrentUserService.Instance.IsLoggedIn)
            {
                MessageBox.Show(
                    "Vui lòng đăng nhập để xem hồ sơ.",
                    "Chưa đăng nhập",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            _userId = CurrentUserService.Instance.UserId!.Value;

            await LoadProfileAsync();
        }

        private async Task LoadProfileAsync()
        {
            try
            {
                using var db = CrmsContextFactory.Create();

                var user = await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        u => u.UserId == _userId);

                if (user == null)
                {
                    txtMessage.Text = "Không tìm thấy tài khoản.";
                    txtMessage.Foreground = Brushes.Red;
                    return;
                }

                txtUserId.Text = user.UserId.ToString();
                txtFullName.Text = user.FullName;
                txtEmail.Text = user.Email;
                txtPhone.Text = user.Phone ?? "";
                txtRole.Text = user.UserRole switch { "Customer" => "Khách hàng", "Staff" => "Nhân viên", "Admin" => "Quản trị viên", _ => user.UserRole };
                txtStatus.Text = user.IsActive
                    ? "Đang hoạt động"
                    : "Ngừng hoạt động";

                txtMessage.Text = "";
            }
            catch (Exception ex)
            {
                txtMessage.Text =
                    "Không thể tải hồ sơ: " + ex.Message;

                txtMessage.Foreground = Brushes.Red;
            }
        }

        private async void SaveChanges_Click(
            object sender, RoutedEventArgs e)
        {
            string fullName = txtFullName.Text.Trim();
            string phone = txtPhone.Text.Trim();

            if (fullName.Length < 2 || fullName.Length > 120)
            {
                MessageBox.Show(
                    "Họ tên cần có từ 2 đến 120 ký tự.",
                    "Kiểm tra thông tin",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (phone.Length > 20)
            {
                MessageBox.Show(
                    "Số điện thoại không được quá 20 ký tự.",
                    "Kiểm tra thông tin",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                using var db = CrmsContextFactory.Create();

                var user = await db.Users.FirstOrDefaultAsync(
                    u => u.UserId == _userId);

                if (user == null)
                {
                    MessageBox.Show("Không tìm thấy tài khoản.");
                    return;
                }

                user.FullName = fullName;
                user.Phone = string.IsNullOrWhiteSpace(phone)
                    ? null
                    : phone;

                user.UpdatedAt = DateTime.UtcNow;

                await db.SaveChangesAsync();
                if (CurrentUserService.Instance.UserId == user.UserId)
                    CurrentUserService.Instance.SetUser(user);

                txtMessage.Text = "Đã lưu thông tin hồ sơ.";
                txtMessage.Foreground = Brushes.Green;

                MessageBox.Show(
                    "Thông tin của bạn đã được cập nhật.",
                    "Thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadProfileAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                MessageBox.Show(
                    "Tài khoản đã được thay đổi ở nơi khác. Vui lòng tải lại hồ sơ và thử lại.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không thể cập nhật hồ sơ: " + ex.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            var vm = Application.Current.MainWindow.DataContext
                as CRMS.ViewModels.MainViewModel;

            if (vm == null)
                return;

            if (CRMS.Services.CurrentUserService.Instance.IsAdmin)
            {
                vm.NavigateCommand.Execute("Dashboard");
            }
            else if (CRMS.Services.CurrentUserService.Instance.IsStaff)
            {
                vm.NavigateCommand.Execute("Dashboard");
            }
            else
            {
                vm.NavigateCommand.Execute("Home");
            }
        }
    }
}
