using CRMS.Helpers;
using CRMS.Services;
using System.Windows.Input;

namespace CRMS.ViewModels
{
    public class HomeViewModel : BaseViewModel
    {
        private string _selectedMode = "SelfDrive";

        public string SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (_selectedMode == value)
                    return;

                _selectedMode = value;
                OnPropertyChanged();

                OnPropertyChanged(nameof(IsSelfDrive));
                OnPropertyChanged(nameof(IsWithDriver));
                OnPropertyChanged(nameof(IsLongTerm));
            }
        }

        public bool IsSelfDrive =>
            SelectedMode == "SelfDrive";

        public bool IsWithDriver =>
            SelectedMode == "WithDriver";

        public bool IsLongTerm =>
            SelectedMode == "LongTerm";

        public bool IsGuest =>
            CurrentUserService.Instance.IsGuest;

        public bool IsLoggedIn =>
            CurrentUserService.Instance.IsLoggedIn;

        public bool IsCustomer =>
            CurrentUserService.Instance.IsCustomer;

        public ICommand SelectModeCommand { get; }

        public HomeViewModel()
        {
            CurrentUserService.Instance.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(IsGuest));
                OnPropertyChanged(nameof(IsLoggedIn));
                OnPropertyChanged(nameof(IsCustomer));
            };

            SelectModeCommand = new RelayCommand(parameter =>
            {
                string? mode = parameter?.ToString();

                if (mode == "SelfDrive" ||
                    mode == "WithDriver" ||
                    mode == "LongTerm")
                {
                    SelectedMode = mode;
                }
            });
        }
    }
}
