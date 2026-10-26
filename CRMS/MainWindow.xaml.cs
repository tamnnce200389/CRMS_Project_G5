
using CRMS.Services;
using CRMS.ViewModels;
using System.Windows;

namespace CRMS
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Logout_Click(
            object sender,
            RoutedEventArgs e)
        {
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
