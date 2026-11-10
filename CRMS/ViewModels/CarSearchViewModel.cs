using CRMS.Helpers;
using CRMS.Models;
using CRMS.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

namespace CRMS.ViewModels
{
    public class CarSearchViewModel : BaseViewModel
    {
        private readonly SearchService _searchService;
        private CancellationTokenSource? _searchCancellation;
        private int _requestVersion;
        private int _filterRevision;
        private DateTime? _beforeHourlyStart, _beforeHourlyEnd;
        public void AdjustHourlyPeriod()
        {
            if (!IsSelfDrive) return;
            if (HourlyOnly)
            {
                _beforeHourlyStart = StartAtLocal; _beforeHourlyEnd = EndAtLocal;
                var start = StartAtLocal ?? DateTime.Now.AddHours(1);
                if (start <= DateTime.Now) start = DateTime.Now.AddHours(1);
                SetRentalPeriod(start, start.AddHours(4));
            }
            else if (_beforeHourlyStart > DateTime.Now && _beforeHourlyEnd.HasValue)
                SetRentalPeriod(_beforeHourlyStart, _beforeHourlyEnd);
        }
        public async Task RemoveCriterionAsync(AppliedCriterion criterion)
        {
            if (IsLoading || criterion.Remove == null) return;
            var hourly = HourlyOnly; criterion.Remove();
            if (hourly && !HourlyOnly) AdjustHourlyPeriod();
            await SearchAsync();
        }
        public BookingSelection PrepareBooking(CarSearchItem car, IUserSession session)
        {
            if (!CanChooseResults || !Cars.Contains(car)) throw new InvalidOperationException("Vui lòng tìm lại và áp dụng bộ lọc trước khi chọn xe.");
            if (session.Role != "Customer" || session.UserId is not > 0) throw new InvalidOperationException("Chức năng đặt xe chỉ dành cho Customer.");
            if (!car.CanUseLegacyBooking) throw new InvalidOperationException("Báo giá trọn chuyến này chưa hỗ trợ đặt trực tiếp.");
            if (RentalMode is not ("SelfDrive" or "WithDriver")) throw new InvalidOperationException("Vui lòng chọn gói thuê dài hạn tại cửa sổ riêng.");
            if (!StartAtLocal.HasValue || !EndAtLocal.HasValue) throw new InvalidOperationException("Vui lòng chọn thời gian nhận và trả xe.");
            CRMS.Services.Vehicles.AvailabilityRules.ValidatePeriod(StartAtLocal.Value, EndAtLocal.Value, DateTime.UtcNow, true);
            return new(session.UserId.Value, car.VehicleId, RentalMode, StartAtLocal.Value, EndAtLocal.Value, car.QuoteUnit == "Hour" ? "Hour" : "Day", ExpectedTotal: car.EstimatedTripTotal);
        }

        public ObservableCollection<CarSearchItem> Cars { get; } = new();
        public ObservableCollection<VehicleCategory> Categories { get; } = new();
        public ObservableCollection<string> Brands { get; } = new() { "Tất cả hãng xe" };
        public List<string> SortOptions { get; } = new() { "Mặc định", "Giá tăng dần", "Giá giảm dần", "Tên A-Z" };
        public List<string> TransmissionOptions { get; } = new() { "Tất cả hộp số", "Số tự động", "Số sàn" };
        public List<string> FuelOptions { get; } = new() { "Tất cả nhiên liệu", "Xăng", "Dầu diesel", "Điện", "Hybrid" };
        public List<string> SeatOptions { get; } = new[] { "Tất cả số chỗ" }.Concat(Enumerable.Range(1, 30).Select(n => n.ToString())).ToList();
        public List<string> YearOptions { get; } = new[] { "Tất cả năm" }.Concat(Enumerable.Range(2000, DateTime.Now.Year - 1999).Reverse().Select(n => n.ToString())).ToList();

