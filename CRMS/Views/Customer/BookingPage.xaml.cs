using CRMS.Data;
using CRMS.Services;
using CRMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CRMS.Views.Customer;

public partial class BookingPage : Page
{
    private readonly BookingService _bookingService = new();
    private bool _placing;
    private bool _initializing;
    private Guid _requestKey = Guid.NewGuid();
    private string? _previewedVoucherCode;
    private bool _initialized;

    public BookingPage()
    {
        InitializeComponent();
        _initializing = true;
        Loaded += async (_, _) =>
        {
            try
            {
                var voucherSupported = await _bookingService.SupportsBookingVoucherAsync();
                VoucherSection.Visibility = voucherSupported ? Visibility.Visible : Visibility.Collapsed;
                VoucherUnavailableText.Visibility = voucherSupported ? Visibility.Collapsed : Visibility.Visible;
            }
            catch
            {
                // Nếu không đọc được metadata Stored Procedure, ẩn Voucher để tránh gọi procedure sai tham số.
                VoucherSection.Visibility = Visibility.Collapsed;
                VoucherUnavailableText.Visibility = Visibility.Visible;
            }

            await LoadVehiclesAsync();
            _initializing = false;
        };
    }

    private async Task LoadVehiclesAsync()
    {
        try
        {
            await using var db = CrmsContextFactory.Create();
            var vehicles = await db.Vehicles.AsNoTracking()
                .Where(v => v.OperationalStatus == "Active" && v.Category.IsActive)
                .OrderBy(v => v.Brand).ThenBy(v => v.Model)
                .Select(v => new VehicleChoice
                {
                    VehicleId = v.VehicleId,
                    DisplayName = v.Brand + " " + v.Model + " — " + v.LicensePlate + " — " + v.DailyRate + " đ/ngày"
                }).ToListAsync();

            VehicleBox.ItemsSource = vehicles;
            if (vehicles.Count > 0) VehicleBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageText.Text = "Không tải được danh sách xe: " + ex.Message;
        }
    }

