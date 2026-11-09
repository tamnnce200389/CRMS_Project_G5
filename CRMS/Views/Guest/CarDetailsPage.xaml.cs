using CRMS.Models;
using CRMS.Services;
using CRMS.ViewModels;
using CRMS.Views.Customer;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CRMS.Views.Guest;

public partial class CarDetailsPage : Page
{
    private readonly CarSearchItem _car;
    private readonly string _rentalMode;
    private readonly BookingService _bookingService = new();
    private readonly LongTermRateService _longTermRateService = new();
    private readonly Dictionary<int, VehicleLongTermRate> _longTermRates = new();
    private readonly Page? _returnPage;
    private readonly Guid _requestKey = Guid.NewGuid();
    private bool _placing;
    private bool _initializing = true;

    private decimal _driverDailyFee = 350000m;
    private decimal _depositPercent = 50m;
    private decimal _lastGrossAmount;
    private int? _longTermMonths;
    private decimal? _longTermPackageTotal;
    private VoucherPreview? _voucherPreview;

    public CarDetailsPage(
        CarSearchItem car,
        string rentalMode,
        DateTime? presetStartDate = null,
        DateTime? presetEndDate = null,
        int? longTermMonths = null,
        decimal? longTermPackageTotal = null,
        Page? returnPage = null)
    {
        InitializeComponent();
        _car = car ?? throw new ArgumentNullException(nameof(car));
        _rentalMode = rentalMode == "WithDriver" ? "WithDriver" : "SelfDrive";
        _longTermMonths = longTermMonths;
        _longTermPackageTotal = longTermPackageTotal;
        _returnPage = returnPage;

        BrandText.Text = car.Brand;
        ModelText.Text = car.Model;
        RateText.Text = $"{car.DailyRate:N0} đ/ngày";
        CategoryText.Text = $"Loại xe: {car.CategoryName}";
        YearText.Text = $"Năm sản xuất: {car.ManufactureYear}";
        SeatText.Text = $"Số chỗ: {car.SeatCount}";
        TransmissionText.Text = $"Hộp số: {car.Transmission}";
        FuelText.Text = $"Nhiên liệu: {car.FuelType}";

        if (!string.IsNullOrWhiteSpace(car.ImageUrl) &&
            Uri.TryCreate(car.ImageUrl, UriKind.Absolute, out var imageUri) &&
            (imageUri.Scheme == Uri.UriSchemeHttp || imageUri.Scheme == Uri.UriSchemeHttps))
        {
            try { VehicleImage.Source = new BitmapImage(imageUri); }
            catch { /* Hình ảnh lỗi không làm ảnh hưởng chức năng đặt xe. */ }
        }

        RentalTypeBox.SelectedIndex = _rentalMode == "WithDriver" ? 1 : 0;
        StartDate.SelectedDate = presetStartDate?.Date ?? DateTime.Today.AddDays(1);
        EndDate.SelectedDate = presetEndDate?.Date ?? DateTime.Today.AddDays(2);

        if (_longTermMonths.HasValue)
        {
            // Chế độ thuê dài hạn: ẩn hoàn toàn phần chọn ngày, chỉ chọn loại gói tháng
            LongTermPlanPanel.Visibility = Visibility.Visible;
            LongTermBanner.Visibility = Visibility.Visible;
            RentalTypeBox.SelectedIndex = 0;
            RentalTypeBox.IsEnabled = false;

            for (int i = 0; i < LongTermPlanBox.Items.Count; i++)
            {
                if (LongTermPlanBox.Items[i] is ComboBoxItem item &&
                    item.Tag?.ToString() == _longTermMonths.Value.ToString())
                {
                    LongTermPlanBox.SelectedIndex = i;
                    break;
                }
            }
            UpdateLongTermDatesAndBanner();
        }

        StartDate.SelectedDateChanged += (_, _) => OnScheduleChanged();
        EndDate.SelectedDateChanged += (_, _) => OnScheduleChanged();
        StartTime.TextChanged += (_, _) => OnScheduleChanged();
        EndTime.TextChanged += (_, _) => OnScheduleChanged();

        Loaded += async (_, _) =>
        {
            try
            {
                var policy = await _bookingService.GetPricingPolicyAsync();
                _driverDailyFee = policy.DriverDailyFee;
                _depositPercent = policy.DepositPercent;

                if (_longTermMonths.HasValue)
                {
                    var rates = await _longTermRateService.GetRatesByVehicleAsync(_car.VehicleId);
                    _longTermRates.Clear();
                    foreach (var r in rates)
                        _longTermRates[(int)r.DurationMonths] = r;
                    UpdateLongTermDatesAndBanner();
                }
            }
            catch
            {
                // Giữ giá trị mặc định nếu chưa kết nối được bảng RentalPolicies.
            }
            finally
            {
                _initializing = false;
                UpdateEstimate();
            }
        };

        _initializing = false;
        UpdateEstimate();
    }

