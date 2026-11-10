using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services.Vehicles;

namespace CRMS.ViewModels
{
    public class VehicleViewModel : BaseViewModel
    {
        private const string AllStatusesLabel = "Tất cả trạng thái";
        private const string AllCategoriesLabel = "Tất cả loại xe";

        private readonly IVehicleService _service;

        public ObservableCollection<VehicleListItem> Vehicles { get; } = new ObservableCollection<VehicleListItem>();
        public ICollectionView VehiclesView { get; }

        public ObservableCollection<string> StatusOptions { get; } = new ObservableCollection<string>
        {
            AllStatusesLabel
        };

        public ObservableCollection<string> CategoryOptions { get; } = new ObservableCollection<string>
        {
            AllCategoriesLabel
        };

        private string _searchText = string.Empty;
        public string SearchText
        {
            get { return _searchText; }
            set
            {
                _searchText = value;
                OnPropertyChanged();
                VehiclesView.Refresh();
                OnPropertyChanged(nameof(FilteredCount));
            }
        }

        private string _selectedStatus = AllStatusesLabel;
        public string SelectedStatus
        {
            get { return _selectedStatus; }
            set
            {
                _selectedStatus = value;
                OnPropertyChanged();
                VehiclesView.Refresh();
                OnPropertyChanged(nameof(FilteredCount));
            }
        }

        private string _selectedCategoryFilter = AllCategoriesLabel;
        public string SelectedCategoryFilter
        {
            get { return _selectedCategoryFilter; }
            set
            {
                _selectedCategoryFilter = value;
                OnPropertyChanged();
                VehiclesView.Refresh();
                OnPropertyChanged(nameof(FilteredCount));
            }
        }