        private string _rentalMode = "SelfDrive";
        public string RentalMode
        {
            get => _rentalMode;
            set
            {
                if (value is not ("SelfDrive" or "WithDriver" or "LongTerm")) throw new ArgumentException("Chế độ thuê không hợp lệ.");
                if (_rentalMode == value) return;
                CancelPendingSearch();
                _rentalMode = value;
                ResetFilterValues();
                SetRentalPeriod(null, null);
                Cars.Clear();
                SetAppliedCriteria([]);
                OnPropertyChanged(); OnPropertyChanged(nameof(IsShortTerm)); OnPropertyChanged(nameof(IsLongTerm));
                OnPropertyChanged(nameof(ResultCount)); OnPropertyChanged(nameof(IsWithDriver)); OnPropertyChanged(nameof(IsSelfDrive)); OnPropertyChanged(nameof(ContextNotice));
            }
        }
        public bool IsShortTerm => RentalMode != "LongTerm";
        public bool IsLongTerm => RentalMode == "LongTerm";
        public DateTime? StartAtLocal { get; private set; }
        public DateTime? EndAtLocal { get; private set; }
        public void SetRentalPeriod(DateTime? startAt, DateTime? endAt)
        {
            ++_filterRevision;
            StartAtLocal = startAt; EndAtLocal = endAt;
            OnPropertyChanged(nameof(StartAtLocal)); OnPropertyChanged(nameof(EndAtLocal)); OnPropertyChanged(nameof(RentalPeriodSummary));
            IsFilterPending = true;
        }
        public ObservableCollection<SearchCity> Cities { get; } = new();
        public ObservableCollection<RentalLocation> DeliveryLocations { get; } = new();
        private RentalLocation? _selectedDeliveryLocation;
        public RentalLocation? SelectedDeliveryLocation
        {
            get => _selectedDeliveryLocation;
            set
            {
                _selectedDeliveryLocation = value;
                LatitudeText = value?.Latitude?.ToString(CultureInfo.InvariantCulture) ?? "";
                LongitudeText = value?.Longitude?.ToString(CultureInfo.InvariantCulture) ?? "";
                if (value != null) SelectedCity = Cities.FirstOrDefault(c => c.CityId == value.CityId);
                Changed(nameof(SelectedDeliveryLocation));
            }
        }
        public ObservableCollection<SearchPurposeChoice> Purposes { get; } = new();
        public List<int> DurationOptions { get; } = new() { 1, 3, 6, 9, 12 };
        public List<string> RouteOptions { get; } = new() { "Nội thành", "Liên tỉnh", "Liên tỉnh một chiều" };
        public bool IsWithDriver => RentalMode == "WithDriver";
        public bool IsSelfDrive => RentalMode == "SelfDrive";
        private CarSearchContext? _initialContext;
        public string LocationSummary => SelectedCity?.CityId > 0 ? SelectedCity.CityName : "Tất cả địa điểm";
        public string DeliveryFilterLabel => $"Giao nhận tận nơi: {LocationSummary}";
        public string DriverJourneySummary => SelectedCity?.CityId > 0
            ? SelectedRoute == "Nội thành" ? $"Nội thành · {SelectedCity.CityName}"
                : $"{SelectedRoute} · {SelectedCity.CityName} → {DestinationCity?.CityName ?? "Chọn điểm đến"}"
            : "Chọn hành trình có tài xế";
        public bool HasValidDriverJourney
        {
            get
            {
                try { CreateJourneyFilter().Validate(); return true; }
                catch (ArgumentException) { return false; }
            }
        }
        public string ContextNotice => IsWithDriver
            ? "Chỉ xe đã xác minh phục vụ đúng hành trình. Giá/chuyến gồm xe và tài xế theo biểu giá đã khai báo, chưa gồm phụ phí."
            : IsLongTerm ? "Giá/tháng từ bảng giá dài hạn thực tế; lịch trống kiểm tra theo kỳ hạn đã chọn."
            : "Chỉ hiển thị xe đã xác minh địa điểm và chế độ tự lái. Giá giờ và giá ngày được phân biệt rõ.";
        public string RentalPeriodSummary => StartAtLocal.HasValue ? $"{StartAtLocal:HH:mm dd/MM/yyyy} – {EndAtLocal:HH:mm dd/MM/yyyy}" : "Chọn thời gian thuê";
        private SearchCity? _selectedCity;
        public SearchCity? SelectedCity { get => _selectedCity; set { _selectedCity = value; Changed(nameof(SelectedCity)); OnPropertyChanged(nameof(LocationSummary)); OnPropertyChanged(nameof(DeliveryFilterLabel)); } }
        private SearchCity? _destinationCity;
        public SearchCity? DestinationCity { get => _destinationCity; set { _destinationCity = value; Changed(nameof(DestinationCity)); } }
        private string _selectedRoute = "Nội thành";
        public string SelectedRoute { get => _selectedRoute; set { _selectedRoute = value; Changed(nameof(SelectedRoute)); } }
        private int _selectedDuration = 3;
        public int SelectedDuration { get => _selectedDuration; set { _selectedDuration = value; if (IsLongTerm && StartAtLocal.HasValue) SetRentalPeriod(StartAtLocal, StartAtLocal.Value.AddMonths(value)); Changed(nameof(SelectedDuration)); } }
        public DateTime? LongTermStartDate { get => StartAtLocal?.Date; set { if (value.HasValue) SetRentalPeriod(value.Value.Date.AddHours(9), value.Value.Date.AddHours(9).AddMonths(SelectedDuration)); OnPropertyChanged(); Changed(nameof(RentalPeriodSummary)); } }
        private string _latitudeText = "";
        public string LatitudeText { get => _latitudeText; set { _latitudeText = value; Changed(nameof(LatitudeText)); } }
        private string _longitudeText = "";
        public string LongitudeText { get => _longitudeText; set { _longitudeText = value; Changed(nameof(LongitudeText)); } }
        public void SetSearchContext(CarSearchContext? context)
        {
            _initialContext = context;
            HourlyOnly = context?.HourlyOnly == true;
            ChildSeatOnly = context?.ChildSeatOnly == true; ExtendedTravelOnly = context?.ExtendedTravelOnly == true; SportOnly = context?.SportOnly == true;
            SelectedRoute = context?.RouteType switch { "InterCityRoundTrip" => "Liên tỉnh", "InterCityOneWay" => "Liên tỉnh một chiều", _ => "Nội thành" };
            if (context?.Fuel == "Electric") SelectedFuel = "Điện";
            if (context?.Fuel == "Hybrid") SelectedFuel = "Hybrid";
            if (context?.BudgetCode == "LowCost") { MinPrice = ""; MaxPrice = "500000"; }
            if (context?.BudgetCode == "Economy") { MinPrice = "500000"; MaxPrice = "1000000"; }
            if (IsLongTerm && !StartAtLocal.HasValue) LongTermStartDate = DateTime.Today.AddDays(1);
            OnPropertyChanged(nameof(LocationSummary)); OnPropertyChanged(nameof(ContextNotice));
        }

