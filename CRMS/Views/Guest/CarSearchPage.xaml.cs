using System.Windows.Controls.Primitives;
using System.Windows.Media;

using System;
using System.Windows;
using System.Windows.Controls;
using CRMS.Services;
using CRMS.ViewModels;
using CRMS.Views;
using CRMS.Views.Customer;

namespace CRMS.Views.Guest
{
    public partial class CarSearchPage : Page
    {
        private readonly CarSearchViewModel _viewModel;

        private bool _isLoaded = false;
        private DateTime? _beforeHourlyStart, _beforeHourlyEnd;



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
            DateTime? endAt,
            CarSearchContext? context = null, CarSearchViewModel? viewModel = null)
        {
            _viewModel = viewModel ?? new CarSearchViewModel();
            InitializeComponent();

            if (rentalMode != "SelfDrive" &&
                rentalMode != "WithDriver" &&
                rentalMode != "LongTerm")
            {
                rentalMode = "SelfDrive";
            }

            _viewModel.RentalMode = rentalMode;
            _viewModel.SetRentalPeriod(startAt, endAt);

            _viewModel.SetSearchContext(context);
            DataContext = _viewModel;

            Loaded += CarSearchPage_Loaded;
            Unloaded += (_, _) => { _viewModel.CancelPendingSearch(); _isLoaded = false; };
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

                _viewModel.Cars.Clear();
                _viewModel.StatusMessage = "Không thể tải bộ lọc: " + ex.Message;
            }
        }

        // Show navigation arrows only when quick filters exceed the available width.
        // LongTerm has a separate centered row and never shows these buttons.
        private void QuickFiltersScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // A template/layout event can fire while the visual tree is being built.
            if (ScrollLeftButton == null || ScrollRightButton == null)
                return;

            const double epsilon = 1.0;
            ScrollLeftButton.Visibility =
                QuickFiltersScroll.HorizontalOffset > epsilon
                    ? Visibility.Visible : Visibility.Collapsed;
            ScrollRightButton.Visibility =
                QuickFiltersScroll.ScrollableWidth - QuickFiltersScroll.HorizontalOffset > epsilon
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        // Scroll the quick filter ribbon. This only scrolls the screen, it never filters cars.
        private void ScrollQuickLeft_Click(object sender, RoutedEventArgs e)
        {
            QuickFiltersScroll.ScrollToHorizontalOffset(
                Math.Max(0, QuickFiltersScroll.HorizontalOffset - 310));
        }

        private void ScrollQuickRight_Click(object sender, RoutedEventArgs e)
        {
            QuickFiltersScroll.ScrollToHorizontalOffset(
                QuickFiltersScroll.HorizontalOffset + 310);
        }

        // UI events delegate filter state and queries to the existing search ViewModel.
        private void ChangeRentalTime_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new RentalTimeWindow(_viewModel.IsWithDriver, _viewModel.HourlyOnly) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
            {
                if (_viewModel.IsSelfDrive) _viewModel.HourlyOnly = dialog.RentalUnit == "Hour";
                _viewModel.SetRentalPeriod(dialog.StartAt, dialog.EndAt);
            }
        }

        private async void QuickOption_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsWithDriver && !_viewModel.HasValidDriverJourney)
            {
                ShowFilterDialog("Route");
                return;
            }
            if (_viewModel.IsSelfDrive && sender is ToggleButton toggle && toggle.Content?.ToString() == "Thuê giờ")
            {
                if (_viewModel.HourlyOnly)
                {
                    _beforeHourlyStart = _viewModel.StartAtLocal; _beforeHourlyEnd = _viewModel.EndAtLocal;
                    var start = _viewModel.StartAtLocal ?? DateTime.Now.AddHours(1);
                    if (start <= DateTime.Now) start = DateTime.Now.AddHours(1);
                    _viewModel.SetRentalPeriod(start, start.AddHours(4));
                }
                else if (_beforeHourlyStart.HasValue && _beforeHourlyEnd.HasValue && _beforeHourlyStart > DateTime.Now)
                    _viewModel.SetRentalPeriod(_beforeHourlyStart, _beforeHourlyEnd);
            }
            try { _viewModel.CreateFilter(); }
            catch (ArgumentException ex)
            {
                _viewModel.StatusMessage = ex.Message;
                VisualFiltersPopup.PlacementTarget = sender as UIElement ?? AdvancedFiltersToggle;
                VisualFiltersPopup.HorizontalOffset = 0;
                AdvancedFiltersToggle.IsChecked = true;
                return;
            }
            await _viewModel.SearchAsync();
        }

        private async void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.SearchAsync();
            if (!_viewModel.IsFilterPending && !_viewModel.StatusMessage.StartsWith("Không thể"))
                AdvancedFiltersToggle.IsChecked = false;
        }

        private void QuickFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton button) return;
            if (button.CommandParameter?.ToString() is "Category" or "Brand" or "Price" ||
                (_viewModel.IsLongTerm && button.CommandParameter?.ToString() is "Sort" or "Transmission"))
            {
                button.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
                if (_viewModel.IsWithDriver && !_viewModel.HasValidDriverJourney && !ChooseDriverRoute()) return;
                ShowFilterDialog(button.CommandParameter.ToString()!);
                return;
            }
            button.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
            VisualFiltersPopup.PlacementTarget = button;
            VisualFiltersPopup.HorizontalOffset = 0;
            AdvancedFiltersToggle.IsChecked = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ComboBox input = button.CommandParameter?.ToString() switch
                {
                    "Category" => CategoryFilterInput, "Brand" => BrandFilterInput,
                    "Sort" => SortFilterInput, _ => TransmissionFilterInput
                };
                input.BringIntoView(); input.Focus(); input.IsDropDownOpen = true;
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void ViewVehiclePhotos_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: CarSearchItem vehicle })
                new VehiclePhotosWindow(vehicle) { Owner = Window.GetWindow(this) }.ShowDialog();
        }

        private async void RemoveCriterion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: AppliedCriterion { Remove: { } remove } } || _viewModel.IsLoading) return;
            var wasHourly = _viewModel.HourlyOnly;
            remove();
            if (wasHourly && !_viewModel.HourlyOnly && _beforeHourlyStart > DateTime.Now && _beforeHourlyEnd.HasValue)
                _viewModel.SetRentalPeriod(_beforeHourlyStart, _beforeHourlyEnd);
            await _viewModel.SearchAsync();
        }

        private void FilterHelp_Click(object sender, RoutedEventArgs e)
        {
            // A help click must not bubble into the enclosing filter's Click handler.
            e.Handled = true;
            if (sender is not Button { Tag: string title } || FilterHelpViewModel.For(title) is not { } explanation) return;
            FilterHelpBackdrop.Visibility = Visibility.Visible;
            try { new FilterHelpWindow(explanation) { Owner = Window.GetWindow(this) }.ShowDialog(); }
            finally { FilterHelpBackdrop.Visibility = Visibility.Collapsed; }
        }

        private void OpenDetailFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string kind }) return;
            AdvancedFiltersToggle.IsChecked = false;
            ShowFilterDialog(kind);
        }

        private bool? ShowFilterDialog(string kind)
        {
            FilterHelpBackdrop.Visibility = Visibility.Visible;
            try
            {
                return new SelfDriveFilterWindow(_viewModel, kind) { Owner = Window.GetWindow(this) }.ShowDialog();
            }
            finally
            {
                FilterHelpBackdrop.Visibility = Visibility.Collapsed;
            }
        }

        private void SelfDriveChildSeat_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button) button.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
            AdvancedFiltersToggle.IsChecked = false;
            ShowFilterDialog("ChildSeat");
        }

        private async void SelfDriveTravel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button) button.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
            if (_viewModel.ExtendedTravelOnly)
            {
                _viewModel.ExtendedTravelOnly = false;
                await _viewModel.SearchAsync();
                return;
            }
            AdvancedFiltersToggle.IsChecked = false;
            ShowFilterDialog("Travel");
        }

        private bool ChooseDriverRoute() => ShowFilterDialog("Route") == true;
        private void DriverRoute_Click(object sender, RoutedEventArgs e) => ChooseDriverRoute();
        private async void DriverFreePickup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button) button.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
            if (!_viewModel.HasValidDriverJourney && !ChooseDriverRoute()) return;
            AdvancedFiltersToggle.IsChecked = false;
            _viewModel.FreeDeliveryOnly = !_viewModel.FreeDeliveryOnly;
            await _viewModel.SearchAsync();
        }

        private async void SelfDriveDelivery_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button) button.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
            AdvancedFiltersToggle.IsChecked = false;
            await _viewModel.ToggleDeliveryAsync();
        }

        private void OpenShortTermFilters_Click(object sender, RoutedEventArgs e)
        {
            VisualFiltersPopup.PlacementTarget = AdvancedFiltersToggle;
            VisualFiltersPopup.HorizontalOffset = -430;
        }


        private void CloseVisualFilters_Click(object sender, RoutedEventArgs e)
        {
            AdvancedFiltersToggle.IsChecked = false;
        }



        // =====================================
        // MO / DONG BO LOC GIA DAI HAN
        // =====================================


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

            if (!_viewModel.CanChooseResults) { _viewModel.StatusMessage = "Vui lòng áp dụng bộ lọc trước khi xem gói thuê."; return; }

            if (!EnsureCustomerLoggedIn()) return;

            var detailWindow =
                new LongTermDetailWindow(car, _viewModel.StartAtLocal, _viewModel.SelectedDuration);

            Window? mainWindow =
                Window.GetWindow(this);

            if (mainWindow != null)
            {
                detailWindow.Owner = mainWindow;
            }

            if (detailWindow.ShowDialog() == true && mainWindow?.DataContext is MainViewModel main)
                main.NavigateCommand.Execute("MyBookings");
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
            if (!_viewModel.CanChooseResults) { _viewModel.StatusMessage = "Vui lòng áp dụng bộ lọc trước khi chọn xe."; return; }

            // Lay xe tu DataContext cua Button
            if (sender is not Button button ||
                button.DataContext is not CarSearchItem car)
            {
                MessageBox.Show(
                    "Không xác định được xe đã chọn.");
                return;
            }

            if (!car.CanUseLegacyBooking)
            {
                MessageBox.Show("Báo giá trọn chuyến này chưa hỗ trợ đặt trực tiếp.", "Báo giá", MessageBoxButton.OK, MessageBoxImage.Information);
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
            if (_viewModel.RentalMode != "SelfDrive" &&
                _viewModel.RentalMode != "WithDriver")
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

            if (!_viewModel.StartAtLocal.HasValue ||
                !_viewModel.EndAtLocal.HasValue)
            {
                MessageBox.Show(
                    "Vui lòng chọn thời gian nhận và trả xe.",
                    "Thiếu thông tin",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (_viewModel.StartAtLocal.Value <= DateTime.Now ||
                _viewModel.EndAtLocal.Value <= _viewModel.StartAtLocal.Value)
            {
                MessageBox.Show(
                    "Thời gian thuê không hợp lệ.",
                    "Lỗi thời gian",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            var selection = new BookingSelection(
                CurrentUserService.Instance.UserId!.Value, car.VehicleId, _viewModel.RentalMode,
                _viewModel.StartAtLocal.Value, _viewModel.EndAtLocal.Value,
                car.QuoteUnit == "Hour" ? "Hour" : "Day", ExpectedTotal: car.EstimatedTripTotal);
            var dialog = new BookingCheckoutWindow(selection, $"{car.Brand} {car.Model}") { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true && Window.GetWindow(this)?.DataContext is MainViewModel main)
                main.NavigateCommand.Execute("MyBookings");
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
