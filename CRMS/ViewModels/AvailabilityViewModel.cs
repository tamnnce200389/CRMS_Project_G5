using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services;
using CRMS.Services.Vehicles;

namespace CRMS.ViewModels;

public sealed record AvailabilityCategoryOption(int? CategoryId, string Label)
{
    public override string ToString() => Label;
}

public sealed record AvailabilityVehicleOption(long? VehicleId, string Label)
{
    public override string ToString() => Label;
}

public sealed class AvailabilityViewModel : BaseViewModel
{
    public const string FilterAll = "Tất cả kết quả";
    public const string FilterAvailableOnly = "Chỉ xe còn trống";
    public const string FilterUnavailableOnly = "Chỉ xe không khả dụng";

    private readonly IAvailabilityService _availabilityService;
    private readonly IVehicleService _vehicleService;
    private readonly long? _initialVehicleId;
    private bool _suppressAutoFilter;

    private DateTime? _startDate;
    private DateTime? _endDate;
    private string _selectedStartTime;
    private string _selectedEndTime;
    private AvailabilityCategoryOption? _selectedCategoryOption;
    private AvailabilityVehicleOption? _selectedVehicleOption;
    private string _selectedAvailabilityFilter = FilterAll;
    private VehicleAvailabilityResult? _selectedResult;
    private bool _isLoading;
    private string _errorMessage = string.Empty;
    private string _statusMessage = "Chọn khung ngày giờ để kiểm tra tình trạng trống lịch của đội xe.";
    private string _checkedPeriodLabel = "—";

    public ObservableCollection<VehicleAvailabilityResult> Results { get; } = new();
    public ICollectionView ResultsView { get; }

    public ObservableCollection<AvailabilityCategoryOption> CategoryOptions { get; } = new();
    public ObservableCollection<AvailabilityVehicleOption> VehicleOptions { get; } = new();

    public IReadOnlyList<string> TimeOptions { get; } = BuildHalfHourTimeOptions();
    public IReadOnlyList<string> AvailabilityFilterOptions { get; } = new[]
    {
        FilterAll,
        FilterAvailableOnly,
        FilterUnavailableOnly
    };

    public DateTime? StartDate
    {
        get => _startDate;
        set
        {
            if (_startDate == value) return;
            _startDate = value;
            OnPropertyChanged();
        }
    }

    public DateTime? EndDate
    {
        get => _endDate;
        set
        {
            if (_endDate == value) return;
            _endDate = value;
            OnPropertyChanged();
        }
    }

    public string SelectedStartTime
    {
        get => _selectedStartTime;
        set
        {
            if (_selectedStartTime == value) return;
            _selectedStartTime = value;
            OnPropertyChanged();
        }
    }

    public string SelectedEndTime
    {
        get => _selectedEndTime;
        set
        {
            if (_selectedEndTime == value) return;
            _selectedEndTime = value;
            OnPropertyChanged();
        }
    }

    public AvailabilityCategoryOption? SelectedCategoryOption
    {
        get => _selectedCategoryOption;
        set
        {
            if (_selectedCategoryOption == value) return;
            _selectedCategoryOption = value;
            OnPropertyChanged();
        }
    }

    public AvailabilityVehicleOption? SelectedVehicleOption
    {
        get => _selectedVehicleOption;
        set
        {
            if (_selectedVehicleOption == value) return;
            _selectedVehicleOption = value;
            OnPropertyChanged();
        }
    }

    public string SelectedAvailabilityFilter
    {
        get => _selectedAvailabilityFilter;
        set
        {
            if (_selectedAvailabilityFilter == value) return;
            _selectedAvailabilityFilter = value;
            OnPropertyChanged();
            if (!_suppressAutoFilter)
                RefreshFilteredView();
        }
    }

