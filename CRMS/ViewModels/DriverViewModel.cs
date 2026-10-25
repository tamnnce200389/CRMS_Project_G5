
using CRMS.Helpers;
using CRMS.Models;
using CRMS.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CRMS.ViewModels
{
    public class DriverViewModel : BaseViewModel
    {
        private readonly DriverRepository _repository;
        private long? _editingDriverId;

        public ObservableCollection<Driver> Drivers { get; }
            = new ObservableCollection<Driver>();

        public ICommand RefreshCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ToggleActiveCommand { get; }

        private Driver? _selectedDriver;

        public Driver? SelectedDriver
        {
            get => _selectedDriver;
            set
            {
                if (_selectedDriver == value) return;

                _selectedDriver = value;
                OnPropertyChanged();

                if (value != null)
                {
                    _editingDriverId = value.DriverId;
                    FullName = value.FullName;
                    Phone = value.Phone ?? "";
                    LicenseNumber = value.LicenseNumber ?? "";
                    Notes = value.Notes ?? "";
                    IsActive = value.IsActive;
                }

                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _fullName = "";
        public string FullName
        {
            get => _fullName;
            set
            {
                _fullName = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _phone = "";
        public string Phone
        {
            get => _phone;
            set { _phone = value; OnPropertyChanged(); }
        }

        private string _licenseNumber = "";
        public string LicenseNumber
        {
            get => _licenseNumber;
            set { _licenseNumber = value; OnPropertyChanged(); }
        }

        private string _notes = "";
        public string Notes
        {
            get => _notes;
            set { _notes = value; OnPropertyChanged(); }
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private bool _isLoading;

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                _isLoading = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _statusMessage =
            "Sẵn sàng tải danh sách tài xế.";

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public int TotalDrivers => Drivers.Count;
        public int ActiveDrivers => Drivers.Count(d => d.IsActive);

        public DriverViewModel()
        {
            _repository = new DriverRepository();

            RefreshCommand = new RelayCommand(
                async _ => await LoadDriversAsync(),
                _ => !IsLoading);

            NewCommand = new RelayCommand(
                _ => ClearForm(),
                _ => !IsLoading);

            SaveCommand = new RelayCommand(
                async _ => await SaveDriverAsync(),
                _ => !IsLoading &&
                     !string.IsNullOrWhiteSpace(FullName));

            ToggleActiveCommand = new RelayCommand(
                async _ => await ToggleActiveAsync(),
                _ => !IsLoading && SelectedDriver != null);
        }

        public async Task LoadDriversAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Đang tải danh sách tài xế...";

                var drivers = await _repository.GetAllAsync();

                Drivers.Clear();

                foreach (var driver in drivers)
                    Drivers.Add(driver);

                OnPropertyChanged(nameof(TotalDrivers));
                OnPropertyChanged(nameof(ActiveDrivers));

                StatusMessage =
                    $"Cập nhật lúc {DateTime.Now:HH:mm} · " +
                    $"{drivers.Count} tài xế";
            }
            catch (Exception ex)
            {
                StatusMessage =
                    "Không thể tải danh sách tài xế. " +
                    "Hãy kiểm tra kết nối database.";

                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ClearForm()
        {
            _editingDriverId = null;
            SelectedDriver = null;

            FullName = "";
            Phone = "";
            LicenseNumber = "";
            Notes = "";
            IsActive = true;

            StatusMessage = "Đang tạo thông tin tài xế mới.";
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task SaveDriverAsync()
        {
            if (IsLoading) return;

            if (string.IsNullOrWhiteSpace(FullName))
            {
                StatusMessage = "Vui lòng nhập họ tên tài xế.";
                return;
            }

            bool isEditing = _editingDriverId.HasValue;
            bool saved = false;

            string successMessage = isEditing
                ? "Cập nhật tài xế thành công."
                : "Thêm tài xế thành công.";

            try
            {
                IsLoading = true;
                StatusMessage = "Đang lưu thông tin...";

                var driver = new Driver
                {
                    DriverId = _editingDriverId ?? 0,
                    FullName = FullName.Trim(),
                    Phone = string.IsNullOrWhiteSpace(Phone)
                        ? null : Phone.Trim(),
                    LicenseNumber =
                        string.IsNullOrWhiteSpace(LicenseNumber)
                            ? null : LicenseNumber.Trim(),
                    Notes = string.IsNullOrWhiteSpace(Notes)
                        ? null : Notes.Trim(),
                    IsActive = IsActive
                };

                if (isEditing)
                    await _repository.UpdateAsync(driver);
                else
                    await _repository.AddAsync(driver);

                saved = true;
            }
            catch (Exception ex)
            {
                StatusMessage = "Lưu tài xế thất bại. Hãy kiểm tra dữ liệu.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsLoading = false;
            }

            if (saved)
            {
                ClearForm();
                await LoadDriversAsync();
                StatusMessage = successMessage;
            }
        }

        private async Task ToggleActiveAsync()
        {
            if (IsLoading || SelectedDriver == null)
                return;

            var driver = SelectedDriver;
            bool newState = !driver.IsActive;
            bool updated = false;

            try
            {
                IsLoading = true;
                StatusMessage = "Đang cập nhật trạng thái tài xế...";

                updated = await _repository.SetActiveAsync(
                    driver.DriverId, newState);

                if (!updated)
                {
                    StatusMessage = "Không tìm thấy tài xế cần cập nhật.";
                    return;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Cập nhật trạng thái thất bại.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsLoading = false;
            }

            if (updated)
            {
                ClearForm();
                await LoadDriversAsync();

                StatusMessage = newState
                    ? "Đã kích hoạt tài xế."
                    : "Đã ngừng hoạt động tài xế.";
            }
        }
    }
}
