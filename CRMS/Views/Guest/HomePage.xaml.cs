using CRMS.Models;
using System.Linq;
using System.Threading.Tasks;
using CRMS.Services.Vehicles;

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using CRMS.Services;
using CRMS.ViewModels;
using CRMS.Views;

namespace CRMS.Views.Guest
{
    public partial class HomePage : Page
    {
        // =====================================
        // DIA DIEM VA BO LOC XE TU LAI
        // =====================================
        private string _selectedCity = "Cần Thơ";
        private int? _selectedCityId;
        private int? _longTermCityId;
        private bool _selfDriveHourly;
        private readonly SearchService _searchService;
        private Task? _cityLoadTask;

        private List<string> _selectedNeeds = new();
        private List<string> _selectedTrends = new();
        private List<string> _selectedBudgets = new();

        // =====================================
        // THOI GIAN XE TU LAI
        // =====================================
        private DateTime? _selfDriveStartAt;
        private DateTime? _selfDriveEndAt;

        // =====================================
        // THOI GIAN XE CO TAI XE
        // =====================================
        private DateTime? _driverStartAt;
        private DateTime? _driverEndAt;

        public HomePage() : this(new SearchService()) { }

        public HomePage(SearchService searchService)
        {
            _searchService = searchService;
            InitializeComponent();
            Loaded += LoadSearchCities;

            DateTime next = DateTime.Now.AddHours(1);

            DateTime defaultStart = new DateTime(
                next.Year,
                next.Month,
                next.Day,
                next.Hour,
                0,
                0);

            _selfDriveStartAt = defaultStart;
            _selfDriveEndAt = defaultStart.AddDays(1);

            _driverStartAt = defaultStart;
            _driverEndAt = defaultStart.AddHours(4);

            SelfDriveLocationButton.Content = _selectedCity;

            SelfDriveTimeButton.Content =
                $"{_selfDriveStartAt:HH:mm dd/MM} - " +
                $"{_selfDriveEndAt:HH:mm dd/MM}";

            DriverTimeButton.Content =
                $"{_driverStartAt:HH:mm dd/MM} - " +
                $"{_driverEndAt:HH:mm dd/MM}";
        }

        // =====================================
        // 1. DANG NHAP
        // =====================================
        private void Login_Click(
            object sender, RoutedEventArgs e)
        {
            Window? mainWindow = Window.GetWindow(this);

            var loginWindow = new LoginWindow();

            if (mainWindow != null)
                loginWindow.Owner = mainWindow;

            bool? result = loginWindow.ShowDialog();

            if (result == true &&
                mainWindow?.DataContext is MainViewModel vm)
            {
                vm.RefreshSession();
            }
        }

        // =====================================
        // 2. DANG KY
        // =====================================
        private void Register_Click(
            object sender, RoutedEventArgs e)
        {
            Window? mainWindow = Window.GetWindow(this);

            var registerWindow = new RegisterWindow();

            if (mainWindow != null)
                registerWindow.Owner = mainWindow;

