using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services.Vehicles;

namespace CRMS.ViewModels;

public class VehicleViewModel : BaseViewModel
{
    private const string AllStatusesLabel = "Tất cả trạng thái";
    private const string AllCategoriesLabel = "Tất cả loại xe";

    private readonly IVehicleService _service;

    public ObservableCollection<VehicleListItem> Vehicles { get; } = new();
    public ICollectionView VehiclesView { get; }

    public ObservableCollection<string> StatusOptions { get; } = new()
    {
        AllStatusesLabel
    };

    public ObservableCollection<string> CategoryOptions { get; } = new()
    {
        AllCategoriesLabel
    };

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
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
        get => _selectedStatus;
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
        get => _selectedCategoryFilter;
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
        get => _selectedVehicle;
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
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public int TotalVehicles => Vehicles.Count;
    public int ActiveCount => Vehicles.Count(v => v.OperationalStatus == "Active");
    public int SuspendedCount => Vehicles.Count(v => v.OperationalStatus == "Suspended");
    public int FilteredCount => VehiclesView.Cast<VehicleListItem>().Count();

    private string _statusMessage = "Sẵn sàng tải danh sách xe.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    // View hooks wired by Admin VehiclePage code-behind (keeps ViewModel UI-framework free for unit tests)
    public Func<long?, Task<bool>>? OpenVehicleEditorAsync { get; set; }
    public Func<Task<bool>>? OpenCategoryManagerAsync { get; set; }
    public Func<string, string, bool>? ConfirmAction { get; set; }
    public Action<string, string>? ShowError { get; set; }

    public ICommand RefreshCommand { get; }
    public ICommand AddVehicleCommand { get; }
    public ICommand EditVehicleCommand { get; }
    public ICommand ToggleStatusCommand { get; }
    public ICommand ManageCategoriesCommand { get; }

    public VehicleViewModel()
        : this(new VehicleService())
    {
    }

    public VehicleViewModel(IVehicleService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));

        VehiclesView = CollectionViewSource.GetDefaultView(Vehicles);
        VehiclesView.Filter = FilterVehicle;

        RefreshCommand = new RelayCommand(
            async _ => await LoadVehiclesAsync(),
            _ => !IsLoading);

        AddVehicleCommand = new RelayCommand(
            async _ => await AddVehicleAsync(),
            _ => !IsLoading);

        EditVehicleCommand = new RelayCommand(
            async param => await EditVehicleAsync(param as VehicleListItem ?? SelectedVehicle),
            param => !IsLoading && (param is VehicleListItem || SelectedVehicle != null));

        ToggleStatusCommand = new RelayCommand(
            async param => await ToggleVehicleStatusAsync(param as VehicleListItem ?? SelectedVehicle),
            param => !IsLoading && (param is VehicleListItem || SelectedVehicle != null));

        ManageCategoriesCommand = new RelayCommand(
            async _ => await ManageCategoriesAsync(),
            _ => !IsLoading);
    }

    private bool FilterVehicle(object obj)
    {
        if (obj is not VehicleListItem vehicle)
            return false;

        var keyword = SearchText.Trim();
        var matchesSearch =
            string.IsNullOrEmpty(keyword) ||
            vehicle.LicensePlate.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            vehicle.Brand.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            vehicle.Model.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            vehicle.CategoryName.Contains(keyword, StringComparison.OrdinalIgnoreCase);

        var matchesStatus =
            string.IsNullOrEmpty(SelectedStatus) ||
            SelectedStatus == AllStatusesLabel ||
            vehicle.OperationalStatus == SelectedStatus;

        var matchesCategory =
            string.IsNullOrEmpty(SelectedCategoryFilter) ||
            SelectedCategoryFilter == AllCategoriesLabel ||
            vehicle.CategoryName == SelectedCategoryFilter;

        return matchesSearch && matchesStatus && matchesCategory;
    }

    public async Task LoadVehiclesAsync()
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "Đang tải danh sách xe…";

            var selectedId = SelectedVehicle?.VehicleId;
            var data = await _service.GetVehiclesAsync();

            Vehicles.Clear();
            foreach (var vehicle in data)
            {
                Vehicles.Add(vehicle);
            }

            var prevStatus = SelectedStatus;
            StatusOptions.Clear();
            StatusOptions.Add(AllStatusesLabel);
            foreach (var status in new[] { "Active", "Suspended" }
                         .Concat(data.Select(v => v.OperationalStatus))
                         .Where(s => !string.IsNullOrWhiteSpace(s))
                         .Distinct(StringComparer.Ordinal))
            {
                StatusOptions.Add(status);
            }
            SelectedStatus = StatusOptions.Contains(prevStatus) ? prevStatus : AllStatusesLabel;

            var prevCategory = SelectedCategoryFilter;
            CategoryOptions.Clear();
            CategoryOptions.Add(AllCategoriesLabel);
            foreach (var cat in data.Select(v => v.CategoryName)
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
        if (OpenVehicleEditorAsync is null)
            return;

        if (await OpenVehicleEditorAsync(null))
        {
            await LoadVehiclesAsync();
            StatusMessage = "Đã thêm phương tiện mới thành công.";
        }
    }

    private async Task EditVehicleAsync(VehicleListItem? item)
    {
        if (item is null || OpenVehicleEditorAsync is null)
            return;

        if (await OpenVehicleEditorAsync(item.VehicleId))
        {
            await LoadVehiclesAsync();
            StatusMessage = $"Đã cập nhật thông tin xe {item.LicensePlate}.";
        }
    }

    private async Task ToggleVehicleStatusAsync(VehicleListItem? item)
    {
        if (item is null)
            return;

        var targetStatus = item.OperationalStatus == "Active" ? "Suspended" : "Active";
        var actionText = targetStatus == "Suspended" ? "tạm ngưng hoạt động" : "kích hoạt lại";

        if (ConfirmAction != null)
        {
            var confirmed = ConfirmAction(
                $"Bạn có chắc muốn {actionText} xe {item.Brand} {item.Model} ({item.LicensePlate})?",
                "Xác nhận thay đổi trạng thái xe");
            if (!confirmed)
                return;
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
            ShowError?.Invoke(ex.Message, "Không thể đổi trạng thái xe");
        }
        catch (Exception ex)
        {
            StatusMessage = "Đã xảy ra lỗi khi cập nhật trạng thái xe.";
            ShowError?.Invoke("Không thể cập nhật trạng thái xe. Vui lòng thử lại.", "Lỗi hệ thống");
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
        if (OpenCategoryManagerAsync is null)
            return;

        if (await OpenCategoryManagerAsync())
        {
            await LoadVehiclesAsync();
        }
    }
}
