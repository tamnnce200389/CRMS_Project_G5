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
using CRMS.Services.Vehicles;

namespace CRMS.ViewModels
{
    public class MaintenanceViewModel : BaseViewModel
    {
        public const string AllStatusLabel = "Tất cả trạng thái";

        private readonly IMaintenanceService _maintenanceService;
        private readonly IVehicleService _vehicleService;
        private readonly long? _initialVehicleId;

        private string _searchText = string.Empty;
        private AvailabilityVehicleOption? _selectedVehicleFilter;
        private string _selectedStatusFilter = AllStatusLabel;
        private MaintenanceListItem? _selectedRecord;

        private bool _isCreateMode = true;
        private AvailabilityVehicleOption? _selectedFormVehicle;
        private bool _startImmediately;
        private DateTime? _startDate;
        private DateTime? _endDate;
        private string _selectedStartTime = "09:00";
        private string _selectedEndTime = "15:00";
        private string _costInput = "0";
        private string _descriptionInput = string.Empty;

        private bool _isLoading;
        private string _errorMessage = string.Empty;
        private string _statusMessage = "Quản lý lịch bảo dưỡng định kỳ và sửa chữa phương tiện.";

        public Func<string, string, bool>? ConfirmAction { get; set; }

        public ObservableCollection<MaintenanceListItem> Records { get; } = new ObservableCollection<MaintenanceListItem>();
        public ICollectionView RecordsView { get; }

        public ObservableCollection<AvailabilityVehicleOption> VehicleFilterOptions { get; } = new ObservableCollection<AvailabilityVehicleOption>();
        public ObservableCollection<AvailabilityVehicleOption> FormVehicleOptions { get; } = new ObservableCollection<AvailabilityVehicleOption>();
        public ObservableCollection<string> TimeOptions { get; } = BuildHalfHourTimeOptions();

        public List<string> StatusFilterOptions { get; } = new List<string>
        {
            AllStatusLabel,
            "InProgress",
            "Scheduled",
            "Completed",
            "Cancelled"
        };

        public string SearchText
        {
            get { return _searchText; }
            set
            {
                if (_searchText == value)
                {
                    return;
                }
                _searchText = value;
                OnPropertyChanged();
                RefreshFilteredView();
            }
        }

        public AvailabilityVehicleOption? SelectedVehicleFilter
        {
            get { return _selectedVehicleFilter; }
            set
            {
                if (_selectedVehicleFilter == value)
                {
                    return;
                }
                _selectedVehicleFilter = value;
                OnPropertyChanged();
                RefreshFilteredView();
            }
        }

        public string SelectedStatusFilter
        {
            get { return _selectedStatusFilter; }
            set
            {
                if (_selectedStatusFilter == value)
                {
                    return;
                }
                _selectedStatusFilter = value;
                OnPropertyChanged();
                RefreshFilteredView();
            }
        }

        public MaintenanceListItem? SelectedRecord
        {
            get { return _selectedRecord; }
            set
            {
                if (_selectedRecord == value)
                {
                    return;
                }
                _selectedRecord = value;
                OnPropertyChanged();
                if (value != null)
                {
                    LoadRecordIntoForm(value);
                }
                RaiseFormStateChanged();
            }
        }

        public bool IsCreateMode
        {
            get { return _isCreateMode; }
            private set
            {
                if (_isCreateMode == value)
                {
                    return;
                }
                _isCreateMode = value;
                OnPropertyChanged();
                RaiseFormStateChanged();
            }
        }

        public AvailabilityVehicleOption? SelectedFormVehicle
        {
            get { return _selectedFormVehicle; }
            set
            {
                if (_selectedFormVehicle == value)
                {
                    return;
                }
                _selectedFormVehicle = value;
                OnPropertyChanged();
            }
        }

