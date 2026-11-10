
using CRMS.ViewModels;
using System.Windows;

namespace CRMS.Views
{
    public partial class RegisterWindow : Window
    {
        private readonly RegisterViewModel _viewModel;

        public RegisterWindow()
        {
            InitializeComponent();

            _viewModel = new RegisterViewModel();

            DataContext = _viewModel;

            _viewModel.RegistrationSucceeded += () =>
            {
                DialogResult = true;
            };
        }

        private void Register_Click(
            object sender, RoutedEventArgs e)
        {
            string password = PasswordInput.Password;
            string confirmPassword = ConfirmPasswordInput.Password;

            var passwords = (password, confirmPassword);

            if (_viewModel.RegisterCommand.CanExecute(passwords))
            {
                _viewModel.RegisterCommand.Execute(passwords);
            }
        }

        private void BackToLogin_Click(
            object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
