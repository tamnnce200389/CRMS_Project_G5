using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services;
using CRMS.Services.Vehicles;

namespace CRMS.ViewModels
{
    public class AvailabilityCategoryOption
    {
        public int? CategoryId { get; set; }
        public string Label { get; set; } = string.Empty;

        public AvailabilityCategoryOption()
        {
        }

        public AvailabilityCategoryOption(int? categoryId, string label)
        {
            CategoryId = categoryId;
            Label = label;
        }

        public override string ToString()
        {
            return Label;
        }
    }

    public class AvailabilityVehicleOption
    {
        public long? VehicleId { get; set; }
        public string Label { get; set; } = string.Empty;

        public AvailabilityVehicleOption()
        {
        }

        public AvailabilityVehicleOption(long? vehicleId, string label)
        {
            VehicleId = vehicleId;
            Label = label;
        }

        public override string ToString()
        {
            return Label;
        }
    }

    public class AvailabilityViewModel : BaseViewModel
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

        public ObservableCollection<VehicleAvailabilityResult> Results { get; } = new ObservableCollection<VehicleAvailabilityResult>();
        public ICollectionView ResultsView { get; }

        public ObservableCollection<AvailabilityCategoryOption> CategoryOptions { get; } = new ObservableCollection<AvailabilityCategoryOption>();
        public ObservableCollection<AvailabilityVehicleOption> VehicleOptions { get; } = new ObservableCollection<AvailabilityVehicleOption>();

        public List<string> TimeOptions { get; } = BuildHalfHourTimeOptions();
        public List<string> AvailabilityFilterOptions { get; } = new List<string>
        {
            FilterAll,
            FilterAvailableOnly,
            FilterUnavailableOnly
        };

        public DateTime? StartDate
        {
            get { return _startDate; }
            set
            {
                if (_startDate == value)
                {
                    return;
                }
                _startDate = value;
                OnPropertyChanged();
            }
        }

        public DateTime? EndDate
        {
            get { return _endDate; }
            set
            {
                if (_endDate == value)
                {
                    return;
                }
                _endDate = value;
                OnPropertyChanged();
            }
        }

        public string SelectedStartTime
        {
            get { return _selectedStartTime; }
            set
            {
                if (_selectedStartTime == value)
                {
                    return;
                }
                _selectedStartTime = value;
                OnPropertyChanged();
            }
        }

        public string SelectedEndTime
        {
            get { return _selectedEndTime; }
            set
            {
                if (_selectedEndTime == value)
                {
                    return;
                }
                _selectedEndTime = value;
                OnPropertyChanged();
            }
        }

        public AvailabilityCategoryOption? SelectedCategoryOption
        {
            get { return _selectedCategoryOption; }
            set
            {
                if (_selectedCategoryOption == value)
                {
                    return;
                }
                _selectedCategoryOption = value;
                OnPropertyChanged();
            }
        }

        public AvailabilityVehicleOption? SelectedVehicleOption
        {
            get { return _selectedVehicleOption; }
            set
            {
                if (_selectedVehicleOption == value)
                {
                    return;
                }
                _selectedVehicleOption = value;
                OnPropertyChanged();
            }
        }

        public string SelectedAvailabilityFilter
        {
            get { return _selectedAvailabilityFilter; }
            set
            {
                if (_selectedAvailabilityFilter == value)
                {
                    return;
                }
                _selectedAvailabilityFilter = value;
                OnPropertyChanged();
                if (!_suppressAutoFilter)
                {
                    RefreshFilteredView();
                }
            }
        }