        private VehicleListItem? _selectedVehicle;
        public VehicleListItem? SelectedVehicle
        {
            get { return _selectedVehicle; }
            set
            {
                _selectedVehicle = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get { return _isLoading; }
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public int TotalVehicles
        {
            get { return Vehicles.Count; }
        }

        public int ActiveCount
        {
            get { return Vehicles.Count(v => v.OperationalStatus == "Active"); }
        }

        public int SuspendedCount
        {
            get { return Vehicles.Count(v => v.OperationalStatus == "Suspended"); }
        }

        public int FilteredCount
        {
            get { return VehiclesView.Cast<VehicleListItem>().Count(); }
        }

        private string _statusMessage = "Sẵn sàng tải danh sách xe.";
        public string StatusMessage
        {
            get { return _statusMessage; }
            private set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public Func<long?, Task<bool>>? OpenVehicleEditorAsync { get; set; }
        public Func<Task<bool>>? OpenCategoryManagerAsync { get; set; }
        public Func<string, string, bool>? ConfirmAction { get; set; }
        public Action<string, string>? ShowError { get; set; }
        public Action<long?>? NavigateToAvailability { get; set; }
        public Action<long?>? NavigateToMaintenance { get; set; }

        public ICommand RefreshCommand { get; }
        public ICommand AddVehicleCommand { get; }
        public ICommand EditVehicleCommand { get; }
        public ICommand ToggleStatusCommand { get; }
        public ICommand ManageCategoriesCommand { get; }
        public ICommand OpenAvailabilityCommand { get; }
        public ICommand OpenMaintenanceCommand { get; }

        public VehicleViewModel()
            : this(new VehicleService())
        {
        }

        public VehicleViewModel(IVehicleService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            _service = service;

            VehiclesView = CollectionViewSource.GetDefaultView(Vehicles);
            VehiclesView.Filter = FilterVehicle;

            RefreshCommand = new AsyncRelayCommand(
                async _ => await LoadVehiclesAsync(),
                _ => !IsLoading);

            AddVehicleCommand = new AsyncRelayCommand(
                async _ => await AddVehicleAsync(),
                _ => !IsLoading);

            EditVehicleCommand = new AsyncRelayCommand(
                async param => await EditVehicleAsync((param as VehicleListItem) ?? SelectedVehicle),
                param => !IsLoading && ((param is VehicleListItem) || SelectedVehicle != null));

            ToggleStatusCommand = new AsyncRelayCommand(
                async param => await ToggleVehicleStatusAsync((param as VehicleListItem) ?? SelectedVehicle),
                param => !IsLoading && ((param is VehicleListItem) || SelectedVehicle != null));

            ManageCategoriesCommand = new AsyncRelayCommand(
                async _ => await ManageCategoriesAsync(),
                _ => !IsLoading);

            OpenAvailabilityCommand = new RelayCommand(
                _ =>
                {
                    if (NavigateToAvailability != null)
                    {
                        NavigateToAvailability(SelectedVehicle != null ? SelectedVehicle.VehicleId : (long?)null);
                    }
                },
                _ => !IsLoading);

            OpenMaintenanceCommand = new RelayCommand(
                _ =>
                {
                    if (NavigateToMaintenance != null)
                    {
                        NavigateToMaintenance(SelectedVehicle != null ? SelectedVehicle.VehicleId : (long?)null);
                    }
                },
                _ => !IsLoading);
        }

        private bool FilterVehicle(object obj)
        {
            VehicleListItem? vehicle = obj as VehicleListItem;
            if (vehicle == null)
            {
                return false;
            }

            string keyword = SearchText.Trim();
            bool matchesSearch =
                string.IsNullOrEmpty(keyword) ||
                vehicle.LicensePlate.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                vehicle.Brand.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                vehicle.Model.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                vehicle.CategoryName.Contains(keyword, StringComparison.OrdinalIgnoreCase);

            bool matchesStatus =
                string.IsNullOrEmpty(SelectedStatus) ||
                SelectedStatus == AllStatusesLabel ||
                vehicle.OperationalStatus == SelectedStatus;

            bool matchesCategory =
                string.IsNullOrEmpty(SelectedCategoryFilter) ||
                SelectedCategoryFilter == AllCategoriesLabel ||
                vehicle.CategoryName == SelectedCategoryFilter;

            return matchesSearch && matchesStatus && matchesCategory;
        }

        public async Task LoadVehiclesAsync()
        {
            if (IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Đang tải danh sách xe…";

                long? selectedId = SelectedVehicle != null ? SelectedVehicle.VehicleId : (long?)null;
                var data = await _service.GetVehiclesAsync();

                Vehicles.Clear();
                foreach (VehicleListItem vehicle in data)
                {
                    Vehicles.Add(vehicle);
                }

                string prevStatus = SelectedStatus;
                StatusOptions.Clear();
                StatusOptions.Add(AllStatusesLabel);
                StatusOptions.Add("Active");
                StatusOptions.Add("Suspended");
                foreach (VehicleListItem item in data)
                {
                    if (!string.IsNullOrWhiteSpace(item.OperationalStatus) && !StatusOptions.Contains(item.OperationalStatus))
                    {
                        StatusOptions.Add(item.OperationalStatus);
                    }
                }
                SelectedStatus = StatusOptions.Contains(prevStatus) ? prevStatus : AllStatusesLabel;

                string prevCategory = SelectedCategoryFilter;
                CategoryOptions.Clear();
                CategoryOptions.Add(AllCategoriesLabel);
                foreach (string cat in data
                    .Select(v => v.CategoryName)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c))
                {
                    CategoryOptions.Add(cat);
                }
                SelectedCategoryFilter = CategoryOptions.Contains(prevCategory) ? prevCategory : AllCategoriesLabel;

                SelectedVehicle = selectedId.HasValue
                    ? Vehicles.FirstOrDefault(v => v.VehicleId == selectedId.Value)
                    : null;

                VehiclesView.Refresh();
                OnPropertyChanged(nameof(TotalVehicles));
                OnPropertyChanged(nameof(ActiveCount));
                OnPropertyChanged(nameof(SuspendedCount));
                OnPropertyChanged(nameof(FilteredCount));

                StatusMessage = $"Cập nhật lúc {DateTime.Now:HH:mm} · {data.Count} xe ({ActiveCount} hoạt động, {SuspendedCount} tạm ngưng)";
            }
            catch (Exception ex)
            {
                StatusMessage = "Không thể tải danh sách xe. Kiểm tra kết nối cơ sở dữ liệu và nhấn Làm mới để thử lại.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsLoading = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task AddVehicleAsync()
        {
            if (OpenVehicleEditorAsync == null)
            {
                return;
            }

            if (await OpenVehicleEditorAsync(null))
            {
                await LoadVehiclesAsync();
                StatusMessage = "Đã thêm phương tiện mới thành công.";
            }
        }

        private async Task EditVehicleAsync(VehicleListItem? item)
        {
            if (item == null || OpenVehicleEditorAsync == null)
            {
                return;
            }

            if (await OpenVehicleEditorAsync(item.VehicleId))
            {
                await LoadVehiclesAsync();
                StatusMessage = $"Đã cập nhật thông tin xe {item.LicensePlate}.";
            }
        }

        private async Task ToggleVehicleStatusAsync(VehicleListItem? item)
        {
            if (item == null)
            {
                return;
            }

            string targetStatus = item.OperationalStatus == "Active" ? "Suspended" : "Active";
            string actionText = targetStatus == "Suspended" ? "tạm ngưng hoạt động" : "kích hoạt lại";

            if (ConfirmAction != null)
            {
                bool confirmed = ConfirmAction(
                    $"Bạn có chắc muốn {actionText} xe {item.Brand} {item.Model} ({item.LicensePlate})?",
                    "Xác nhận thay đổi trạng thái xe");
                if (!confirmed)
                {
                    return;
                }
            }

            try
            {
                IsLoading = true;
                await _service.SetVehicleStatusAsync(item.VehicleId, targetStatus);
                IsLoading = false;
                await LoadVehiclesAsync();
                StatusMessage = $"Đã {actionText} xe {item.LicensePlate}.";
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                if (ShowError != null)
                {
                    ShowError(ex.Message, "Không thể đổi trạng thái xe");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Đã xảy ra lỗi khi cập nhật trạng thái xe.";
                if (ShowError != null)
                {
                    ShowError("Không thể cập nhật trạng thái xe. Vui lòng thử lại.", "Lỗi hệ thống");
                }
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsLoading = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task ManageCategoriesAsync()
        {
            if (OpenCategoryManagerAsync == null)
            {
                return;
            }

            if (await OpenCategoryManagerAsync())
            {
                await LoadVehiclesAsync();
            }
        }
    }
}
