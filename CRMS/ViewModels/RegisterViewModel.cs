
using CRMS.Helpers;
using CRMS.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CRMS.ViewModels
{
    public class RegisterViewModel : BaseViewModel
    {
        private readonly AuthService _authService = new();

        private string _fullName = "";
        public string FullName
        {
            get => _fullName;
            set
            {
                _fullName = value;
                OnPropertyChanged();
            }
        }

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

        private string _phone = "";
        public string Phone
        {
            get => _phone;
            set
            {
                _phone = value;
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

        public ICommand RegisterCommand { get; }

        public event Action? RegistrationSucceeded;

        public RegisterViewModel()
        {
            RegisterCommand = new RelayCommand(
                async parameter =>
                {
                    if (parameter is ValueTuple<string, string> passwords)
                    {
                        await RegisterAsync(
                            passwords.Item1,
                            passwords.Item2);
                    }
                },
                _ => !_isLoading);
        }

        private async Task RegisterAsync(
            string password,
            string confirmPassword)
        {
            if (_isLoading)
                return;

            try
            {
                _isLoading = true;
                OnPropertyChanged(nameof(IsNotLoading));
                CommandManager.InvalidateRequerySuggested();

                ErrorMessage = "";

                var result = await _authService.RegisterAsync(
                    FullName,
                    Email,
                    Phone,
                    password,
                    confirmPassword);

                if (result.Success)
                {
                    RegistrationSucceeded?.Invoke();
                }
                else
                {
                    ErrorMessage = result.Message;
                }
            }
            catch (Exception)
            {
                ErrorMessage =
                    "Registration failed. Please try again.";
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
