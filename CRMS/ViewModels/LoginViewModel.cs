
using CRMS.Helpers;
using CRMS.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CRMS.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private readonly AuthService _authService = new();

        private string _email = "";
        public string Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged();
            }
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoading;

        public bool IsNotLoading => !_isLoading;

        public ICommand LoginCommand { get; }

        public event Action? LoginSucceeded;

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(
                async parameter =>
                    await LoginAsync(parameter as string),
                _ => !_isLoading);
        }

        private async Task LoginAsync(string? password)
        {
            if (_isLoading)
                return;

            try
            {
                _isLoading = true;
                OnPropertyChanged(nameof(IsNotLoading));
                CommandManager.InvalidateRequerySuggested();

                ErrorMessage = "";

                var result = await _authService.LoginAsync(
                    Email, password ?? "");

                if (result.Success)
                {
                    LoginSucceeded?.Invoke();
                }
                else
                {
                    ErrorMessage = result.Message;
                }
            }
            catch (Exception)
            {
                ErrorMessage =
                    "Cannot connect to database. Please try again.";
            }
            finally
            {
                _isLoading = false;
                OnPropertyChanged(nameof(IsNotLoading));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}