        public VehicleAvailabilityResult? SelectedResult
        {
            get { return _selectedResult; }
            set
            {
                if (_selectedResult == value)
                {
                    return;
                }
                _selectedResult = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedResult));
            }
        }

        public bool HasSelectedResult
        {
            get { return SelectedResult != null; }
        }

        public bool IsLoading
        {
            get { return _isLoading; }
            private set
            {
                if (_isLoading == value)
                {
                    return;
                }
                _isLoading = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            private set
            {
                if (_errorMessage == value)
                {
                    return;
                }
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError
        {
            get { return !string.IsNullOrWhiteSpace(ErrorMessage); }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            private set
            {
                if (_statusMessage == value)
                {
                    return;
                }
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public string CheckedPeriodLabel
        {
            get { return _checkedPeriodLabel; }
            private set
            {
                if (_checkedPeriodLabel == value)
                {
                    return;
                }
                _checkedPeriodLabel = value;
                OnPropertyChanged();
            }
        }

        public int TotalCheckedCount
        {
            get { return Results.Count; }
        }

        public int AvailableCount
        {
            get { return Results.Count(x => x.IsAvailable); }
        }

        public int UnavailableCount
        {
            get { return Results.Count(x => !x.IsAvailable); }
        }

        public int FilteredCount
        {
            get { return ResultsView.Cast<object>().Count(); }
        }

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
            if (availabilityService == null)
            {
                throw new ArgumentNullException(nameof(availabilityService));
            }
            if (vehicleService == null)
            {
                throw new ArgumentNullException(nameof(vehicleService));
            }

            _availabilityService = availabilityService;
            _vehicleService = vehicleService;
            _initialVehicleId = initialVehicleId;

            DateTime defaultStart = AvailabilityRules.ToVietnamLocal(DateTime.UtcNow).Date.AddDays(1).AddHours(9);
            DateTime defaultEnd = defaultStart.AddDays(1);
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
            {
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                List<CategoryListItem> categories = await _vehicleService.GetCategoriesAsync(true);
                CategoryOptions.Clear();
                CategoryOptions.Add(new AvailabilityCategoryOption(null, "Tất cả loại xe"));
                foreach (CategoryListItem c in categories)
                {
                    string label = c.IsActive ? c.CategoryName : $"{c.CategoryName} (Ngừng)";
                    CategoryOptions.Add(new AvailabilityCategoryOption(c.CategoryId, label));
                }
                SelectedCategoryOption = CategoryOptions[0];

                List<VehicleListItem> vehicles = await _vehicleService.GetVehiclesAsync();
                VehicleOptions.Clear();
                VehicleOptions.Add(new AvailabilityVehicleOption(null, "Tất cả phương tiện"));
                foreach (VehicleListItem v in vehicles)
                {
                    VehicleOptions.Add(new AvailabilityVehicleOption(
                        v.VehicleId,
                        $"{v.LicensePlate} · {v.Brand} {v.Model}"));
                }

                if (_initialVehicleId.HasValue)
                {
                    AvailabilityVehicleOption? found = VehicleOptions.FirstOrDefault(x => x.VehicleId == _initialVehicleId.Value);
                    SelectedVehicleOption = found ?? VehicleOptions[0];
                }
                else
                {
                    SelectedVehicleOption = VehicleOptions[0];
                }

                DateTime startLocal;
                DateTime endLocal;
                string validationError;
                if (TryComposePeriod(out startLocal, out endLocal, out validationError))
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
            {
                return;
            }

            DateTime startLocal;
            DateTime endLocal;
            string validationError;
            if (!TryComposePeriod(out startLocal, out endLocal, out validationError))
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
            int? categoryId = SelectedCategoryOption != null ? SelectedCategoryOption.CategoryId : null;
            long? vehicleId = SelectedVehicleOption != null ? SelectedVehicleOption.VehicleId : null;

            List<VehicleAvailabilityResult> list = await _availabilityService.CheckFleetAvailabilityAsync(
                startLocal,
                endLocal,
                categoryId,
                vehicleId,
                true,
                false);

            long? previousId = SelectedResult != null ? SelectedResult.VehicleId : (long?)null;
            Results.Clear();
            foreach (VehicleAvailabilityResult item in list)
            {
                Results.Add(item);
            }

            CheckedPeriodLabel = $"{startLocal:dd/MM/yyyy HH:mm} → {endLocal:dd/MM/yyyy HH:mm}";
            RefreshFilteredView();

            VehicleAvailabilityResult? matched = null;
            if (previousId.HasValue)
            {
                matched = ResultsView.Cast<VehicleAvailabilityResult>()
                    .FirstOrDefault(x => x.VehicleId == previousId.Value);
            }
            SelectedResult = matched ?? ResultsView.Cast<VehicleAvailabilityResult>().FirstOrDefault();

            StatusMessage = $"Đã kiểm tra {TotalCheckedCount} xe trong khoảng [{startLocal:dd/MM/yyyy HH:mm}, {endLocal:dd/MM/yyyy HH:mm}): {AvailableCount} xe trống, {UnavailableCount} xe không khả dụng.";
        }

        private async Task ResetFiltersAsync()
        {
            DateTime defaultStart = AvailabilityRules.ToVietnamLocal(DateTime.UtcNow).Date.AddDays(1);
            StartDate = defaultStart;
            EndDate = defaultStart.AddDays(1);
            SelectedStartTime = "09:00";
            SelectedEndTime = "09:00";

            if (CategoryOptions.Count > 0)
            {
                SelectedCategoryOption = CategoryOptions[0];
            }
            if (VehicleOptions.Count > 0)
            {
                SelectedVehicleOption = VehicleOptions[0];
            }

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

            if (SelectedResult == null || !ResultsView.Cast<VehicleAvailabilityResult>().Contains(SelectedResult))
            {
                SelectedResult = ResultsView.Cast<VehicleAvailabilityResult>().FirstOrDefault();
            }
        }

        private bool FilterResultPredicate(object obj)
        {
            VehicleAvailabilityResult? item = obj as VehicleAvailabilityResult;
            if (item == null)
            {
                return false;
            }

            if (SelectedAvailabilityFilter == FilterAvailableOnly)
            {
                return item.IsAvailable;
            }

            if (SelectedAvailabilityFilter == FilterUnavailableOnly)
            {
                return !item.IsAvailable;
            }

            return true;
        }

        private bool TryComposePeriod(out DateTime startLocal, out DateTime endLocal, out string error)
        {
            startLocal = default(DateTime);
            endLocal = default(DateTime);
            error = string.Empty;

            if (!StartDate.HasValue || !EndDate.HasValue)
            {
                error = "Vui lòng chọn đầy đủ ngày nhận xe và ngày trả xe.";
                return false;
            }

            TimeSpan startTime;
            TimeSpan endTime;
            if (!TimeSpan.TryParseExact(SelectedStartTime, @"hh\:mm", CultureInfo.InvariantCulture, out startTime) ||
                !TimeSpan.TryParseExact(SelectedEndTime, @"hh\:mm", CultureInfo.InvariantCulture, out endTime))
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

        private static List<string> BuildHalfHourTimeOptions()
        {
            var list = new List<string>(48);
            for (int hour = 0; hour < 24; hour++)
            {
                list.Add($"{hour:00}:00");
                list.Add($"{hour:00}:30");
            }
            return list;
        }
    }
}