            if (registerWindow.ShowDialog() == true)
            {
                MessageBox.Show(
                    "Đăng ký thành công! Vui lòng đăng nhập.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // =====================================
        // 3. DANG XUAT
        // =====================================
        private void Logout_Click(
            object sender, RoutedEventArgs e)
        {
            CurrentUserService.Instance.Logout();

            Window? mainWindow = Window.GetWindow(this);

            if (mainWindow?.DataContext is MainViewModel vm)
            {
                vm.RefreshSession();
            }
        }

        // =====================================
        // 4. HO SO CA NHAN
        // =====================================
        private void Profile_Click(
            object sender, RoutedEventArgs e)
        {
            if (!CurrentUserService.Instance.IsLoggedIn)
            {
                MessageBox.Show(
                    "Vui lòng đăng nhập để xem hồ sơ cá nhân.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            Window? mainWindow = Window.GetWindow(this);

            if (mainWindow?.DataContext is MainViewModel vm)
            {
                vm.NavigateCommand.Execute("Profile");
            }
            else
            {
                MessageBox.Show(
                    "Không thể mở trang hồ sơ lúc này.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // =====================================
        // 5. VE CHUNG TOI
        // =====================================
        private void About_Click(
            object sender, RoutedEventArgs e)
        {
            if (Content is ScrollViewer { Content: UIElement content } scroll)
                scroll.ScrollToVerticalOffset(AboutSection.TranslatePoint(new Point(), content).Y);
        }

        private void Service_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string mode } && DataContext is HomeViewModel home)
            {
                home.SelectModeCommand.Execute(mode);
                UpdateLayout();
                SearchPanel.BringIntoView();
            }
        }

        // =====================================
        // 6. DIA DIEM XE TU LAI
        // =====================================
        private void SelfDriveLocation_Click(
            object sender, RoutedEventArgs e)
        {
            Window? mainWindow = Window.GetWindow(this);

            var locationWindow =
                new LocationSearchWindow(_selectedCity, _selectedTrends, _selectedCityId, _selectedNeeds, _selectedBudgets);

            if (mainWindow != null)
                locationWindow.Owner = mainWindow;

            if (locationWindow.ShowDialog() == true)
            {
                _selectedCity = locationWindow.SelectedCity;
                _selectedCityId = locationWindow.SelectedCityId;

                _selectedNeeds = locationWindow.SelectedNeeds;
                _selectedTrends = locationWindow.SelectedTrends;
                _selectedBudgets = locationWindow.SelectedBudgets;

                SelfDriveLocationButton.Content = _selectedCity;
            }
        }

        // =====================================
        // 7. THOI GIAN XE TU LAI
        // =====================================
        private void SelfDriveTime_Click(
            object sender, RoutedEventArgs e)
        {
            Window? mainWindow = Window.GetWindow(this);

            var rentalWindow = new RentalTimeWindow(false, _selfDriveHourly);

            if (mainWindow != null)
                rentalWindow.Owner = mainWindow;

            if (rentalWindow.ShowDialog() == true)
            {
                _selfDriveHourly = rentalWindow.RentalUnit == "Hour";
                _selfDriveStartAt = rentalWindow.StartAt;
                _selfDriveEndAt = rentalWindow.EndAt;

                SelfDriveTimeButton.Content =
                    $"{rentalWindow.StartAt:HH:mm dd/MM} - " +
                    $"{rentalWindow.EndAt:HH:mm dd/MM}";
            }
        }

        // =====================================
        // 8. THOI GIAN XE CO TAI XE
        // =====================================
        private void DriverTime_Click(
            object sender, RoutedEventArgs e)
        {
            Window? mainWindow = Window.GetWindow(this);

            var rentalWindow = new RentalTimeWindow(true);

            if (mainWindow != null)
                rentalWindow.Owner = mainWindow;

            if (rentalWindow.ShowDialog() == true)
            {
                _driverStartAt = rentalWindow.StartAt;
                _driverEndAt = rentalWindow.EndAt;

                DriverTimeButton.Content =
                    $"{rentalWindow.StartAt:HH:mm dd/MM} - " +
                    $"{rentalWindow.EndAt:HH:mm dd/MM}";
            }
        }

        // =====================================
        // 9. TIM XE - CHUYEN SANG CAR SEARCH
        // =====================================
        private async void SearchCars_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            var mode = button.Tag?.ToString() ?? "SelfDrive";
            try
            {
                await EnsureSearchCitiesAsync();
                DateTime? start = mode == "SelfDrive" ? _selfDriveStartAt : mode == "WithDriver" ? _driverStartAt : null;
                DateTime? end = mode == "SelfDrive" ? _selfDriveEndAt : mode == "WithDriver" ? _driverEndAt : null;
                if (mode != "LongTerm")
                {
                    if (!start.HasValue || !end.HasValue) throw new ArgumentException("Vui lòng chọn thời gian thuê.");
                    AvailabilityRules.ValidatePeriod(start.Value, end.Value, DateTime.UtcNow, requireFutureStart: true);
                }
                var context = BuildSearchContext(mode);
                if (Window.GetWindow(this)?.DataContext is MainViewModel vm)
                    vm.CurrentPage = new CarSearchPage(mode, start, end, context);
            }
            catch (Exception ex)
            {
                _cityLoadTask = null;
                MessageBox.Show(ex.Message, "Tìm xe", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private Task EnsureSearchCitiesAsync() => _cityLoadTask ??= LoadSearchCitiesCoreAsync();
        private async void LoadSearchCities(object sender, RoutedEventArgs e)
        {
            try { await EnsureSearchCitiesAsync(); }
            catch (Exception ex) { _cityLoadTask = null; SelfDriveLocationButton.ToolTip = "Không thể tải địa điểm: " + ex.Message; }
        }
        private async Task LoadSearchCitiesCoreAsync()
        {
            var cities = await _searchService.GetCitiesAsync();
            var current = cities.FirstOrDefault(c => c.CityId == _selectedCityId)
                ?? cities.FirstOrDefault(c => c.CityName == _selectedCity);
            _selectedCityId = current?.CityId;
            _selectedCity = current?.CityName ?? "Tất cả địa điểm";
            SelfDriveLocationButton.Content = _selectedCity;
            _longTermCityId = cities.FirstOrDefault(c => c.CityName == "Cần Thơ")?.CityId;
            DriverPickupCombo.ItemsSource = new[] { new SearchCity { CityId=0, CityName="Chọn điểm đón" } }.Concat(cities).ToList();
            DriverDestinationCombo.ItemsSource = new[] { new SearchCity { CityId=0, CityName="Chọn điểm đến" } }.Concat(cities).ToList();
            DriverPickupCombo.SelectedItem = current ?? DriverPickupCombo.Items[0];
            DriverDestinationCombo.SelectedIndex = 0;
        }
        internal CarSearchContext BuildSearchContext(string mode)
        {
            var pickup = DriverPickupCombo.SelectedItem as SearchCity;
            var destination = DriverDestinationCombo.SelectedItem as SearchCity;
            var route = OneWayRadio.IsChecked == true ? "InterCityOneWay" : InterCityRadio.IsChecked == true ? "InterCityRoundTrip" : "InnerCity";
            if (mode == "WithDriver")
            {
                if (pickup?.CityId is not > 0) throw new ArgumentException("Vui lòng chọn điểm đón.");
                if (route != "InnerCity" && destination?.CityId is not > 0) throw new ArgumentException("Vui lòng chọn điểm đến.");
                if (route != "InnerCity" && pickup.CityId == destination!.CityId) throw new ArgumentException("Điểm đến phải khác điểm đón.");
            }
            if (mode == "LongTerm" && !_longTermCityId.HasValue)
                throw new ArgumentException("Địa điểm Cần Thơ chưa được cấu hình cho trang thuê dài hạn.");
            return new CarSearchContext
            {
                PickupCityId = mode == "WithDriver" ? pickup?.CityId : mode == "LongTerm" ? _longTermCityId : _selectedCityId,
                DestinationCityId = mode == "WithDriver" ? (route == "InnerCity" ? pickup?.CityId : destination?.CityId) : null,
                RouteType = route,
                HourlyOnly = mode == "SelfDrive" && _selfDriveHourly,
                Fuel = mode == "SelfDrive" ? (_selectedTrends.Contains("Electric") ? "Electric" : _selectedTrends.Contains("Hybrid") ? "Hybrid" : null) : null,
                PurposeCodes = mode == "SelfDrive" ? _selectedNeeds.Where(n => n != "ChildSeat" && n != "LongDistance").ToArray() : Array.Empty<string>(),
                ChildSeatOnly = mode == "SelfDrive" && _selectedNeeds.Contains("ChildSeat"),
                ExtendedTravelOnly = mode == "SelfDrive" && _selectedNeeds.Contains("LongDistance"),
                SportOnly = mode == "SelfDrive" && _selectedTrends.Contains("Sport"),
                BudgetCode = mode == "SelfDrive" ? _selectedBudgets.FirstOrDefault() : null
            };
        }

    }
}
