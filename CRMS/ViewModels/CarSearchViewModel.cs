
using CRMS.Helpers;
using CRMS.Models;
using CRMS.Services;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CRMS.ViewModels
{
    public class CarSearchViewModel : BaseViewModel
    {
        // Service truy van SQL Server
        private readonly SearchService _searchService = new();

        // Danh sach xe tim duoc
        public ObservableCollection<CarSearchItem> Cars { get; }
            = new();

        // Danh sach loai xe
        public ObservableCollection<VehicleCategory> Categories { get; }
            = new();

        // ====================================
        // 1. TIM KIEM THEO TEN XE
        // ====================================
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

        // ====================================
        // 2. LOC THEO LOAI XE
        // ====================================
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

        // ====================================
        // 3. GIA THUE THAP NHAT
        // ====================================
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

        // ====================================
        // 4. GIA THUE CAO NHAT
        // ====================================
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

        // ====================================
        // 5. TRANG THAI TAI DU LIEU
        // ====================================
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

        // ====================================
        // 6. THONG BAO KET QUA
        // ====================================
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

        // ====================================
        // 7. COMMANDS
        // ====================================
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

        // ====================================
        // 8. LOAD LOAI XE TU SQL SERVER
        // ====================================
        public async Task LoadCategoriesAsync()
        {
            var categories =
                await _searchService.GetCategoriesAsync();

            Categories.Clear();

            // Tat ca loai xe
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

        // ====================================
        // 9. CHUYEN GIA TU STRING SANG DECIMAL
        // ====================================
        private bool TryParsePrice(
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
                out decimal value);

            if (!success || value < 0)
                return false;

            price = value;

            return true;
        }

        // ====================================
        // 10. TIM KIEM XE
        // ====================================
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

                // Lay CategoryId dang chon
                int? categoryId = null;

                if (SelectedCategory != null &&
                    SelectedCategory.CategoryId > 0)
                {
                    categoryId = SelectedCategory.CategoryId;
                }

                // Goi SearchService de truy van SQL
                var result = await _searchService.SearchCarsAsync(
                    keyword: SearchText,
                    categoryId: categoryId,
                    minPrice: min,
                    maxPrice: max);

                // Cap nhat danh sach hien thi
                Cars.Clear();

                foreach (var car in result)
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
                    "Có lỗi khi tìm kiếm xe.";

                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ====================================
        // 11. XOA BO LOC
        // ====================================
        private async Task ClearFiltersAsync()
        {
            SearchText = "";
            MinPrice = "";
            MaxPrice = "";

            if (Categories.Count > 0)
            {
                SelectedCategory = Categories[0];
            }

            await SearchAsync();
        }

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

        // Xe tu lai hoac xe co tai xe
        public bool IsShortTerm =>
            RentalMode == "SelfDrive" ||
            RentalMode == "WithDriver";

        // Thue xe dai han
        public bool IsLongTerm =>
            RentalMode == "LongTerm";
    }
}