        public bool StartImmediately
        {
            get { return _startImmediately; }
            set
            {
                if (_startImmediately == value)
                {
                    return;
                }
                _startImmediately = value;
                OnPropertyChanged();
                if (value)
                {
                    DateTime nowLocal = AvailabilityRules.ToVietnamLocal(DateTime.UtcNow);
                    string nowTime = $"{nowLocal:HH:mm}";
                    EnsureTimeOption(nowTime);
                    StartDate = nowLocal.Date;
                    SelectedStartTime = nowTime;

                    DateTime endLocal = nowLocal.Date.AddHours(Math.Min(23, nowLocal.Hour + 4));
                    if (endLocal <= nowLocal)
                    {
                        endLocal = nowLocal.Date.AddDays(1).AddHours(12);
                    }
                    string endTime = $"{endLocal:HH}:00";
                    EnsureTimeOption(endTime);
                    EndDate = endLocal.Date;
                    SelectedEndTime = endTime;
                }
            }
        }

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

        public string CostInput
        {
            get { return _costInput; }
            set
            {
                if (_costInput == value)
                {
                    return;
                }
                _costInput = value;
                OnPropertyChanged();
            }
        }

        public string DescriptionInput
        {
            get { return _descriptionInput; }
            set
            {
                if (_descriptionInput == value)
                {
                    return;
                }
                _descriptionInput = value;
                OnPropertyChanged();
            }
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

        public int TotalRecords
        {
            get { return Records.Count; }
        }

        public int InProgressCount
        {
            get { return Records.Count(x => x.IsInProgress); }
        }

        public int ScheduledCount
        {
            get { return Records.Count(x => x.IsScheduled); }
        }

        public int CompletedCount
        {
            get { return Records.Count(x => x.IsCompleted); }
        }

        public decimal TotalCompletedCost
        {
            get { return Records.Where(x => x.IsCompleted).Sum(x => x.Cost); }
        }

        public int FilteredCount
        {
            get { return RecordsView.Cast<object>().Count(); }
        }

        public bool IsFormEditable
        {
            get { return IsCreateMode || (SelectedRecord != null && SelectedRecord.IsEditable); }
        }

        public bool IsFormLocked
        {
            get { return !IsFormEditable; }
        }

        public bool ShowLifecycleActions
        {
            get { return !IsCreateMode && SelectedRecord != null && SelectedRecord.IsEditable; }
        }

        public bool CanStartSelected
        {
            get { return !IsCreateMode && SelectedRecord != null && SelectedRecord.CanStart; }
        }

        public string SelectedStatusText
        {
            get { return SelectedRecord != null ? SelectedRecord.MaintenanceStatus : string.Empty; }
        }

        public bool HasSelectedStatusBadge
        {
            get { return !IsCreateMode && SelectedRecord != null; }
        }

        public string FormTitle
        {
            get
            {
                return IsCreateMode
                    ? "Tạo lịch bảo trì mới"
                    : $"Chi tiết phiếu {SelectedRecord?.ReferenceCode}";
            }
        }

        public string PrimaryButtonText
        {
            get
            {
                return IsCreateMode
                    ? "Lưu lịch bảo trì"
                    : "Cập nhật phiếu bảo trì";
            }
        }

        public ICommand NewScheduleCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand StartMaintenanceCommand { get; }
        public ICommand CompleteMaintenanceCommand { get; }
        public ICommand CancelMaintenanceCommand { get; }
        public ICommand RefreshCommand { get; }

        public MaintenanceViewModel(long? initialVehicleId = null)
            : this(new MaintenanceService(), new VehicleService(), initialVehicleId)
        {
        }

        public MaintenanceViewModel(
            IMaintenanceService maintenanceService,
            IVehicleService vehicleService,
            long? initialVehicleId = null)
        {
            if (maintenanceService == null)
            {
                throw new ArgumentNullException(nameof(maintenanceService));
            }
            if (vehicleService == null)
            {
                throw new ArgumentNullException(nameof(vehicleService));
            }

            _maintenanceService = maintenanceService;
            _vehicleService = vehicleService;
            _initialVehicleId = initialVehicleId;

            RecordsView = CollectionViewSource.GetDefaultView(Records);
            RecordsView.Filter = FilterPredicate;

            ResetCreateFormDefaults();

            NewScheduleCommand = new RelayCommand(_ => SwitchToCreateMode(), _ => !IsLoading);
            SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync(), _ => !IsLoading && IsFormEditable);
            StartMaintenanceCommand = new AsyncRelayCommand(
                async _ => await TransitionSelectedAsync("InProgress"),
                _ => !IsLoading && CanStartSelected);
            CompleteMaintenanceCommand = new AsyncRelayCommand(
                async _ => await TransitionSelectedAsync("Completed"),
                _ => !IsLoading && ShowLifecycleActions);
            CancelMaintenanceCommand = new AsyncRelayCommand(
                async _ => await CancelSelectedAsync(),
                _ => !IsLoading && ShowLifecycleActions);
            RefreshCommand = new AsyncRelayCommand(async _ => await LoadRecordsAsync(), _ => !IsLoading);
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

                List<VehicleListItem> vehicles = await _vehicleService.GetVehiclesAsync();
                VehicleFilterOptions.Clear();
                FormVehicleOptions.Clear();

                VehicleFilterOptions.Add(new AvailabilityVehicleOption(null, "Tất cả phương tiện"));
                foreach (VehicleListItem v in vehicles)
                {
                    var opt = new AvailabilityVehicleOption(v.VehicleId, $"{v.LicensePlate} · {v.Brand} {v.Model}");
                    VehicleFilterOptions.Add(opt);
                    FormVehicleOptions.Add(opt);
                }

                if (_initialVehicleId.HasValue)
                {
                    SelectedVehicleFilter = VehicleFilterOptions.FirstOrDefault(x => x.VehicleId == _initialVehicleId.Value)
                        ?? VehicleFilterOptions[0];
                    SelectedFormVehicle = FormVehicleOptions.FirstOrDefault(x => x.VehicleId == _initialVehicleId.Value)
                        ?? FormVehicleOptions.FirstOrDefault();
                }
                else
                {
                    SelectedVehicleFilter = VehicleFilterOptions[0];
                    SelectedFormVehicle = FormVehicleOptions.FirstOrDefault();
                }

                await LoadRecordsCoreAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Không thể tải dữ liệu bảo trì: {ex.Message}";
                StatusMessage = "Không thể tải danh sách bảo trì.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadRecordsAsync(long? selectMaintenanceId = null)
        {
            if (IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                await LoadRecordsCoreAsync(selectMaintenanceId);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                StatusMessage = "Không thể tải danh sách bảo trì.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadRecordsCoreAsync(long? selectMaintenanceId = null)
        {
            List<MaintenanceListItem> list = await _maintenanceService.GetMaintenanceRecordsAsync();
            long? keepId = selectMaintenanceId.HasValue
                ? selectMaintenanceId
                : (SelectedRecord != null ? SelectedRecord.MaintenanceId : (long?)null);

            Records.Clear();
            foreach (MaintenanceListItem item in list)
            {
                Records.Add(item);
            }

            RefreshFilteredView();

            if (keepId.HasValue)
            {
                MaintenanceListItem? match = Records.FirstOrDefault(x => x.MaintenanceId == keepId.Value);
                if (match != null)
                {
                    SelectedRecord = match;
                }
            }

            StatusMessage = $"Cập nhật lúc {DateTime.Now:HH:mm} · {TotalRecords} phiếu ({InProgressCount} đang bảo trì, {ScheduledCount} đã lên lịch, {CompletedCount} hoàn thành).";
        }

        public void SwitchToCreateMode()
        {
            _selectedRecord = null;
            OnPropertyChanged(nameof(SelectedRecord));
            IsCreateMode = true;
            ResetCreateFormDefaults();

            if (SelectedVehicleFilter != null && SelectedVehicleFilter.VehicleId.HasValue)
            {
                long filterVehicleId = SelectedVehicleFilter.VehicleId.Value;
                SelectedFormVehicle = FormVehicleOptions.FirstOrDefault(x => x.VehicleId == filterVehicleId)
                    ?? FormVehicleOptions.FirstOrDefault();
            }
            else if (SelectedFormVehicle == null)
            {
                SelectedFormVehicle = FormVehicleOptions.FirstOrDefault();
            }
        }

        private async Task SaveAsync()
        {
            if (IsLoading)
            {
                return;
            }

            DateTime startLocal;
            DateTime endLocal;
            decimal cost;
            string validationError;
            if (!TryBuildFormValues(out startLocal, out endLocal, out cost, out validationError))
            {
                ErrorMessage = validationError;
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                if (IsCreateMode)
                {
                    long vehicleId = (SelectedFormVehicle != null && SelectedFormVehicle.VehicleId.HasValue)
                        ? SelectedFormVehicle.VehicleId.Value
                        : 0;

                    long newId = await _maintenanceService.ScheduleMaintenanceAsync(new MaintenanceScheduleRequest(
                        vehicleId,
                        startLocal,
                        endLocal,
                        DescriptionInput,
                        cost,
                        StartImmediately));

                    await LoadRecordsCoreAsync(newId);
                    StatusMessage = $"Đã lưu phiếu bảo trì BT-#{newId} thành công.";
                }
                else if (SelectedRecord != null)
                {
                    long targetId = SelectedRecord.MaintenanceId;
                    await _maintenanceService.UpdateMaintenanceAsync(new MaintenanceUpdateRequest(
                        targetId,
                        startLocal,
                        endLocal,
                        DescriptionInput,
                        cost,
                        SelectedRecord.MaintenanceStatus));

                    await LoadRecordsCoreAsync(targetId);
                    StatusMessage = $"Đã cập nhật phiếu bảo trì BT-#{targetId}.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CancelSelectedAsync()
        {
            if (SelectedRecord == null)
            {
                return;
            }

            if (ConfirmAction != null)
            {
                bool confirmed = ConfirmAction(
                    $"Bạn có chắc chắn muốn hủy phiếu bảo trì {SelectedRecord.ReferenceCode} của xe {SelectedRecord.LicensePlate}?\nSau khi hủy, xe sẽ được mở lại lịch trống.",
                    "Xác nhận hủy lịch bảo trì");
                if (!confirmed)
                {
                    return;
                }
            }

            await TransitionSelectedAsync("Cancelled");
        }

        private async Task TransitionSelectedAsync(string nextStatus)
        {
            if (SelectedRecord == null || IsLoading)
            {
                return;
            }

            decimal cost;
            if (!TryParseCost(CostInput, out cost))
            {
                ErrorMessage = "Chi phí bảo trì không hợp lệ (nhập số không âm, tối đa 2 chữ số lẻ).";
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                long targetId = SelectedRecord.MaintenanceId;

                await _maintenanceService.ChangeStatusAsync(
                    targetId,
                    nextStatus,
                    cost,
                    DescriptionInput);

                await LoadRecordsCoreAsync(targetId);
                StatusMessage = $"Đã chuyển trạng thái phiếu BT-#{targetId} thành công.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadRecordIntoForm(MaintenanceListItem item)
        {
            IsCreateMode = false;
            ErrorMessage = string.Empty;

            SelectedFormVehicle = FormVehicleOptions.FirstOrDefault(x => x.VehicleId == item.VehicleId)
                ?? FormVehicleOptions.FirstOrDefault();

            string startTime = $"{item.StartAtLocal:HH:mm}";
            string endTime = $"{item.EndAtLocal:HH:mm}";
            EnsureTimeOption(startTime);
            EnsureTimeOption(endTime);

            _startImmediately = false;
            OnPropertyChanged(nameof(StartImmediately));
            StartDate = item.StartAtLocal.Date;
            SelectedStartTime = startTime;
            EndDate = item.EndAtLocal.Date;
            SelectedEndTime = endTime;
            CostInput = item.Cost.ToString("0.##", CultureInfo.InvariantCulture);
            DescriptionInput = item.Description;
        }

        private void ResetCreateFormDefaults()
        {
            DateTime tomorrow = AvailabilityRules.ToVietnamLocal(DateTime.UtcNow).Date.AddDays(1);
            _startImmediately = false;
            OnPropertyChanged(nameof(StartImmediately));
            StartDate = tomorrow;
            EndDate = tomorrow;
            SelectedStartTime = "09:00";
            SelectedEndTime = "15:00";
            CostInput = "0";
            DescriptionInput = string.Empty;
            ErrorMessage = string.Empty;
        }

        private void RefreshFilteredView()
        {
            RecordsView.Refresh();
            OnPropertyChanged(nameof(TotalRecords));
            OnPropertyChanged(nameof(InProgressCount));
            OnPropertyChanged(nameof(ScheduledCount));
            OnPropertyChanged(nameof(CompletedCount));
            OnPropertyChanged(nameof(TotalCompletedCost));
            OnPropertyChanged(nameof(FilteredCount));
        }

        private void RaiseFormStateChanged()
        {
            OnPropertyChanged(nameof(IsFormEditable));
            OnPropertyChanged(nameof(IsFormLocked));
            OnPropertyChanged(nameof(ShowLifecycleActions));
            OnPropertyChanged(nameof(CanStartSelected));
            OnPropertyChanged(nameof(SelectedStatusText));
            OnPropertyChanged(nameof(HasSelectedStatusBadge));
            OnPropertyChanged(nameof(FormTitle));
            OnPropertyChanged(nameof(PrimaryButtonText));
            CommandManager.InvalidateRequerySuggested();
        }

        private bool FilterPredicate(object obj)
        {
            MaintenanceListItem? item = obj as MaintenanceListItem;
            if (item == null)
            {
                return false;
            }

            if (SelectedVehicleFilter != null &&
                SelectedVehicleFilter.VehicleId.HasValue &&
                item.VehicleId != SelectedVehicleFilter.VehicleId.Value)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(SelectedStatusFilter) &&
                !string.Equals(SelectedStatusFilter, AllStatusLabel, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.MaintenanceStatus, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string kw = SearchText.Trim();
                bool match =
                    item.LicensePlate.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    item.Brand.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    item.Model.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    item.Description.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    item.ReferenceCode.Contains(kw, StringComparison.OrdinalIgnoreCase);
                if (!match)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryBuildFormValues(
            out DateTime startLocal,
            out DateTime endLocal,
            out decimal cost,
            out string error)
        {
            startLocal = default(DateTime);
            endLocal = default(DateTime);
            cost = 0m;
            error = string.Empty;

            long formVehicleId = (SelectedFormVehicle != null && SelectedFormVehicle.VehicleId.HasValue)
                ? SelectedFormVehicle.VehicleId.Value
                : 0;

            if (IsCreateMode && formVehicleId <= 0)
            {
                error = "Vui lòng chọn phương tiện cần lập lịch bảo trì.";
                return false;
            }

            if (!StartDate.HasValue || !EndDate.HasValue)
            {
                error = "Vui lòng chọn đầy đủ ngày bắt đầu và ngày kết thúc bảo trì.";
                return false;
            }

            TimeSpan st;
            TimeSpan et;
            if (!TimeSpan.TryParseExact(SelectedStartTime, @"hh\:mm", CultureInfo.InvariantCulture, out st) ||
                !TimeSpan.TryParseExact(SelectedEndTime, @"hh\:mm", CultureInfo.InvariantCulture, out et))
            {
                error = "Giờ bắt đầu / kết thúc không đúng định dạng HH:mm.";
                return false;
            }

            if (!TryParseCost(CostInput, out cost))
            {
                error = "Chi phí bảo trì không hợp lệ (nhập số không âm, tối đa 2 chữ số lẻ).";
                return false;
            }

            startLocal = DateTime.SpecifyKind(StartDate.Value.Date.Add(st), DateTimeKind.Unspecified);
            endLocal = DateTime.SpecifyKind(EndDate.Value.Date.Add(et), DateTimeKind.Unspecified);
            return true;
        }

        private static bool TryParseCost(string? raw, out decimal cost)
        {
            string trimmed = (raw ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                cost = 0m;
                return true;
            }

            return VehicleRules.TryParseVndAmount(trimmed, out cost)
                   && cost >= 0m
                   && decimal.Round(cost, 2) == cost;
        }

        private void EnsureTimeOption(string hhmm)
        {
            if (TimeOptions.Contains(hhmm))
            {
                return;
            }

            int idx = 0;
            while (idx < TimeOptions.Count && string.CompareOrdinal(TimeOptions[idx], hhmm) < 0)
            {
                idx++;
            }
            TimeOptions.Insert(idx, hhmm);
        }

        private static ObservableCollection<string> BuildHalfHourTimeOptions()
        {
            var list = new ObservableCollection<string>();
            for (int h = 0; h < 24; h++)
            {
                list.Add($"{h:00}:00");
                list.Add($"{h:00}:30");
            }
            return list;
        }
    }
}
