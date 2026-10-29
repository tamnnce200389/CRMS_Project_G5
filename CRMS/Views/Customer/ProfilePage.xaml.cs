
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
                    "Please log in to view your profile.",
                    "Access denied",
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
                    txtMessage.Text = "Account not found.";
                    txtMessage.Foreground = Brushes.Red;
                    return;
                }

                txtUserId.Text = user.UserId.ToString();
                txtFullName.Text = user.FullName;
                txtEmail.Text = user.Email;
                txtPhone.Text = user.Phone ?? "";
                txtRole.Text = user.UserRole;
                txtStatus.Text = user.IsActive
                    ? "Active"
                    : "Inactive";

                txtMessage.Text = "";
            }
            catch (Exception ex)
            {
                txtMessage.Text =
                    "Could not load profile: " + ex.Message;

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
                    "Full name must contain 2-120 characters.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (phone.Length > 20)
            {
                MessageBox.Show(
                    "Phone number cannot exceed 20 characters.",
                    "Validation",
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
                    MessageBox.Show("Account not found.");
                    return;
                }

                user.FullName = fullName;
                user.Phone = string.IsNullOrWhiteSpace(phone)
                    ? null
                    : phone;

                user.UpdatedAt = DateTime.UtcNow;

                await db.SaveChangesAsync();

                txtMessage.Text = "Profile updated successfully!";
                txtMessage.Foreground = Brushes.Green;

                MessageBox.Show(
                    "Your profile has been updated.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadProfileAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                MessageBox.Show(
                    "Your account was changed elsewhere. Reload the profile and try again.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not update profile: " + ex.Message,
                    "Error",
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
