
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

        public HomePage()
        {
            InitializeComponent();

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
            AboutSection.BringIntoView();
        }

        // =====================================
        // 6. DIA DIEM XE TU LAI
        // =====================================
        private void SelfDriveLocation_Click(
            object sender, RoutedEventArgs e)
        {
            Window? mainWindow = Window.GetWindow(this);

            var locationWindow =
                new LocationSearchWindow(_selectedCity);

            if (mainWindow != null)
                locationWindow.Owner = mainWindow;

            if (locationWindow.ShowDialog() == true)
            {
                _selectedCity = locationWindow.SelectedCity;

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

            var rentalWindow = new RentalTimeWindow(false);

            if (mainWindow != null)
                rentalWindow.Owner = mainWindow;

            if (rentalWindow.ShowDialog() == true)
            {
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
        private void SearchCars_Click(
            object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            string rentalMode =
                button.Tag?.ToString() ?? "SelfDrive";

            if (rentalMode == "SelfDrive")
            {
                if (!_selfDriveStartAt.HasValue ||
                    !_selfDriveEndAt.HasValue ||
                    _selfDriveStartAt.Value <= DateTime.Now ||
                    _selfDriveEndAt <= _selfDriveStartAt)
                {
                    MessageBox.Show(
                        "Vui lòng chọn lại thời gian thuê hợp lệ.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            if (rentalMode == "WithDriver")
            {
                if (!_driverStartAt.HasValue ||
                    !_driverEndAt.HasValue ||
                    _driverStartAt.Value <= DateTime.Now ||
                    _driverEndAt <= _driverStartAt)
                {
                    MessageBox.Show(
                        "Vui lòng chọn lại thời gian thuê hợp lệ.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                bool needsDestination =
                    InterCityRadio.IsChecked == true ||
                    OneWayRadio.IsChecked == true;

                if (needsDestination)
                {
                    var destination =
                        DriverDestinationCombo.SelectedItem
                        as ComboBoxItem;

                    if (destination == null ||
                        !destination.IsEnabled)
                    {
                        MessageBox.Show(
                            "Vui lòng chọn điểm đến.",
                            "Thông báo",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    var pickup =
                        DriverPickupCombo.SelectedItem
                        as ComboBoxItem;

                    if (pickup?.Content?.ToString() ==
                        destination.Content?.ToString())
                    {
                        MessageBox.Show(
                            "Điểm đến phải khác điểm đón.",
                            "Thông báo",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }
            }

            Window? mainWindow = Window.GetWindow(this);

            if (mainWindow?.DataContext is MainViewModel vm)
            {

                // Chuyển sang trang tìm xe và truyền thời gian thuê
                DateTime? startAt = rentalMode switch
                {
                    "SelfDrive" => _selfDriveStartAt,
                    "WithDriver" => _driverStartAt,
                    _ => null
                };

                DateTime? endAt = rentalMode switch
                {
                    "SelfDrive" => _selfDriveEndAt,
                    "WithDriver" => _driverEndAt,
                    _ => null
                };

                vm.CurrentPage = new CarSearchPage(
                    rentalMode,
                    startAt,
                    endAt);
            }
        }
    }
}
