
using CRMS.Helpers;
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

        public ICommand SelectModeCommand { get; }

        public HomeViewModel()
        {
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
