using System.Windows.Controls;
using CRMS.ViewModels;
using CRMS.Views;
using System.Windows;
using CRMS.Services;

namespace CRMS.Views
{
    public partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
        }


        // Mo cua so dang nhap

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            // Lay cua so MainWindow dang chua HomePage
            Window? mainWindow = Window.GetWindow(this);

            // Tao cua so dang nhap
            LoginWindow loginWindow = new LoginWindow();

            if (mainWindow != null)
            {
                loginWindow.Owner = mainWindow;
            }

            // Hien thi LoginWindow
            bool? result = loginWindow.ShowDialog();

            // Neu dang nhap thanh cong
            if (result == true)
            {
                if (mainWindow?.DataContext is MainViewModel vm)
                {
                    // Cap nhat giao dien theo Role
                    vm.RefreshSession();
                }
            }
        }


        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            CurrentUserService.Instance.Logout();

            Window? mainWindow = Window.GetWindow(this);

            if (mainWindow?.DataContext is MainViewModel vm)
            {
                vm.RefreshSession();
            }
        }

        // Mo cua so dang ky
        private void Register_Click(
            object sender, RoutedEventArgs e)
        {
            Window? mainWindow = Window.GetWindow(this);

            var registerWindow = new RegisterWindow
            {
                Owner = mainWindow
            };

            bool? result = registerWindow.ShowDialog();

            if (result == true)
            {
                MessageBox.Show(
                    "Đăng ký thành công! Hãy đăng nhập để tiếp tục.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

    }
}