        private void Changed(string name)
        {
            ++_filterRevision; OnPropertyChanged(name); IsFilterPending = true; OnPropertyChanged(nameof(HasAnySelfDriveFilter)); OnPropertyChanged(nameof(HasChildSeatFilter)); OnPropertyChanged(nameof(DriverJourneySummary));
            OnPropertyChanged(nameof(HasCategoryFilter)); OnPropertyChanged(nameof(HasBrandFilter));
            OnPropertyChanged(nameof(HasSortFilter)); OnPropertyChanged(nameof(HasTransmissionFilter)); OnPropertyChanged(nameof(HasPriceFilter));
            OnPropertyChanged(nameof(IsElectricFilter)); OnPropertyChanged(nameof(IsHybridFilter));
        }
        private string _searchText = "";
        public string SearchText { get => _searchText; set { if (_searchText == value) return; _searchText = value; Changed(nameof(SearchText)); } }
        private VehicleCategory? _selectedCategory;
        public VehicleCategory? SelectedCategory { get => _selectedCategory; set { if (_selectedCategory == value) return; _selectedCategory = value; Changed(nameof(SelectedCategory)); } }
        private string _selectedBrand = "Tất cả hãng xe";
        public string SelectedBrand { get => _selectedBrand; set { if (_selectedBrand == value) return; _selectedBrand = value; Changed(nameof(SelectedBrand)); } }
        private string _minPrice = "";
        public string MinPrice { get => _minPrice; set { if (_minPrice == value) return; _minPrice = value; Changed(nameof(MinPrice)); } }
        private string _maxPrice = "";
        public string MaxPrice { get => _maxPrice; set { if (_maxPrice == value) return; _maxPrice = value; Changed(nameof(MaxPrice)); } }
        private string _selectedSort = "Mặc định";
        public string SelectedSort { get => _selectedSort; set { if (_selectedSort == value) return; _selectedSort = value; Changed(nameof(SelectedSort)); } }
        private string _selectedTransmission = "Tất cả hộp số";
        public string SelectedTransmission { get => _selectedTransmission; set { if (_selectedTransmission == value) return; _selectedTransmission = value; Changed(nameof(SelectedTransmission)); } }
        private string _selectedFuel = "Tất cả nhiên liệu";
        public string SelectedFuel { get => _selectedFuel; set { if (_selectedFuel == value) return; _selectedFuel = value; Changed(nameof(SelectedFuel)); } }
        private string _selectedSeats = "Tất cả số chỗ";
        public string SelectedSeats { get => _selectedSeats; set { if (_selectedSeats == value) return; _selectedSeats = value; Changed(nameof(SelectedSeats)); } }
        private string _selectedYear = "Tất cả năm";
        public string SelectedYear { get => _selectedYear; set { if (_selectedYear == value) return; _selectedYear = value; Changed(nameof(SelectedYear)); } }
        private bool _discountOnly;
        public bool DiscountOnly { get => _discountOnly; set { if (_discountOnly == value) return; _discountOnly = value; Changed(nameof(DiscountOnly)); } }
        private bool _hourlyOnly;
        public bool HourlyOnly { get => _hourlyOnly; set { if (_hourlyOnly == value) return; _hourlyOnly = value; Changed(nameof(HourlyOnly)); } }
        private bool _deliveryOnly;
        public bool DeliveryOnly { get => _deliveryOnly; set { if (_deliveryOnly == value) return; _deliveryOnly = value; Changed(nameof(DeliveryOnly)); } }
        private bool _freeDeliveryOnly;
        public bool FreeDeliveryOnly { get => _freeDeliveryOnly; set { if (_freeDeliveryOnly == value) return; _freeDeliveryOnly = value; Changed(nameof(FreeDeliveryOnly)); } }
        private bool _highlyRatedOnly;
        public bool HighlyRatedOnly { get => _highlyRatedOnly; set { if (_highlyRatedOnly == value) return; _highlyRatedOnly = value; Changed(nameof(HighlyRatedOnly)); } }
        private bool _instantOnly;
        public bool InstantOnly { get => _instantOnly; set { if (_instantOnly == value) return; _instantOnly = value; Changed(nameof(InstantOnly)); } }
        private bool _noCollateralOnly;
        public bool NoCollateralOnly { get => _noCollateralOnly; set { if (_noCollateralOnly == value) return; _noCollateralOnly = value; Changed(nameof(NoCollateralOnly)); } }
        private bool _childSeatOnly;
        public bool ChildSeatOnly { get => _childSeatOnly; set { if (_childSeatOnly == value) return; _childSeatOnly = value; if (!value && ChildSeatAgeCodes.Length > 0) ChildSeatAgeCodes = Array.Empty<string>(); Changed(nameof(ChildSeatOnly)); } }
        private bool _extendedTravelOnly;
        public bool ExtendedTravelOnly { get => _extendedTravelOnly; set { if (_extendedTravelOnly == value) return; _extendedTravelOnly = value; Changed(nameof(ExtendedTravelOnly)); } }
        private bool _unlimitedMileageOnly;
        public bool UnlimitedMileageOnly { get => _unlimitedMileageOnly; set { if (_unlimitedMileageOnly == value) return; _unlimitedMileageOnly = value; Changed(nameof(UnlimitedMileageOnly)); } }
        private bool _sportOnly;
        public bool SportOnly { get => _sportOnly; set { if (_sportOnly == value) return; _sportOnly = value; Changed(nameof(SportOnly)); } }
        public bool IsElectricFilter { get => SelectedFuel == "Điện"; set { if (value) SelectedFuel = "Điện"; else if (IsElectricFilter) SelectedFuel = "Tất cả nhiên liệu"; } }
        public bool IsHybridFilter { get => SelectedFuel == "Hybrid"; set { if (value) SelectedFuel = "Hybrid"; else if (IsHybridFilter) SelectedFuel = "Tất cả nhiên liệu"; } }
        public bool HasPriceFilter => !string.IsNullOrWhiteSpace(MinPrice) || !string.IsNullOrWhiteSpace(MaxPrice);
        public IReadOnlyList<AppliedCriterion> AppliedCriteria { get; private set; } = [];
        private void SetAppliedCriteria(IReadOnlyList<AppliedCriterion> criteria)
        {
            AppliedCriteria = criteria;
            OnPropertyChanged(nameof(AppliedCriteria));
        }
        public bool HasCategoryFilter => SelectedCategory?.CategoryId > 0;
        public bool HasBrandFilter => SelectedBrand != "Tất cả hãng xe";
        public bool HasSortFilter => SelectedSort != "Mặc định";
        public bool HasTransmissionFilter => SelectedTransmission != "Tất cả hộp số";
        private string[] _childSeatAgeCodes = Array.Empty<string>();
        public string[] ChildSeatAgeCodes { get => _childSeatAgeCodes; set { _childSeatAgeCodes = value; Changed(nameof(ChildSeatAgeCodes)); OnPropertyChanged(nameof(HasChildSeatFilter)); } }
        public bool HasChildSeatFilter => ChildSeatOnly || ChildSeatAgeCodes.Length > 0;
        public bool HasAnySelfDriveFilter => HasCategoryFilter || HasBrandFilter || HasTransmissionFilter || HasSortFilter ||
            !string.IsNullOrWhiteSpace(SearchText) || !string.IsNullOrWhiteSpace(MinPrice) || !string.IsNullOrWhiteSpace(MaxPrice) ||
            SelectedFuel != "Tất cả nhiên liệu" || SelectedSeats != "Tất cả số chỗ" || SelectedYear != "Tất cả năm" ||
            HourlyOnly || DeliveryOnly || FreeDeliveryOnly || HighlyRatedOnly || InstantOnly || NoCollateralOnly || DiscountOnly || HasChildSeatFilter ||
            ExtendedTravelOnly || UnlimitedMileageOnly || SportOnly || Purposes.Any(p => p.IsSelected);
        public Task<SelfDriveFacets> GetSelfDriveFacetsAsync() => _searchService.GetSelfDriveFacetsAsync(new CarSearchFilter
        {
            CityId = SelectedCity?.CityId > 0 ? SelectedCity.CityId : null,
            StartAtLocal = StartAtLocal,
            EndAtLocal = EndAtLocal
        });
        private CarSearchFilter CreateJourneyFilter() => new()
        {
            RentalMode = "WithDriver",
            CityId = SelectedCity?.CityId > 0 ? SelectedCity.CityId : null,
            DestinationCityId = SelectedRoute == "Nội thành" ? (SelectedCity?.CityId > 0 ? SelectedCity.CityId : null) : (DestinationCity?.CityId > 0 ? DestinationCity.CityId : null),
            RouteType = SelectedRoute switch { "Liên tỉnh" => "InterCityRoundTrip", "Liên tỉnh một chiều" => "InterCityOneWay", _ => "InnerCity" },
            StartAtLocal = StartAtLocal,
            EndAtLocal = EndAtLocal
        };
        private SearchCity RequireLongTermCity() => Cities.FirstOrDefault(c => c.IsActive && c.CityId > 0 && string.Equals(c.CityName.Trim(), "Cần Thơ", StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Địa điểm Cần Thơ chưa được cấu hình cho trang thuê dài hạn.");
        public Task<SelfDriveFacets> GetCurrentModeFacetsAsync() => IsLongTerm
            ? _searchService.GetLongTermFacetsAsync(new CarSearchFilter
            {
                RentalMode = "LongTerm",
                CityId = RequireLongTermCity().CityId,
                DurationMonths = SelectedDuration,
                StartAtLocal = StartAtLocal,
                EndAtLocal = EndAtLocal
            }) : IsWithDriver ? _searchService.GetWithDriverFacetsAsync(CreateJourneyFilter()) : GetSelfDriveFacetsAsync();
        private bool _isFilterPending;
        public bool IsFilterPending { get => _isFilterPending; private set { _isFilterPending = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanChooseResults)); } }
        private string _appliedFiltersSummary = "";
        public string AppliedFiltersSummary { get => _appliedFiltersSummary; private set { _appliedFiltersSummary = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasAppliedFiltersSummary)); } }
        public bool HasAppliedFiltersSummary => !string.IsNullOrWhiteSpace(AppliedFiltersSummary);
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; private set { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotLoading)); OnPropertyChanged(nameof(CanChooseResults)); CommandManager.InvalidateRequerySuggested(); } }
        public bool IsNotLoading => !IsLoading;
        public bool CanChooseResults => !IsLoading && !IsFilterPending;
        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }
        private bool _hasEmptyResults;
        public bool HasEmptyResults
        {
            get => _hasEmptyResults;
            private set
            {
                if (_hasEmptyResults == value) return;
                _hasEmptyResults = value;
                OnPropertyChanged();
            }
        }
        public int ResultCount => Cars.Count;
        public async Task InitializeAsync()
        {
            try { await LoadCategoriesAsync(); await SearchAsync(); }
            catch (Exception ex) { Cars.Clear(); OnPropertyChanged(nameof(ResultCount)); StatusMessage = "Không thể tải bộ lọc: " + ex.Message; }
        }
        public ICommand SearchCommand { get; }
        public ICommand ClearFilterCommand { get; }
        public CarSearchViewModel() : this(new SearchService()) { }
        public CarSearchViewModel(SearchService searchService)
        {
            _searchService = searchService;
            SearchCommand = new AsyncRelayCommand(async _ => await SearchAsync(), _ => !IsLoading);
            ClearFilterCommand = new AsyncRelayCommand(async _ => await ClearFiltersAsync(), _ => !IsLoading);
        }
        public async Task LoadCategoriesAsync()
        {
            var categories = await _searchService.GetCategoriesAsync();
            var brands = await _searchService.GetBrandsAsync();
            var selectedId = SelectedCategory?.CategoryId;
            Categories.Clear();
            Categories.Add(new VehicleCategory { CategoryId = 0, CategoryName = "Tất cả loại xe", IsActive = true });
            foreach (var c in categories) Categories.Add(c);
            SelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == selectedId) ?? Categories[0];
            var cities = await _searchService.GetCitiesAsync();
            var purposes = await _searchService.GetPurposesAsync();
            var cityId = SelectedCity?.CityId ?? _initialContext?.PickupCityId;
            var destinationId = DestinationCity?.CityId ?? _initialContext?.DestinationCityId;
            Cities.Clear(); Cities.Add(new SearchCity { CityId = 0, CityName = "Tất cả địa điểm" });
            foreach (var city in cities) Cities.Add(city);
            SelectedCity = IsLongTerm ? RequireLongTermCity() : Cities.FirstOrDefault(c => c.CityId == cityId) ?? Cities[0];
            DestinationCity = Cities.FirstOrDefault(c => c.CityId == destinationId);
            var locations = await _searchService.GetLocationsAsync();
            var selectedLocationId = SelectedDeliveryLocation?.LocationId;
            DeliveryLocations.Clear(); foreach (var l in locations) DeliveryLocations.Add(l);
            SelectedDeliveryLocation = DeliveryLocations.FirstOrDefault(l => l.LocationId == selectedLocationId);
            var selectedPurposes = Purposes.Where(p => p.IsSelected).Select(p => p.Code).Concat(_initialContext?.PurposeCodes ?? Array.Empty<string>()).ToHashSet();
            Purposes.Clear();
            foreach (var purpose in purposes)
            {
                var choice = new SearchPurposeChoice(purpose.PurposeCode, purpose.PurposeName) { IsSelected = selectedPurposes.Contains(purpose.PurposeCode) };
                choice.PropertyChanged += (_, _) => Changed(nameof(Purposes));
                Purposes.Add(choice);
            }
            _initialContext = null;
            var brand = SelectedBrand;
            Brands.Clear(); Brands.Add("Tất cả hãng xe");
            foreach (var b in brands) Brands.Add(b);
            SelectedBrand = Brands.Contains(brand) ? brand : Brands[0];
        }
        internal static decimal? ParsePrice(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (!decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out var result) || result < 0)
                throw new ArgumentException("Giá thuê không hợp lệ. Nhập số tiền VND, ví dụ 500000 hoặc 500.000.");
            return result;
        }
        private static double? ParseCoordinate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                throw new ArgumentException("Tọa độ phải là số hợp lệ, dùng dấu chấm thập phân.");
            return number;
        }
        public CarSearchFilter CreateFilter()
        {
            var filter = new CarSearchFilter
            {
                RentalMode = RentalMode,
                Keyword = SearchText.Trim(),
                CategoryId = HasCategoryFilter ? SelectedCategory!.CategoryId : null,
                Brand = HasBrandFilter ? SelectedBrand : null,
                Transmission = SelectedTransmission switch { "Số tự động" => "Automatic", "Số sàn" => "Manual", _ => null },
                FuelType = SelectedFuel switch { "Xăng" => "Gasoline", "Dầu diesel" => "Diesel", "Điện" => "Electric", "Hybrid" => "Hybrid", _ => null },
                Seats = byte.TryParse(SelectedSeats, out var seats) ? seats : null,
                MinYear = short.TryParse(SelectedYear, out var year) ? year : null,
                MinPrice = ParsePrice(MinPrice),
                MaxPrice = ParsePrice(MaxPrice),
                Sort = SelectedSort,
                DiscountOnly = DiscountOnly,
                StartAtLocal = StartAtLocal,
                EndAtLocal = EndAtLocal,
                CityId = IsLongTerm ? RequireLongTermCity().CityId : SelectedCity?.CityId > 0 ? SelectedCity.CityId : null,
                DestinationCityId = IsWithDriver && SelectedRoute == "Nội thành" ? (SelectedCity?.CityId > 0 ? SelectedCity.CityId : null) : (DestinationCity?.CityId > 0 ? DestinationCity.CityId : null),
                RouteType = SelectedRoute switch { "Liên tỉnh" => "InterCityRoundTrip", "Liên tỉnh một chiều" => "InterCityOneWay", _ => "InnerCity" },
                Latitude = DeliveryOnly && !FreeDeliveryOnly ? null : ParseCoordinate(LatitudeText),
                Longitude = DeliveryOnly && !FreeDeliveryOnly ? null : ParseCoordinate(LongitudeText),
                DurationMonths = SelectedDuration, CustomerId = CurrentUserService.Instance.UserId,
                PurposeCodes = Purposes.Where(p => p.IsSelected).Select(p => p.Code).ToArray(),
                ChildSeatAgeCodes = IsSelfDrive ? ChildSeatAgeCodes : Array.Empty<string>(),
                HourlyOnly = HourlyOnly,
                DeliveryOnly = DeliveryOnly,
                FreeDeliveryOnly = FreeDeliveryOnly,
                FreePickupInCityOnly = IsWithDriver && FreeDeliveryOnly,
                HighlyRatedOnly = HighlyRatedOnly,
                InstantOnly = InstantOnly,
                NoCollateralOnly = NoCollateralOnly,
                ChildSeatOnly = ChildSeatOnly,
                ExtendedTravelOnly = ExtendedTravelOnly,
                UnlimitedMileageOnly = UnlimitedMileageOnly,
                SportOnly = SportOnly
            };
            filter.Validate(); return filter;
        }
        public void CancelPendingSearch()
        {
            HasEmptyResults = false;
            ++_requestVersion; _searchCancellation?.Cancel(); _searchCancellation = null; IsLoading = false;
        }
        public async Task ToggleDeliveryAsync()
        {
            if (!IsSelfDrive || IsLoading) return;
            if (!DeliveryOnly && SelectedCity?.CityId is not > 0)
            {
                StatusMessage = "Chọn khu vực tìm xe trước khi bật giao nhận tận nơi.";
                return;
            }
            DeliveryOnly = !DeliveryOnly;
            await SearchAsync();
        }
        public async Task SearchAsync()
        {
            HasEmptyResults = false;
            var version = ++_requestVersion;
            var revision = _filterRevision;
            _searchCancellation?.Cancel();
            using var cancellation = new CancellationTokenSource();
            _searchCancellation = cancellation;
            CarSearchFilter filter;
            string summary;
            try
            {
                filter = CreateFilter();
                var parts = new List<AppliedCriterion>();
                if (!string.IsNullOrWhiteSpace(filter.Keyword)) parts.Add(new($"Từ khóa: {filter.Keyword}", () => SearchText = ""));
                if (HasCategoryFilter) parts.Add(new(SelectedCategory!.CategoryName, () => SelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == 0)));
                if (HasBrandFilter) parts.Add(new(SelectedBrand, () => SelectedBrand = "Tất cả hãng xe"));
                if (HasTransmissionFilter) parts.Add(new(SelectedTransmission, () => SelectedTransmission = "Tất cả hộp số"));
                if (filter.FuelType != null) parts.Add(new(SelectedFuel, () => SelectedFuel = "Tất cả nhiên liệu"));
                if (filter.Seats.HasValue) parts.Add(new($"{filter.Seats} chỗ", () => SelectedSeats = "Tất cả số chỗ"));
                if (filter.MinYear.HasValue) parts.Add(new($"Từ năm {filter.MinYear}", () => SelectedYear = "Tất cả năm"));
                if (filter.MinPrice.HasValue) parts.Add(new($"Giá từ {filter.MinPrice:N0} ₫", () => MinPrice = ""));
                if (filter.MaxPrice.HasValue) parts.Add(new($"Giá đến {filter.MaxPrice:N0} ₫", () => MaxPrice = ""));
                if (filter.DiscountOnly) parts.Add(new("Có ưu đãi", () => DiscountOnly = false));
                if (IsLongTerm && SelectedCity?.CityId > 0) parts.Add(new(SelectedCity.CityName));
                if (ExtendedTravelOnly && DestinationCity?.CityId > 0) parts.Add(new("Đến " + DestinationCity.CityName, () => ExtendedTravelOnly = false));
                if (HourlyOnly) parts.Add(new("Thuê giờ", () => HourlyOnly = false));
                if (filter.DeliveryOnly) parts.Add(new(DeliveryFilterLabel, () => DeliveryOnly = false));
                if (FreeDeliveryOnly) parts.Add(new(IsWithDriver ? $"Miễn phí đưa đón nội thành: {LocationSummary}" : SelectedDeliveryLocation != null ? $"Miễn phí đưa đón: {SelectedDeliveryLocation.DisplayName}" : "Miễn phí đưa đón", () => FreeDeliveryOnly = false));
                if (HighlyRatedOnly) parts.Add(new("Chủ xe uy tín", () => HighlyRatedOnly = false));
                if (InstantOnly) parts.Add(new("Hỗ trợ đặt nhanh", () => InstantOnly = false));
                if (NoCollateralOnly) parts.Add(new("Miễn thế chấp", () => NoCollateralOnly = false));
                if (filter.ChildSeatAgeCodes.Length > 0)
                    parts.Add(new("Ghế trẻ em: " + string.Join(" / ", filter.ChildSeatAgeCodes.Distinct().Select(code => code switch
                    {
                        "ChildSeat:0-12" => "0–12 tuổi (đa năng)",
                        "ChildSeat:0-1" => "0–1 tuổi",
                        "ChildSeat:1-4" => "1–4 tuổi",
                        "ChildSeat:4-7" => "4–7 tuổi",
                        "ChildSeat:7-12" => "7–12 tuổi",
                        _ => code
                    })), () => ChildSeatOnly = false));
                else if (filter.ChildSeatOnly) parts.Add(new("Ghế trẻ em (không yêu cầu độ tuổi)", () => ChildSeatOnly = false));
                if (ExtendedTravelOnly && DestinationCity?.CityId is not > 0) parts.Add(new("Di chuyển mở rộng", () => ExtendedTravelOnly = false));
                if (UnlimitedMileageOnly) parts.Add(new("Không giới hạn km", () => UnlimitedMileageOnly = false));
                if (SportOnly) parts.Add(new("Xe thể thao đã xác minh", () => SportOnly = false));
                if (IsLongTerm) parts.Add(new($"Gói {SelectedDuration} tháng"));
                parts.AddRange(Purposes.Where(p => p.IsSelected).Select(p => new AppliedCriterion(p.Name, () => p.IsSelected = false)));
                if (HasSortFilter) parts.Add(new(SelectedSort, () => SelectedSort = "Mặc định"));
                summary = parts.Count > 0 ? "Đang áp dụng: " + string.Join(" · ", parts.Select(p => p.Label)) : "";
                IsLoading = true; StatusMessage = "Đang tìm kiếm xe...";
                var results = await _searchService.SearchAsync(filter, cancellation.Token);
                if (version != _requestVersion) return;
                Cars.Clear(); foreach (var car in results) Cars.Add(car);
                SetAppliedCriteria(parts);
                OnPropertyChanged(nameof(ResultCount)); AppliedFiltersSummary = summary; IsFilterPending = revision != _filterRevision;
                HasEmptyResults = results.Count == 0;
                StatusMessage = results.Count == 0 ? "Không có xe phù hợp. Hãy thay đổi hoặc đặt lại bộ lọc." : $"Tìm thấy {results.Count} xe phù hợp.";
                StatusMessage += filter.StartAtLocal.HasValue ? " Đã kiểm tra trùng lịch thuê/bảo trì." : " Chưa kiểm tra lịch trống vì chưa chọn thời gian thuê.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (version == _requestVersion)
                {
                    Cars.Clear(); OnPropertyChanged(nameof(ResultCount));
                    SetAppliedCriteria([]);
                    AppliedFiltersSummary = "Chưa áp dụng thành công";
                    StatusMessage = "Không thể tìm xe: " + ex.Message;
                }
            }
            finally
            {
                if (version == _requestVersion) { IsLoading = false; _searchCancellation = null; }
            }
        }
        private void ResetFilterValues()
        {
            SearchText = ""; MinPrice = ""; MaxPrice = "";
            ChildSeatAgeCodes = Array.Empty<string>();
            SelectedSort = "Mặc định"; SelectedTransmission = "Tất cả hộp số"; SelectedFuel = "Tất cả nhiên liệu";
            SelectedSeats = "Tất cả số chỗ"; SelectedYear = "Tất cả năm"; SelectedBrand = "Tất cả hãng xe"; DiscountOnly = false;
            SelectedCategory = Categories.FirstOrDefault();
            HourlyOnly = false; DeliveryOnly = false; FreeDeliveryOnly = false; HighlyRatedOnly = false; InstantOnly = false; NoCollateralOnly = false; ChildSeatOnly = false; ExtendedTravelOnly = false; UnlimitedMileageOnly = false; SportOnly = false;
            SelectedDeliveryLocation = null; LatitudeText = ""; LongitudeText = "";
            foreach (var choice in Purposes) choice.IsSelected = false;
        }
        public async Task ClearFiltersAsync()
        {
            ResetFilterValues();
            // Keep rental mode, route context and the selected rental period.
            await SearchAsync();
        }
    }
}

namespace CRMS.ViewModels
{
    public sealed record AppliedCriterion(string Label, Action? Remove = null)
    {
        public bool CanRemove => Remove != null;
    }
    public sealed class SearchPurposeChoice : CRMS.ViewModels.BaseViewModel
    {
        public string Code { get; }
        public string Name { get; }
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); } }
        public SearchPurposeChoice(string code, string name) { Code = code; Name = name; }
    }

}
