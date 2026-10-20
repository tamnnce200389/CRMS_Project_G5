
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

            // Lay moc gio tiep theo tu dong ho may
            DateTime next = DateTime.Now.AddHours(1);

            DateTime defaultStart = new DateTime(
                next.Year,
                next.Month,
                next.Day,
                next.Hour,
                0,
                0);

            // Xe tu lai mac dinh 1 ngay
            _selfDriveStartAt = defaultStart;
            _selfDriveEndAt = defaultStart.AddDays(1);

            // Xe co tai xe mac dinh 4 gio
            _driverStartAt = defaultStart;
            _driverEndAt = defaultStart.AddHours(4);

            // Hien thi dia diem
            SelfDriveLocationButton.Content = _selectedCity;

            // Hien thi thoi gian xe tu lai
            SelfDriveTimeButton.Content =
                $"{_selfDriveStartAt:HH:mm dd/MM} - " +
                $"{_selfDriveEndAt:HH:mm dd/MM}";

            // Hien thi thoi gian xe co tai xe
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
        // 4. VE CHUNG TOI
        // =====================================
        private void About_Click(
            object sender, RoutedEventArgs e)
        {
            AboutSection.BringIntoView();
        }

        // =====================================
        // 5. DIA DIEM XE TU LAI
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
                // Luu dia diem da chon
                _selectedCity = locationWindow.SelectedCity;

                // Luu cac bo loc
                _selectedNeeds = locationWindow.SelectedNeeds;
                _selectedTrends = locationWindow.SelectedTrends;
                _selectedBudgets = locationWindow.SelectedBudgets;

                // Cap nhat nut dia diem
                SelfDriveLocationButton.Content = _selectedCity;
            }
        }

        // =====================================
        // 6. THOI GIAN XE TU LAI
        // =====================================
        private void SelfDriveTime_Click(
            object sender, RoutedEventArgs e)
        {
            Window? mainWindow = Window.GetWindow(this);

            // false: Cho phep thue ngay va thue gio
            var rentalWindow = new RentalTimeWindow(false);

            if (mainWindow != null)
                rentalWindow.Owner = mainWindow;

            if (rentalWindow.ShowDialog() == true)
            {
                // Luu thoi gian
                _selfDriveStartAt = rentalWindow.StartAt;
                _selfDriveEndAt = rentalWindow.EndAt;

                // Cap nhat noi dung nut
                SelfDriveTimeButton.Content =
                    $"{rentalWindow.StartAt:HH:mm dd/MM} - " +
                    $"{rentalWindow.EndAt:HH:mm dd/MM}";
            }
        }

        // =====================================
        // 7. THOI GIAN XE CO TAI XE
        // =====================================
        private void DriverTime_Click(
            object sender, RoutedEventArgs e)
        {
            Window? mainWindow = Window.GetWindow(this);

            // true: Cho ngay bat dau, gio don,
            // va thoi luong thue
            var rentalWindow = new RentalTimeWindow(true);

            if (mainWindow != null)
                rentalWindow.Owner = mainWindow;

            if (rentalWindow.ShowDialog() == true)
            {
                // Luu thoi gian
                _driverStartAt = rentalWindow.StartAt;
                _driverEndAt = rentalWindow.EndAt;

                // Cap nhat noi dung nut
                DriverTimeButton.Content =
                    $"{rentalWindow.StartAt:HH:mm dd/MM} - " +
                    $"{rentalWindow.EndAt:HH:mm dd/MM}";
            }
        }


        // =====================================
        // 8. TIM XE - CHUYEN SANG CAR SEARCH
        // =====================================
        private void SearchCars_Click(
            object sender, RoutedEventArgs e)
        {
            // Xac dinh dich vu dang tim
            if (sender is not Button button)
                return;

            string rentalMode =
                button.Tag?.ToString() ?? "SelfDrive";

            // Kiem tra thoi gian xe tu lai
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

            // Kiem tra xe co tai xe
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

                // Lien tinh phai co diem den
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

            // Lay MainWindow dang chua HomePage
            Window? mainWindow = Window.GetWindow(this);

            if (mainWindow?.DataContext is MainViewModel vm)
            {
                // Tao trang ket qua theo dung loai dich vu
                CarSearchPage searchPage =
                    new CarSearchPage(rentalMode);

                // Chuyen sang trang tim xe
                vm.CurrentPage = searchPage;
            }
        }

    }
}