    private async void LoadVouchers_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!TryGetBookingInput(out var vehicleId, out var rentalType, out var startUtc, out var endUtc))
                return;

            var customerId = GetCustomerId();
            var vouchers = await _bookingService.GetAvailableVouchersAsync(
                customerId, vehicleId, rentalType, startUtc, endUtc);

            VoucherBox.ItemsSource = vouchers;
            VoucherBox.SelectedIndex = -1;

            VoucherMessageText.Text = vouchers.Count == 0
                ? "Hiện không có Voucher phù hợp với giá trị đơn hàng hoặc hạn mức sử dụng."
                : $"Tìm thấy {vouchers.Count} Voucher có thể sử dụng. Chọn một mã hoặc nhập mã riêng.";
            VoucherMessageText.Foreground = System.Windows.Media.Brushes.DarkSlateGray;
        }
        catch (Exception ex)
        {
            VoucherMessageText.Text = "Không tải được Voucher: " + ex.Message;
            VoucherMessageText.Foreground = System.Windows.Media.Brushes.Firebrick;
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
        ClearVoucherPreview("Mã Voucher đã thay đổi. Hãy kiểm tra lại mã trước khi đặt xe.");
    }

    private void BookingInputs_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        VoucherBox.ItemsSource = null;
        ClearVoucherPreview("Thông tin thuê xe đã thay đổi. Hãy tải lại và kiểm tra Voucher.");
    }

    private void BookingInputs_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing) return;
        VoucherBox.ItemsSource = null;
        ClearVoucherPreview("Ngày giờ thuê đã thay đổi. Hãy tải lại và kiểm tra Voucher.");
    }

    private async void ApplyVoucher_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!TryGetBookingInput(out var vehicleId, out var rentalType, out var startUtc, out var endUtc))
                return;

            var code = VoucherCodeBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                VoucherMessageText.Text = "Vui lòng chọn hoặc nhập mã Voucher.";
                return;
            }

            ApplyVoucherButton.IsEnabled = false;
            var preview = await _bookingService.PreviewVoucherAsync(
                GetCustomerId(), vehicleId, rentalType, startUtc, endUtc, code);

            _previewedVoucherCode = preview.VoucherCode;
            GrossAmountText.Text = FormatMoney(preview.GrossAmount);
            DiscountAmountText.Text = "− " + FormatMoney(preview.DiscountAmount);
            TotalAmountText.Text = FormatMoney(preview.TotalAmount);
            DepositAmountText.Text = FormatMoney(preview.DepositAmount);
            VoucherSummaryPanel.Visibility = Visibility.Visible;

            VoucherMessageText.Text = $"Voucher {preview.VoucherCode} hợp lệ. Số tiền giảm đã được tính thử; SQL sẽ kiểm tra lại khi tạo Booking.";
            VoucherMessageText.Foreground = System.Windows.Media.Brushes.ForestGreen;
        }
        catch (Exception ex)
        {
            _previewedVoucherCode = null;
            VoucherSummaryPanel.Visibility = Visibility.Collapsed;
            VoucherMessageText.Text = "Không thể áp dụng Voucher: " + ex.Message;
            VoucherMessageText.Foreground = System.Windows.Media.Brushes.Firebrick;
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
        VoucherBox.ItemsSource = null;
        ClearVoucherPreview("Đã bỏ Voucher. Booking sẽ được tính theo giá gốc.");
        VoucherMessageText.Foreground = System.Windows.Media.Brushes.DarkSlateGray;
    }

    private void ClearVoucherPreview(string message)
    {
        _previewedVoucherCode = null;
        VoucherSummaryPanel.Visibility = Visibility.Collapsed;
        VoucherMessageText.Text = message;
        VoucherMessageText.Foreground = System.Windows.Media.Brushes.DarkSlateGray;
    }

    private async void PlaceBooking_Click(object sender, RoutedEventArgs e)
    {
        if (_placing) return;

        if (!CurrentUserService.Instance.IsCustomer || !CurrentUserService.Instance.UserId.HasValue)
        {
            MessageBox.Show("Vui lòng đăng nhập bằng tài khoản Customer trước khi đặt xe.");
            return;
        }

        if (!TryGetBookingInput(out var vehicleId, out var rentalType, out var startUtc, out var endUtc))
            return;

        var voucherCode = string.IsNullOrWhiteSpace(VoucherCodeBox.Text)
            ? null
            : VoucherCodeBox.Text.Trim();

        if (voucherCode != null && !string.Equals(voucherCode, _previewedVoucherCode, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Bạn đã nhập/chọn Voucher nhưng chưa kiểm tra mã. Hãy nhấn 'Kiểm tra và áp dụng thử' trước khi xác nhận.",
                "Chưa kiểm tra Voucher", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _placing = true;
        PlaceButton.IsEnabled = false;
        try
        {
            var depositMethod = (DepositMethodBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "VNPay";
            var id = await _bookingService.CreateAsync(new CreateBookingRequest
            {
                CustomerId = CurrentUserService.Instance.UserId.Value,
                VehicleId = vehicleId,
                RentalType = rentalType,
                DepositMethod = depositMethod,
                StartAtUtc = startUtc,
                EndAtUtc = endUtc,
                RequestKey = _requestKey,
                VoucherCode = voucherCode
            });

            // VNPay trong đồ án là chế độ mô phỏng, không gọi cổng thanh toán thật.
            // Ưu tiên điều hướng qua MainViewModel của cửa sổ chính; nếu BookingPage
            // đang được host trong Frame riêng thì dùng NavigationService dự phòng.
            if (string.Equals(depositMethod, "VNPay", StringComparison.OrdinalIgnoreCase))
            {
                var paymentPage = new VNPaySimulationPage(id);
                var hostWindow = Window.GetWindow(this);

                if (hostWindow?.DataContext is MainViewModel mainVm)
                {
                    mainVm.CurrentPage = paymentPage;
                }
                else if (Application.Current?.MainWindow?.DataContext is MainViewModel appVm)
                {
                    appVm.CurrentPage = paymentPage;
                }
                else if (NavigationService != null)
                {
                    NavigationService.Navigate(paymentPage);
                }
                else
                {
                    MessageText.Text = $"Đã tạo Booking #{id}, nhưng không tìm thấy vùng điều hướng để mở VNPay Demo. " +
                                       "Bạn có thể vào My Bookings và chọn Thanh toán.";
                    MessageBox.Show(MessageText.Text, "Booking đã được tạo",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                PlaceButton.IsEnabled = false;
                return;
            }

            var successMessage = $"Đã tạo Booking #{id}. Trạng thái: PendingDeposit. " +
                                 "Vui lòng chờ nhân viên xác nhận tiền cọc nếu chọn tiền mặt.";
            MessageText.Text = successMessage;
            MessageBox.Show(successMessage, "Đặt xe thành công",
                MessageBoxButton.OK, MessageBoxImage.Information);

            // Giữ nguyên RequestKey để retry không tạo Booking trùng.
            PlaceButton.IsEnabled = false;
        }
        catch (Exception ex)
        {
            MessageText.Text = "Không thể đặt xe: " + ex.Message;
            MessageBox.Show(MessageText.Text, "Đặt xe thất bại",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _placing = false;
            if (string.IsNullOrWhiteSpace(MessageText.Text)
                || !MessageText.Text.StartsWith("Đã tạo Booking"))
                PlaceButton.IsEnabled = true;
        }
    }

    private bool TryGetBookingInput(
        out long vehicleId, out string rentalType,
        out DateTime startUtc, out DateTime endUtc)
    {
        vehicleId = 0;
        rentalType = (RentalTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "SelfDrive";
        startUtc = default;
        endUtc = default;

        if (VehicleBox.SelectedValue is not long selectedVehicleId)
        {
            MessageBox.Show("Vui lòng chọn xe đang hoạt động.");
            return false;
        }

        if (!TryDateTime(StartDate.SelectedDate, StartTime.Text, out var startLocal)
            || !TryDateTime(EndDate.SelectedDate, EndTime.Text, out var endLocal)
            || endLocal <= startLocal)
        {
            MessageBox.Show("Ngày giờ nhận/trả xe không hợp lệ. Hãy dùng định dạng giờ HH:mm.");
            return false;
        }

        vehicleId = selectedVehicleId;
        startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
        endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();

        if (startUtc <= DateTime.UtcNow)
        {
            MessageBox.Show("Thời gian nhận xe phải nằm trong tương lai.");
            return false;
        }

        return true;
    }

    private static long GetCustomerId()
    {
        if (!CurrentUserService.Instance.IsCustomer || !CurrentUserService.Instance.UserId.HasValue)
            throw new InvalidOperationException("Vui lòng đăng nhập bằng tài khoản Customer.");
        return CurrentUserService.Instance.UserId.Value;
    }

    private static string FormatMoney(decimal amount) => amount.ToString("N0") + " đ";

    private static bool TryDateTime(DateTime? date, string? time, out DateTime value)
    {
        value = default;
        if (!date.HasValue || !TimeSpan.TryParse(time, out var parsed)
            || parsed < TimeSpan.Zero || parsed >= TimeSpan.FromDays(1))
            return false;

        value = date.Value.Date.Add(parsed);
        return true;
    }

    private sealed class VehicleChoice
    {
        public long VehicleId { get; init; }
        public string DisplayName { get; init; } = "";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        throw new NotImplementedException();
    }

    private async void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initialized) await RefreshAsync();
    }
}
