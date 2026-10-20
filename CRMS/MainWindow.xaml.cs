
using CRMS.Services;
using CRMS.ViewModels;
using CRMS.Views;
using System.Windows;

namespace CRMS
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Mo cua so Login
        private void Login_Click(
            object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow
            {
                Owner = this
            };

            bool? result = loginWindow.ShowDialog();

            if (result == true &&
                DataContext is MainViewModel vm)
            {
                // Cap nhat Sidebar theo Role
                vm.RefreshSession();
            }
        }

        // Dang xuat
        private void Logout_Click(
            object sender, RoutedEventArgs e)
        {
            CurrentUserService.Instance.Logout();

            if (DataContext is MainViewModel vm)
            {
                // Quay ve giao dien Guest
                vm.RefreshSession();
            }
        }
    }
}
