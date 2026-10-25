
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
        // =====================================
        // SERVICES
        // =====================================
        private readonly SearchService _searchService = new();

        private readonly LongTermRateService
            _longTermRateService = new();

        // =====================================
        // DANH SACH XE VA LOAI XE
        // =====================================
        public ObservableCollection<CarSearchItem> Cars { get; }
            = new();

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
        // TIM KIEM THEO TEN XE
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
        // LOC THEO LOAI XE
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
        // LOC GIA
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
        // TRUYEN DONG
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
        // TRANG THAI
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
        // LOAD DANH SACH LOAI XE
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
        // KIEM TRA GIA NHAP VAO
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

            string value = transmission.Trim().ToLowerInvariant();

            bool isAutomatic =
                value.Contains("automatic") ||
                value.Contains("tự động") ||
                value == "at";

            bool isManual =
                value.Contains("manual") ||
                value.Contains("số sàn") ||
                value == "mt";

            if (SelectedTransmission == "Số tự động")
                return isAutomatic;

            if (SelectedTransmission == "Số sàn")
                return isManual;

            return true;
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

                // Lay danh sach xe tu SQL
                var results =
                    await _searchService.SearchCarsAsync(
                        keyword: SearchText,
                        categoryId: categoryId,
                        minPrice: IsLongTerm ? null : min,
                        maxPrice: IsLongTerm ? null : max);

                // =====================================
                // GIA UU DAI 3 THANG TREN DANH SACH
                // =====================================
                Dictionary<long, VehicleLongTermRate> rates = new();

                if (IsLongTerm)
                {
                    rates =
                        await _longTermRateService
                            .GetRatesByDurationAsync(
                                results.Select(x => x.VehicleId),
                                3);
                }

                List<CarSearchItem> matchedCars = new();

                foreach (var car in results)
                {
                    if (!MatchesTransmission(car.Transmission))
                        continue;

                    if (IsLongTerm)
                    {
                        // Chi hien xe co gia goi 3 thang
                        if (!rates.TryGetValue(
                            car.VehicleId, out var rate))
                        {
                            continue;
                        }

                        // Gia goc moi thang
                        car.LongTermBaseMonthlyRate =
                            rate.BaseMonthlyRate;

                        // Gia uu dai cua goi 3 thang
                        car.LongTermMonthlyRate =
                            rate.MonthlyRate;

                        car.LongTermDiscountPercent =
                            rate.DiscountPercent;

                        // Tong gia cho 3 thang
                        car.LongTermTotalPrice =
                            rate.MonthlyRate * 3;

                        // Loc gia/thang
                        if (min.HasValue &&
                            car.LongTermMonthlyRate < min.Value)
                            continue;

                        if (max.HasValue &&
                            car.LongTermMonthlyRate > max.Value)
                            continue;
                    }

                    matchedCars.Add(car);
                }

                // =====================================
                // SAP XEP XE
                // =====================================
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

                Cars.Clear();

                foreach (var car in sorted)
                {
                    Cars.Add(car);
                }

                OnPropertyChanged(nameof(ResultCount));

                StatusMessage =
                    $"Tìm thấy {Cars.Count} xe phù hợp.";
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

            await SearchAsync();
        }
    }
}
