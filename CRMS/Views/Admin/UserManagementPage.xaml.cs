
using CRMS.Data;
using CRMS.Models;
using CRMS.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CRMS.Views.Admin
{
    public partial class UserManagementPage : Page
    {
        private List<User> _allUsers = new();

        public UserManagementPage()
        {
            InitializeComponent();
            LoadUsers();
        }

        // Load danh sach tai khoan
        private void LoadUsers()
        {
            try
            {
                using var db = CrmsContextFactory.Create();

                _allUsers = db.Users
                    .AsNoTracking()
                    .OrderBy(u => u.UserId)
                    .ToList();

                ApplySearch();
                SelectedUserText.Text =
                    $"Tổng số tài khoản: {_allUsers.Count}";

                RoleComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không thể tải danh sách người dùng.\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Tim theo ID, ten, email, so dien thoai
        private void ApplySearch()
        {
            string keyword = SearchBox.Text.Trim();

            var result = _allUsers.Where(u =>
                u.UserId.ToString().Contains(keyword,
                    StringComparison.OrdinalIgnoreCase)
                || (u.FullName ?? "").Contains(keyword,
                    StringComparison.OrdinalIgnoreCase)
                || (u.Email ?? "").Contains(keyword,
                    StringComparison.OrdinalIgnoreCase)
                || (u.Phone ?? "").Contains(keyword,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            UsersGrid.ItemsSource = result;
        }

        private void SearchBox_TextChanged(
            object sender, TextChangedEventArgs e)
        {
            ApplySearch();
        }

        private void Reload_Click(
            object sender, RoutedEventArgs e)
        {
            LoadUsers();
        }

        // Hien thong tin tai khoan duoc chon
        private void UsersGrid_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (UsersGrid.SelectedItem is not User user)
            {
                SelectedUserText.Text =
                    "Chọn một tài khoản trong danh sách.";
                return;
            }

            SelectedUserText.Text =
                $"Đã chọn: #{user.UserId} - {user.FullName} ({user.Email})"
                + (user.IsActive ? " | Đang hoạt động" : " | Đã khóa");

            foreach (ComboBoxItem item in RoleComboBox.Items)
            {
                if (item.Content?.ToString() == user.UserRole)
                {
                    RoleComboBox.SelectedItem = item;
                    break;
                }
            }

            bool isSelf =
                CurrentUserService.Instance.UserId == user.UserId;

            ToggleStatusButton.IsEnabled = !isSelf;
            RoleComboBox.IsEnabled = !isSelf;
        }

        // Doi role cua tai khoan
        private void ChangeRole_Click(
            object sender, RoutedEventArgs e)
        {
            if (!CurrentUserService.Instance.IsAdmin)
            {
                MessageBox.Show("Bạn không có quyền thực hiện thao tác này.");
                return;
            }

            if (UsersGrid.SelectedItem is not User selectedUser)
            {
                MessageBox.Show("Hãy chọn một tài khoản trước.");
                return;
            }

            if (CurrentUserService.Instance.UserId == selectedUser.UserId)
            {
                MessageBox.Show("Bạn không thể tự đổi vai trò của mình.");
                return;
            }

            if (RoleComboBox.SelectedItem is not ComboBoxItem roleItem)
            {
                MessageBox.Show("Hãy chọn vai trò hợp lệ.");
                return;
            }

            string newRole = roleItem.Content?.ToString() ?? "";

            if (newRole != "Customer"
                && newRole != "Staff"
                && newRole != "Admin")
            {
                MessageBox.Show("Vai trò không hợp lệ.");
                return;
            }

            if (newRole == selectedUser.UserRole)
            {
                MessageBox.Show("Tài khoản đã có vai trò này.");
                return;
            }

            var confirm = MessageBox.Show(
                $"Đổi vai trò của {selectedUser.FullName} "
                + $"từ {selectedUser.UserRole} thành {newRole}?",
                "Xác nhận đổi vai trò",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                using var db = CrmsContextFactory.Create();

                var user = db.Users.FirstOrDefault(
                    u => u.UserId == selectedUser.UserId);

                if (user == null)
                {
                    MessageBox.Show("Không tìm thấy tài khoản.");
                    LoadUsers();
                    return;
                }

                // Kiem tra lai tai khoan hien tai
                if (CurrentUserService.Instance.UserId == user.UserId)
                {
                    MessageBox.Show("Không thể tự đổi vai trò.");
                    return;
                }

                user.UserRole = newRole;
                user.UpdatedAt = DateTime.UtcNow;

                db.SaveChanges();

                MessageBox.Show(
                    "Đổi vai trò thành công.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                LoadUsers();
            }
            catch (DbUpdateConcurrencyException)
            {
                MessageBox.Show(
                    "Tài khoản vừa được cập nhật ở nơi khác. Hãy tải lại danh sách.");
                LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không thể đổi vai trò.\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Khoa / mo khoa tai khoan
        private void ToggleStatus_Click(
            object sender, RoutedEventArgs e)
        {
            if (!CurrentUserService.Instance.IsAdmin)
            {
                MessageBox.Show("Bạn không có quyền thực hiện thao tác này.");
                return;
            }

            if (UsersGrid.SelectedItem is not User selectedUser)
            {
                MessageBox.Show("Hãy chọn một tài khoản trước.");
                return;
            }

            if (CurrentUserService.Instance.UserId == selectedUser.UserId)
            {
                MessageBox.Show("Bạn không thể tự khóa tài khoản của mình.");
                return;
            }

            string action = selectedUser.IsActive ? "khóa" : "mở khóa";

            var confirm = MessageBox.Show(
                $"Bạn có chắc muốn {action} tài khoản "
                + $"{selectedUser.FullName}?",
                "Xác nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                using var db = CrmsContextFactory.Create();

                var user = db.Users.FirstOrDefault(
                    u => u.UserId == selectedUser.UserId);

                if (user == null)
                {
                    MessageBox.Show("Không tìm thấy tài khoản.");
                    LoadUsers();
                    return;
                }

                // Kiem tra lai truoc khi cap nhat
                if (CurrentUserService.Instance.UserId == user.UserId)
                {
                    MessageBox.Show("Không thể tự khóa tài khoản.");
                    return;
                }

                user.IsActive = !user.IsActive;
                user.UpdatedAt = DateTime.UtcNow;

                db.SaveChanges();

                MessageBox.Show(
                    user.IsActive
                        ? "Đã mở khóa tài khoản."
                        : "Đã khóa tài khoản.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                LoadUsers();
            }
            catch (DbUpdateConcurrencyException)
            {
                MessageBox.Show(
                    "Tài khoản vừa được cập nhật ở nơi khác. Hãy tải lại danh sách.");
                LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không thể cập nhật trạng thái tài khoản.\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
