
using CRMS.ViewModels;
using System.Windows;

namespace CRMS.Views
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;

        public LoginWindow()
        {
            InitializeComponent();

            _viewModel = new LoginViewModel();
            DataContext = _viewModel;

            // Dang nhap thanh cong thi dong LoginWindow
            _viewModel.LoginSucceeded += () =>
            {
                DialogResult = true;
            };
        }

        private void Login_Click(
            object sender, RoutedEventArgs e)
        {
            string password = PasswordInput.Password;

            if (_viewModel.LoginCommand.CanExecute(password))
            {
                _viewModel.LoginCommand.Execute(password);
            }
        }

        // Giu lai OpenRegister_Click neu da tao o buoc truoc
        private void OpenRegister_Click(
            object sender, RoutedEventArgs e)
        {
            RegisterWindow registerWindow = new RegisterWindow
            {
                Owner = this
            };

            bool? result = registerWindow.ShowDialog();

            if (result == true)
            {
                MessageBox.Show(
                    "Đăng ký thành công! Hãy đăng nhập.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}
