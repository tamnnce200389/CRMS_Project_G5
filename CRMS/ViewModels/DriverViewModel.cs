
using CRMS.Helpers;
using CRMS.Models;
using CRMS.Repositories;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CRMS.ViewModels
{
    public class DriverViewModel : BaseViewModel
    {
        private readonly DriverRepository _repository;

        public ObservableCollection<Driver> Drivers { get; }
            = new ObservableCollection<Driver>();

        public ICommand RefreshCommand { get; }
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        private string _statusMessage = "Sẵn sàng tải danh sách tài xế.";
        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }
        public int TotalDrivers => Drivers.Count;
        public int ActiveDrivers => Drivers.Count(d => d.IsActive);

        public DriverViewModel()
        {
            _repository = new DriverRepository();

            RefreshCommand = new RelayCommand(
                async _ => await LoadDriversAsync(), _ => !IsLoading);
        }

        public async Task LoadDriversAsync()
        {
            if (IsLoading) return;
            try
            {
                IsLoading = true;
                StatusMessage = "Đang tải danh sách tài xế…";
                var drivers = await _repository.GetAllAsync();
                Drivers.Clear();
                foreach (var driver in drivers) Drivers.Add(driver);
                OnPropertyChanged(nameof(TotalDrivers));
                OnPropertyChanged(nameof(ActiveDrivers));
                StatusMessage = $"Cập nhật lúc {DateTime.Now:HH:mm} · {drivers.Count} tài xế";
            }
            catch (Exception ex)
            {
                StatusMessage = "Không thể tải danh sách tài xế. Kiểm tra kết nối cơ sở dữ liệu và nhấn Làm mới để thử lại.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally { IsLoading = false; }
        }
    }
}