    public VehicleAvailabilityResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (_selectedResult == value) return;
            _selectedResult = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedResult));
        }
    }

    public bool HasSelectedResult => SelectedResult != null;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string CheckedPeriodLabel
    {
        get => _checkedPeriodLabel;
        private set
        {
            if (_checkedPeriodLabel == value) return;
            _checkedPeriodLabel = value;
            OnPropertyChanged();
        }
    }

    public int TotalCheckedCount => Results.Count;
    public int AvailableCount => Results.Count(x => x.IsAvailable);
    public int UnavailableCount => Results.Count(x => !x.IsAvailable);
    public int FilteredCount => ResultsView.Cast<object>().Count();

    public ICommand CheckAvailabilityCommand { get; }
    public ICommand ResetFilterCommand { get; }

    public AvailabilityViewModel(long? initialVehicleId = null)
        : this(new VehicleAvailabilityService(), new VehicleService(), initialVehicleId)
    {
    }

    public AvailabilityViewModel(
        IAvailabilityService availabilityService,
        IVehicleService vehicleService,
        long? initialVehicleId = null)
    {
        _availabilityService = availabilityService ?? throw new ArgumentNullException(nameof(availabilityService));
        _vehicleService = vehicleService ?? throw new ArgumentNullException(nameof(vehicleService));
        _initialVehicleId = initialVehicleId;

        var defaultStart = AvailabilityRules.ToVietnamLocal(DateTime.UtcNow).Date.AddDays(1).AddHours(9);
        var defaultEnd = defaultStart.AddDays(1);
        _startDate = defaultStart.Date;
        _endDate = defaultEnd.Date;
        _selectedStartTime = "09:00";
        _selectedEndTime = "09:00";

        ResultsView = CollectionViewSource.GetDefaultView(Results);
        ResultsView.Filter = FilterResultPredicate;

        CheckAvailabilityCommand = new AsyncRelayCommand(async _ => await CheckAvailabilityAsync(), _ => !IsLoading);
        ResetFilterCommand = new AsyncRelayCommand(async _ => await ResetFiltersAsync(), _ => !IsLoading);
    }

    public async Task InitializeAsync()
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var categories = await _vehicleService.GetCategoriesAsync(includeInactive: true);
            CategoryOptions.Clear();
            CategoryOptions.Add(new AvailabilityCategoryOption(null, "Tất cả loại xe"));
            foreach (var c in categories)
            {
                var label = c.IsActive ? c.CategoryName : $"{c.CategoryName} (Ngừng)";
                CategoryOptions.Add(new AvailabilityCategoryOption(c.CategoryId, label));
            }
            SelectedCategoryOption = CategoryOptions[0];

            var vehicles = await _vehicleService.GetVehiclesAsync();
            VehicleOptions.Clear();
            VehicleOptions.Add(new AvailabilityVehicleOption(null, "Tất cả phương tiện"));
            foreach (var v in vehicles)
            {
                VehicleOptions.Add(new AvailabilityVehicleOption(
                    v.VehicleId,
                    $"{v.LicensePlate} · {v.Brand} {v.Model}"));
            }

            SelectedVehicleOption = _initialVehicleId.HasValue
                ? VehicleOptions.FirstOrDefault(x => x.VehicleId == _initialVehicleId.Value) ?? VehicleOptions[0]
                : VehicleOptions[0];

            if (TryComposePeriod(out var startLocal, out var endLocal, out var validationError))
            {
                await CheckAvailabilityCoreAsync(startLocal, endLocal);
            }
            else
            {
                ErrorMessage = validationError;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải danh mục bộ lọc: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task CheckAvailabilityAsync()
    {
        if (IsLoading)
            return;

        if (!TryComposePeriod(out var startLocal, out var endLocal, out var validationError))
        {
            ErrorMessage = validationError;
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            await CheckAvailabilityCoreAsync(startLocal, endLocal);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Kiểm tra lịch trống không thành công.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CheckAvailabilityCoreAsync(DateTime startLocal, DateTime endLocal)
    {
        var categoryId = SelectedCategoryOption?.CategoryId;
        var vehicleId = SelectedVehicleOption?.VehicleId;

        var list = await _availabilityService.CheckFleetAvailabilityAsync(
            startLocal,
            endLocal,
            categoryId: categoryId,
            vehicleId: vehicleId,
            includeSuspendedVehicles: true,
            requireFutureStart: false);

        var previousId = SelectedResult?.VehicleId;
        Results.Clear();
        foreach (var item in list)
            Results.Add(item);

        CheckedPeriodLabel = $"{startLocal:dd/MM/yyyy HH:mm} → {endLocal:dd/MM/yyyy HH:mm}";
        RefreshFilteredView();

        SelectedResult = ResultsView.Cast<VehicleAvailabilityResult>()
                             .FirstOrDefault(x => x.VehicleId == previousId)
                         ?? ResultsView.Cast<VehicleAvailabilityResult>().FirstOrDefault();

        StatusMessage = $"Đã kiểm tra {TotalCheckedCount} xe trong khoảng [{startLocal:dd/MM/yyyy HH:mm}, {endLocal:dd/MM/yyyy HH:mm}): {AvailableCount} xe trống, {UnavailableCount} xe không khả dụng.";
    }

    private async Task ResetFiltersAsync()
    {
        var defaultStart = AvailabilityRules.ToVietnamLocal(DateTime.UtcNow).Date.AddDays(1);
        StartDate = defaultStart;
        EndDate = defaultStart.AddDays(1);
        SelectedStartTime = "09:00";
        SelectedEndTime = "09:00";

        if (CategoryOptions.Count > 0)
            SelectedCategoryOption = CategoryOptions[0];
        if (VehicleOptions.Count > 0)
            SelectedVehicleOption = VehicleOptions[0];

        _suppressAutoFilter = true;
        SelectedAvailabilityFilter = FilterAll;
        _suppressAutoFilter = false;

        await CheckAvailabilityAsync();
    }

    private void RefreshFilteredView()
    {
        ResultsView.Refresh();
        OnPropertyChanged(nameof(TotalCheckedCount));
        OnPropertyChanged(nameof(AvailableCount));
        OnPropertyChanged(nameof(UnavailableCount));
        OnPropertyChanged(nameof(FilteredCount));

        if (SelectedResult is null || !ResultsView.Cast<VehicleAvailabilityResult>().Contains(SelectedResult))
        {
            SelectedResult = ResultsView.Cast<VehicleAvailabilityResult>().FirstOrDefault();
        }
    }

    private bool FilterResultPredicate(object obj)
    {
        if (obj is not VehicleAvailabilityResult item)
            return false;

        return SelectedAvailabilityFilter switch
        {
            FilterAvailableOnly => item.IsAvailable,
            FilterUnavailableOnly => !item.IsAvailable,
            _ => true
        };
    }

    private bool TryComposePeriod(out DateTime startLocal, out DateTime endLocal, out string error)
    {
        startLocal = default;
        endLocal = default;
        error = string.Empty;

        if (!StartDate.HasValue || !EndDate.HasValue)
        {
            error = "Vui lòng chọn đầy đủ ngày nhận xe và ngày trả xe.";
            return false;
        }

        if (!TimeSpan.TryParseExact(SelectedStartTime, @"hh\:mm", CultureInfo.InvariantCulture, out var startTime) ||
            !TimeSpan.TryParseExact(SelectedEndTime, @"hh\:mm", CultureInfo.InvariantCulture, out var endTime))
        {
            error = "Định dạng giờ không hợp lệ (HH:mm).";
            return false;
        }

        startLocal = DateTime.SpecifyKind(StartDate.Value.Date.Add(startTime), DateTimeKind.Unspecified);
        endLocal = DateTime.SpecifyKind(EndDate.Value.Date.Add(endTime), DateTimeKind.Unspecified);

        if (endLocal <= startLocal)
        {
            error = "Thời gian trả xe phải sau thời gian nhận xe.";
            return false;
        }

        return true;
    }

    private static IReadOnlyList<string> BuildHalfHourTimeOptions()
    {
        var list = new List<string>(48);
        for (var hour = 0; hour < 24; hour++)
        {
            list.Add($"{hour:00}:00");
            list.Add($"{hour:00}:30");
        }
        return list;
    }
}
