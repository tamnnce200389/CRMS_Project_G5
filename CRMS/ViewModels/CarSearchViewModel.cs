
using CRMS.Helpers;
using CRMS.Models;
using CRMS.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CRMS.ViewModels
{
    public class CarSearchViewModel : BaseViewModel
    {
        private readonly SearchService _searchService = new();

        private readonly LongTermRateService
            _longTermRateService = new();

        private readonly VehicleAvailabilityService
            _availabilityService = new();

        // Danh sach xe
        public ObservableCollection<CarSearchItem> Cars { get; }
            = new();

        // Danh sach loai xe
        public ObservableCollection<VehicleCategory> Categories { get; }
            = new();

        // =====================================
        // LOAI DICH VU
        // =====================================
        private string _rentalMode = "SelfDrive";

        public string RentalMode
        {
            get => _rentalMode;
            set
            {
                if (_rentalMode == value)
                    return;

                _rentalMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsShortTerm));
                OnPropertyChanged(nameof(IsLongTerm));
            }
        }

        public bool IsShortTerm =>
            RentalMode == "SelfDrive" ||
            RentalMode == "WithDriver";

        public bool IsLongTerm =>
            RentalMode == "LongTerm";

        // =====================================
        // THOI GIAN THUE TU HOMEPAGE
        // =====================================
        public DateTime? StartAtLocal { get; private set; }

        public DateTime? EndAtLocal { get; private set; }

        public void SetRentalPeriod(
            DateTime? startAt,
            DateTime? endAt)
        {
            StartAtLocal = startAt;
            EndAtLocal = endAt;

            OnPropertyChanged(nameof(StartAtLocal));
            OnPropertyChanged(nameof(EndAtLocal));
        }

        // =====================================
        // TIM KIEM THEO TU KHOA
        // =====================================
        private string _searchText = "";

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
            }
        }

        // =====================================
        // LOAI XE
        // =====================================
        private VehicleCategory? _selectedCategory;

        public VehicleCategory? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value;
                OnPropertyChanged();
            }
        }

        // =====================================
        // KHOANG GIA
        // =====================================
        private string _minPrice = "";

        public string MinPrice
        {
            get => _minPrice;
            set
            {
                _minPrice = value;
                OnPropertyChanged();
            }
        }

        private string _maxPrice = "";

        public string MaxPrice
        {
            get => _maxPrice;
            set
            {
                _maxPrice = value;
                OnPropertyChanged();
            }
        }

        // =====================================
        // SAP XEP
        // =====================================
        public List<string> SortOptions { get; } = new()
        {
            "Mặc định",
            "Giá tăng dần",
            "Giá giảm dần",
            "Tên A-Z"
        };

        private string _selectedSort = "Mặc định";

        public string SelectedSort
        {
            get => _selectedSort;
            set
            {
                _selectedSort = value;
                OnPropertyChanged();
            }
        }

        // =====================================
        // HOP SO
        // =====================================
        public List<string> TransmissionOptions { get; } = new()
        {
            "Tất cả hộp số",
            "Số tự động",
            "Số sàn"
        };

        private string _selectedTransmission = "Tất cả hộp số";

        public string SelectedTransmission
        {
            get => _selectedTransmission;
            set
            {
                _selectedTransmission = value;
                OnPropertyChanged();
            }
        }

        // =====================================
        // TRANG THAI TIM KIEM
        // =====================================
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

        private string _statusMessage = "";

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public int ResultCount => Cars.Count;

        // =====================================
        // COMMANDS
        // =====================================
        public ICommand SearchCommand { get; }

        public ICommand ClearFilterCommand { get; }

        public CarSearchViewModel()
        {
            SearchCommand = new RelayCommand(
                async _ => await SearchAsync(),
                _ => !IsLoading);

            ClearFilterCommand = new RelayCommand(
                async _ => await ClearFiltersAsync(),
                _ => !IsLoading);
        }

        // =====================================
        // LOAD LOAI XE
        // =====================================
        public async Task LoadCategoriesAsync()
        {
            var categories =
                await _searchService.GetCategoriesAsync();

            Categories.Clear();

            Categories.Add(new VehicleCategory
            {
                CategoryId = 0,
                CategoryName = "Tất cả loại xe",
                IsActive = true
            });

            foreach (var category in categories)
            {
                Categories.Add(category);
            }

            SelectedCategory = Categories[0];
        }

        // =====================================
        // KIEM TRA GIA
        // =====================================
        private static bool TryParsePrice(
            string text,
            out decimal? price)
        {
            price = null;

            if (string.IsNullOrWhiteSpace(text))
                return true;

            bool success = decimal.TryParse(
                text.Trim(),
                NumberStyles.Number,
                CultureInfo.GetCultureInfo("vi-VN"),
                out decimal result);

            if (!success || result < 0)
                return false;

            price = result;
            return true;
        }

        // =====================================
        // LOC HOP SO
        // =====================================
        private bool MatchesTransmission(string transmission)
        {
            if (SelectedTransmission == "Tất cả hộp số")
                return true;

            string value = (transmission ?? "")
                .Trim()
                .ToLowerInvariant();

            bool isAutomatic =
                value.Contains("automatic") ||
                value.Contains("tự động") ||
                value == "at";

            bool isManual =
                value.Contains("manual") ||
                value.Contains("số sàn") ||
                value == "mt";

            return SelectedTransmission switch
            {
                "Số tự động" => isAutomatic,
                "Số sàn" => isManual,
                _ => true
            };
        }

        // =====================================
        // TIM KIEM XE
        // =====================================
        public async Task SearchAsync()
        {
            if (IsLoading)
                return;

            if (!TryParsePrice(MinPrice, out decimal? min))
            {
                StatusMessage = "Giá tối thiểu không hợp lệ.";
                return;
            }

            if (!TryParsePrice(MaxPrice, out decimal? max))
            {
                StatusMessage = "Giá tối đa không hợp lệ.";
                return;
            }

            if (min.HasValue &&
                max.HasValue &&
                min.Value > max.Value)
            {
                StatusMessage =
                    "Giá tối thiểu không được lớn hơn giá tối đa.";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Đang tìm kiếm xe...";

                int? categoryId = null;

                if (SelectedCategory != null &&
                    SelectedCategory.CategoryId > 0)
                {
                    categoryId = SelectedCategory.CategoryId;
                }

                // -------------------------------------
                // 1. TIM XE THEO THONG TIN CO BAN
                // -------------------------------------
                var results =
                    await _searchService.SearchCarsAsync(
                        keyword: SearchText,
                        categoryId: categoryId,
                        minPrice: IsLongTerm ? null : min,
                        maxPrice: IsLongTerm ? null : max);

                // -------------------------------------
                // 2. KIEM TRA XE CON TRONG
                // -------------------------------------
                bool availabilityChecked = false;

                if (IsShortTerm &&
                    StartAtLocal.HasValue &&
                    EndAtLocal.HasValue)
                {
                    var availableIds =
                        await _availabilityService
                            .GetAvailableVehicleIdsAsync(
                                StartAtLocal.Value,
                                EndAtLocal.Value);

                    // Chi giu xe con trong
                    results = results
                        .Where(car =>
                            availableIds.Contains(car.VehicleId))
                        .ToList();

                    availabilityChecked = true;
                }

                // -------------------------------------
                // 3. GIA DAI HAN GOI 3 THANG
                // -------------------------------------
                Dictionary<long, VehicleLongTermRate> rates
                    = new();

                if (IsLongTerm)
                {
                    rates = await _longTermRateService
                        .GetRatesByDurationAsync(
                            results.Select(x => x.VehicleId),
                            3);
                }

                // -------------------------------------
                // 4. LOC HOP SO VA GIA THANG
                // -------------------------------------
                List<CarSearchItem> matchedCars = new();

                foreach (var car in results)
                {
                    if (!MatchesTransmission(car.Transmission))
                        continue;

                    if (IsLongTerm)
                    {
                        if (!rates.TryGetValue(
                            car.VehicleId,
                            out var rate))
                        {
                            continue;
                        }

                        car.LongTermBaseMonthlyRate =
                            rate.BaseMonthlyRate;

                        car.LongTermMonthlyRate =
                            rate.MonthlyRate;

                        car.LongTermDiscountPercent =
                            rate.DiscountPercent;

                        car.LongTermTotalPrice =
                            rate.MonthlyRate * 3;

                        if (min.HasValue &&
                            car.LongTermMonthlyRate < min.Value)
                            continue;

                        if (max.HasValue &&
                            car.LongTermMonthlyRate > max.Value)
                            continue;
                    }

                    matchedCars.Add(car);
                }

                // -------------------------------------
                // 5. SAP XEP
                // -------------------------------------
                IEnumerable<CarSearchItem> sorted = matchedCars;

                switch (SelectedSort)
                {
                    case "Giá tăng dần":
                        sorted = matchedCars.OrderBy(x =>
                            IsLongTerm
                                ? x.LongTermMonthlyRate
                                : x.DailyRate);
                        break;

                    case "Giá giảm dần":
                        sorted = matchedCars.OrderByDescending(x =>
                            IsLongTerm
                                ? x.LongTermMonthlyRate
                                : x.DailyRate);
                        break;

                    case "Tên A-Z":
                        sorted = matchedCars
                            .OrderBy(x => x.Brand)
                            .ThenBy(x => x.Model);
                        break;
                }

                // -------------------------------------
                // 6. CAP NHAT ITEMS CONTROL
                // -------------------------------------
                Cars.Clear();

                foreach (var car in sorted)
                {
                    Cars.Add(car);
                }

                OnPropertyChanged(nameof(ResultCount));

                if (availabilityChecked)
                {
                    StatusMessage =
                        $"Tìm thấy {Cars.Count} xe phù hợp và " +
                        "không bị trùng lịch thuê/bảo trì.";
                }
                else if (IsShortTerm)
                {
                    StatusMessage =
                        $"Tìm thấy {Cars.Count} xe. " +
                        "Chưa kiểm tra lịch vì chưa có thời gian thuê.";
                }
                else
                {
                    StatusMessage =
                        $"Tìm thấy {Cars.Count} xe có gói thuê dài hạn. " +
                        "Lịch trống sẽ kiểm tra khi chọn ngày thuê.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage =
                    "Lỗi tìm kiếm xe: " + ex.Message;

                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // =====================================
        // XOA BO LOC
        // =====================================
        private async Task ClearFiltersAsync()
        {
            SearchText = "";
            MinPrice = "";
            MaxPrice = "";

            SelectedSort = "Mặc định";
            SelectedTransmission = "Tất cả hộp số";

            if (Categories.Count > 0)
                SelectedCategory = Categories[0];

            // Giu nguyen thoi gian thue da chon
            await SearchAsync();
        }
    }
}
