
using CRMS.Services;
using CRMS.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CRMS
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Account_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            AccountMenu.PlacementTarget = button;
            AccountMenu.HorizontalOffset = button.ActualWidth - AccountMenu.Width;
            AccountMenu.IsOpen = true;
        }

        private void Logout_Click(
            object sender,
            RoutedEventArgs e)
        {
            AccountMenu.IsOpen = false;
            // Xoa phien dang nhap
            CurrentUserService.Instance.Logout();

            // Cap nhat lai UI theo role
            if (DataContext is MainViewModel vm)
            {
                vm.RefreshSession();
            }
        }
    }
}