    private string CurrentRentalType => _longTermMonths.HasValue
        ? "LongTerm"
        : (RentalTypeBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? _rentalMode;

    private void UpdateLongTermDatesAndBanner()
    {
        if (!_longTermMonths.HasValue) return;
        int months = _longTermMonths.Value;
        DateTime start = StartDate.SelectedDate?.Date ?? DateTime.Today.AddDays(1);
        if (start <= DateTime.Today) start = DateTime.Today.AddDays(1);
        DateTime end = start.AddMonths(months);
        StartDate.SelectedDate = start;
        EndDate.SelectedDate = end;
        EndDate.IsEnabled = false;

        if (_longTermRates.TryGetValue(months, out var rate))
        {
            _longTermPackageTotal = rate.MonthlyRate * months;
        }
        else if (!_longTermPackageTotal.HasValue)
        {
            decimal discount = months switch { 3 => 0.02m, 6 => 0.03m, 9 => 0.04m, 12 => 0.05m, _ => 0m };
            decimal monthly = Math.Round(_car.DailyRate * 30m * (1m - discount), 2);
            _longTermPackageTotal = monthly * months;
        }

        LongTermBannerText.Text =
            $"Gói thuê dài hạn {months} tháng — Giá trọn gói xe: {_longTermPackageTotal.Value:N0} đ";
    }

    private void LongTermPlanBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (LongTermPlanBox.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out int months))
        {
            _longTermMonths = months;
            _longTermPackageTotal = null;
            UpdateLongTermDatesAndBanner();
            InvalidateVoucherPreview();
            UpdateEstimate();
        }
    }

    private void RentalTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        InvalidateVoucherPreview();
        UpdateEstimate();
    }

    private void OnScheduleChanged()
    {
        if (_initializing) return;
        if (_longTermMonths.HasValue && StartDate.SelectedDate.HasValue)
        {
            _initializing = true;
            EndDate.SelectedDate = StartDate.SelectedDate.Value.Date.AddMonths(_longTermMonths.Value);
            _initializing = false;
            UpdateLongTermDatesAndBanner();
        }
        InvalidateVoucherPreview();
        UpdateEstimate();
    }

    private void InvalidateVoucherPreview()
    {
        _voucherPreview = null;
        if (VoucherMessageText != null && !string.IsNullOrWhiteSpace(VoucherCodeBox?.Text))
        {
            VoucherMessageText.Text = "Thông tin thuê đã thay đổi. Hãy nhấn 'Kiểm tra & áp dụng thử' nếu dùng Voucher.";
            VoucherMessageText.Foreground = Brushes.DarkSlateGray;
        }
    }

    private void UpdateEstimate()
    {
        if (EstimateText == null || DepositEstimateText == null) return;
        if (!TryDateTime(StartDate.SelectedDate, StartTime.Text, out var start) ||
            !TryDateTime(EndDate.SelectedDate, EndTime.Text, out var end) || end <= start)
        {
            EstimateText.Text = "Vui lòng chọn thời gian hợp lệ.";
            DepositEstimateText.Text = "";
            return;
        }

        var days = Math.Max(1, (int)Math.Ceiling((end - start).TotalSeconds / 86400d));
        decimal vehicleAmount = (_longTermMonths.HasValue && _longTermPackageTotal.HasValue)
            ? _longTermPackageTotal.Value
            : days * _car.DailyRate;

        bool withDriver = CurrentRentalType == "WithDriver";
        decimal driverAmount = withDriver ? days * _driverDailyFee : 0m;
        decimal gross = vehicleAmount + driverAmount;
        _lastGrossAmount = gross;
        decimal discount = _voucherPreview?.DiscountAmount ?? 0m;
        if (discount >= gross) discount = 0m;
        decimal total = gross - discount;
        decimal deposit = Math.Round(total * _depositPercent / 100m, 2, MidpointRounding.AwayFromZero);

        string vehiclePart = (_longTermMonths.HasValue && _longTermPackageTotal.HasValue)
            ? $"Gói {_longTermMonths.Value} tháng ({start:dd/MM/yyyy} → {end:dd/MM/yyyy} · {days} ngày): {vehicleAmount:N0} đ"
            : $"Tiền xe ({days} ngày × {_car.DailyRate:N0} đ): {vehicleAmount:N0} đ";

        string driverPart = withDriver
            ? $"\n+ Tài xế ({days} ngày × {_driverDailyFee:N0} đ): {driverAmount:N0} đ"
            : "";

        string discountPart = discount > 0
            ? $"\n− Voucher ({_voucherPreview!.VoucherCode}): -{discount:N0} đ"
            : "";

        EstimateText.Text = $"{vehiclePart}{driverPart}{discountPart}\n= Tổng cộng: {total:N0} đ";
        DepositEstimateText.Text = $"Tiền cọc cần thanh toán ({_depositPercent:0.##}%): {deposit:N0} đ";
    }

    private bool EnsureCustomerLoggedIn()
    {
        if (CurrentUserService.Instance.IsCustomer && CurrentUserService.Instance.UserId.HasValue)
            return true;

        var answer = MessageBox.Show(
            "Bạn cần đăng nhập tài khoản Customer để thực hiện thao tác này. Đăng nhập ngay?",
            "Yêu cầu đăng nhập", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (answer != MessageBoxResult.Yes) return false;

        var owner = Window.GetWindow(this);
        var login = new LoginWindow();
        if (owner != null) login.Owner = owner;
        if (login.ShowDialog() != true || !CurrentUserService.Instance.IsCustomer ||
            !CurrentUserService.Instance.UserId.HasValue)
        {
            MessageBox.Show("Vui lòng đăng nhập bằng tài khoản Customer để tiếp tục.");
            return false;
        }

        if (owner?.DataContext is MainViewModel mainVm)
        {
            var currentPage = mainVm.CurrentPage;
            mainVm.RefreshSession();
            if (currentPage != null && !mainVm.IsManagement)
                mainVm.CurrentPage = currentPage;
        }

        return true;
    }

    private async void LoadVouchers_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureCustomerLoggedIn()) return;
        if (!TryDateTime(StartDate.SelectedDate, StartTime.Text, out var startLocal) ||
            !TryDateTime(EndDate.SelectedDate, EndTime.Text, out var endLocal) || endLocal <= startLocal)
        {
            MessageBox.Show("Vui lòng chọn thời gian hợp lệ trước khi tải Voucher.");
            return;
        }

        try
        {
            var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
            var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();
            var vouchers = await _bookingService.GetAvailableVouchersAsync(
                CurrentUserService.Instance.UserId!.Value,
                _car.VehicleId,
                CurrentRentalType,
                startUtc,
                endUtc,
                _longTermMonths);

            VoucherBox.ItemsSource = vouchers;
            VoucherBox.SelectedIndex = vouchers.Count > 0 ? 0 : -1;
            VoucherMessageText.Text = vouchers.Count == 0
                ? "Hiện không có Voucher khả dụng đạt mức đơn tối thiểu."
                : $"Tìm thấy {vouchers.Count} Voucher phù hợp. Nhấn 'Kiểm tra & áp dụng thử' để tính tiền giảm.";
            VoucherMessageText.Foreground = Brushes.DarkSlateGray;
        }
        catch (Exception ex)
        {
            VoucherMessageText.Text = "Không tải được Voucher: " + ex.Message;
            VoucherMessageText.Foreground = Brushes.Firebrick;
        }
    }

    private void VoucherBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VoucherBox.SelectedValue is string code && !string.IsNullOrWhiteSpace(code))
            VoucherCodeBox.Text = code;
    }

    private void VoucherCodeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing) return;
        _voucherPreview = null;
        UpdateEstimate();
    }

    private async void ApplyVoucher_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureCustomerLoggedIn()) return;
        var code = VoucherCodeBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            VoucherMessageText.Text = "Vui lòng chọn hoặc nhập mã Voucher.";
            VoucherMessageText.Foreground = Brushes.Firebrick;
            return;
        }

        if (!TryDateTime(StartDate.SelectedDate, StartTime.Text, out var startLocal) ||
            !TryDateTime(EndDate.SelectedDate, EndTime.Text, out var endLocal) || endLocal <= startLocal)
        {
            MessageBox.Show("Thời gian thuê không hợp lệ.");
            return;
        }

        ApplyVoucherButton.IsEnabled = false;
        try
        {
            var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
            var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();
            _voucherPreview = await _bookingService.PreviewVoucherAsync(
                CurrentUserService.Instance.UserId!.Value,
                _car.VehicleId,
                CurrentRentalType,
                startUtc,
                endUtc,
                code,
                _longTermMonths);

            VoucherMessageText.Text =
                $"Đã áp dụng thử mã {_voucherPreview.VoucherCode}: giảm {_voucherPreview.DiscountAmount:N0} đ.";
            VoucherMessageText.Foreground = Brushes.ForestGreen;
            UpdateEstimate();
        }
        catch (Exception ex)
        {
            _voucherPreview = null;
            VoucherMessageText.Text = "Không thể áp dụng Voucher: " + ex.Message;
            VoucherMessageText.Foreground = Brushes.Firebrick;
            UpdateEstimate();
        }
        finally
        {
            ApplyVoucherButton.IsEnabled = true;
        }
    }

    private void ClearVoucher_Click(object sender, RoutedEventArgs e)
    {
        VoucherBox.SelectedIndex = -1;
        VoucherCodeBox.Clear();
        _voucherPreview = null;
        VoucherMessageText.Text = "Đã bỏ mã Voucher.";
        VoucherMessageText.Foreground = Brushes.DarkSlateGray;
        UpdateEstimate();
    }

    private async void PlaceBooking_Click(object sender, RoutedEventArgs e)
    {
        if (_placing) return;
        if (!EnsureCustomerLoggedIn()) return;

        if (!TryDateTime(StartDate.SelectedDate, StartTime.Text, out var startLocal) ||
            !TryDateTime(EndDate.SelectedDate, EndTime.Text, out var endLocal) || endLocal <= startLocal)
        {
            MessageBox.Show("Ngày giờ nhận/trả xe không hợp lệ. Giờ nhập theo định dạng HH:mm.");
            return;
        }

        _placing = true;
        PlaceButton.IsEnabled = false;
        try
        {
            var depositMethod = (DepositMethodBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "VNPay";
            var id = await _bookingService.CreateAsync(new CreateBookingRequest
            {
                CustomerId = CurrentUserService.Instance.UserId!.Value,
                VehicleId = _car.VehicleId,
                RentalType = CurrentRentalType,
                DepositMethod = depositMethod,
                StartAtUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime(),
                EndAtUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime(),
                RequestKey = _requestKey,
                VoucherCode = string.IsNullOrWhiteSpace(VoucherCodeBox.Text) ? null : VoucherCodeBox.Text.Trim(),
                LongTermMonths = _longTermMonths,
                PriceUnit = _longTermMonths.HasValue ? "Month" : "Day",
                ExpectedTotal = _lastGrossAmount
            });

            if (string.Equals(depositMethod, "VNPay", StringComparison.OrdinalIgnoreCase))
            {
                var paymentPage = new VNPaySimulationPage(id);

                if (NavigationService != null)
                {
                    NavigationService.Navigate(paymentPage);
                    return;
                }

                if (Window.GetWindow(this)?.DataContext is MainViewModel mainVm)
                {
                    mainVm.CurrentPage = paymentPage;
                    return;
                }

                MessageText.Text =
                    $"Đã tạo Booking #{id}, nhưng không tìm thấy vùng điều hướng để mở VNPay Demo. " +
                    "Không nhấn Đặt xe lần nữa; hãy vào Lịch sử đặt xe để thanh toán.";
                MessageBox.Show(MessageText.Text, "Booking đã được tạo",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                PlaceButton.IsEnabled = false;
                return;
            }

            MessageText.Text =
                $"Đặt xe thành công! Mã Booking: #{id}. Đơn đang ở trạng thái PendingDeposit (chờ nhân viên xác nhận thu cọc tiền mặt).";
            MessageBox.Show(MessageText.Text, "Đặt xe thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            PlaceButton.Content = "Đã gửi yêu cầu";
            PlaceButton.IsEnabled = false;
            if (Window.GetWindow(this)?.DataContext is MainViewModel bookingNavigation)
                bookingNavigation.CurrentPage = new MyBookingsPage();
        }
        catch (Exception ex)
        {
            MessageText.Text = "Không thể tạo đơn: " + ex.Message;
            MessageBox.Show(MessageText.Text, "Đặt xe thất bại", MessageBoxButton.OK, MessageBoxImage.Warning);
            PlaceButton.IsEnabled = true;
        }
        finally
        {
            _placing = false;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this)?.DataContext is MainViewModel mainVm)
        {
            mainVm.CurrentPage = _returnPage ?? new CarSearchPage(_rentalMode,
                StartDate.SelectedDate, EndDate.SelectedDate);
            return;
        }
        if (NavigationService?.CanGoBack == true) NavigationService.GoBack();
    }

    private static bool TryDateTime(DateTime? date, string? time, out DateTime value)
    {
        value = default;
        if (!date.HasValue || !TimeSpan.TryParse(time, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < TimeSpan.Zero || parsed >= TimeSpan.FromDays(1))
            return false;
        value = date.Value.Date.Add(parsed);
        return true;
    }
}
