
using System;
using System.Windows;
using System.Windows.Controls;
using CRMS.Services;
using CRMS.ViewModels;
using CRMS.Views;

namespace CRMS.Views.Guest
{
    public partial class CarSearchPage : Page
    {
        private readonly CarSearchViewModel _viewModel = new();

        private bool _isLoaded = false;

        private string _rentalMode = "SelfDrive";

        private DateTime? _startAt;
        private DateTime? _endAt;

        // =====================================
        // CONSTRUCTORS
        // =====================================

        public CarSearchPage()
            : this("SelfDrive", null, null)
        {
        }

        public CarSearchPage(string rentalMode)
            : this(rentalMode, null, null)
        {
        }

        public CarSearchPage(
            string rentalMode,
            DateTime? startAt,
            DateTime? endAt)
        {
            InitializeComponent();

            if (rentalMode != "SelfDrive" &&
                rentalMode != "WithDriver" &&
                rentalMode != "LongTerm")
            {
                rentalMode = "SelfDrive";
            }

            _rentalMode = rentalMode;

            _startAt = startAt;
            _endAt = endAt;

            _viewModel.RentalMode = rentalMode;
            _viewModel.SetRentalPeriod(startAt, endAt);

            DataContext = _viewModel;

            Loaded += CarSearchPage_Loaded;
        }

        // =====================================
        // LOAD DANH SACH XE TU SQL
        // =====================================

        private async void CarSearchPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_isLoaded)
                return;

            _isLoaded = true;

            try
            {
                await _viewModel.LoadCategoriesAsync();
                await _viewModel.SearchAsync();
            }
            catch (Exception ex)
            {
                _isLoaded = false;

                MessageBox.Show(
                    ex.Message,
                    "Lỗi tải danh sách xe",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =====================================
        // MO / DONG BO LOC GIA DAI HAN
        // =====================================

        private void ToggleLongTermFilter_Click(
            object sender,
            RoutedEventArgs e)
        {
            LongTermPriceFilter.Visibility =
                LongTermPriceFilter.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }

        // =====================================
        // XEM CHI TIET THUE DAI HAN
        // =====================================

        private void LongTermChoose_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.DataContext is not CarSearchItem car)
            {
                return;
            }

            var detailWindow =
                new LongTermDetailWindow(car);

            Window? mainWindow =
                Window.GetWindow(this);

            if (mainWindow != null)
            {
                detailWindow.Owner = mainWindow;
            }

            detailWindow.ShowDialog();
        }

        // =====================================
        // YEU CAU CUSTOMER DANG NHAP
        // =====================================

        private bool EnsureCustomerLoggedIn()
        {
            var session = CurrentUserService.Instance;

            // Guest nhan Chon xe thi mo LoginWindow
            if (session.IsGuest)
            {
                Window? mainWindow =
                    Window.GetWindow(this);

                var loginWindow = new LoginWindow();

                if (mainWindow != null)
                {
                    loginWindow.Owner = mainWindow;
                }

                bool? loginResult =
                    loginWindow.ShowDialog();

                // Neu dong LoginWindow thi dung lai
                if (loginResult != true)
                {
                    return false;
                }

                // Cap nhat giao dien sau dang nhap
                if (mainWindow?.DataContext is MainViewModel vm)
                {
                    vm.RefreshSession();

                    // Giu lai trang tim xe hien tai
                    if (session.IsCustomer)
                    {
                        vm.CurrentPage = this;
                    }
                }
            }

            // Chi Customer moi duoc tiep tuc dat xe
            if (!session.IsCustomer ||
                !session.UserId.HasValue)
            {
                MessageBox.Show(
                    "Chức năng đặt xe chỉ dành cho Customer.",
                    "Không có quyền",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            return true;
        }

        // =====================================
        // CHON XE - F05 CAR SEARCH
        // =====================================

        private void SelectCar_Click(
            object sender,
            RoutedEventArgs e)
        {
            // Lay xe tu DataContext cua Button
            if (sender is not Button button ||
                button.DataContext is not CarSearchItem car)
            {
                MessageBox.Show(
                    "Không xác định được xe đã chọn.");
                return;
            }

            // =====================================
            // 1. KIEM TRA DANG NHAP
            // =====================================

            // Guest se duoc chuyen sang LoginWindow
            if (!EnsureCustomerLoggedIn())
            {
                return;
            }

            // =====================================
            // 2. KIEM TRA LOAI THUE
            // =====================================

            // LongTerm do module rieng xu ly
            if (_rentalMode != "SelfDrive" &&
                _rentalMode != "WithDriver")
            {
                MessageBox.Show(
                    "Loại thuê này chưa được hỗ trợ tại đây.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            // =====================================
            // 3. KIEM TRA THOI GIAN
            // =====================================

            if (!_startAt.HasValue ||
                !_endAt.HasValue)
            {
                MessageBox.Show(
                    "Vui lòng chọn thời gian nhận và trả xe.",
                    "Thiếu thông tin",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (_startAt.Value <= DateTime.Now ||
                _endAt.Value <= _startAt.Value)
            {
                MessageBox.Show(
                    "Thời gian thuê không hợp lệ.",
                    "Lỗi thời gian",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            // =====================================
            // 4. LUU THONG TIN XE DUOC CHON
            // =====================================

            long customerId =
                CurrentUserService.Instance.UserId!.Value;

            var selection = new BookingSelection(
                customerId,
                car.VehicleId,
                _rentalMode,
                _startAt.Value,
                _endAt.Value
            );

            // Chuyen thong tin cho module Booking
            BookingSelectionService.SetSelection(selection);

            // =====================================
            // 5. CHUYEN SANG MODULE BOOKING
            // =====================================

            Window? mainWindow =
                Window.GetWindow(this);

            if (mainWindow?.DataContext is MainViewModel vm)
            {
                // Chuyen den trang Booking cua nhom
                // Module Booking se doc BookingSelectionService
                if (vm.NavigateCommand.CanExecute("Bookings"))
                {
                    vm.NavigateCommand.Execute("Bookings");
                }
                else
                {
                    MessageBox.Show(
                        "Đã chọn xe thành công.\n" +
                        "Module Booking chưa sẵn sàng.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
        }

        // =====================================
        // QUAY LAI HOMEPAGE
        // =====================================

        private void BackToHome_Click(
            object sender,
            RoutedEventArgs e)
        {
            Window? mainWindow =
                Window.GetWindow(this);

            if (mainWindow?.DataContext is MainViewModel vm)
            {
                vm.NavigateCommand.Execute("Home");
            }
        }
    }
}
