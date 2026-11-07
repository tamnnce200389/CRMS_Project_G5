using CRMS.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CRMS.Views.Staff;

public partial class BookingQueuePage : Page
{
    private readonly BookingService _service = new();
    private bool _initialized;

    public BookingQueuePage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (!CurrentUserService.Instance.IsStaff && !CurrentUserService.Instance.IsAdmin)
            {
                StatusText.Text = "Chỉ Staff/Admin được truy cập danh sách đơn toàn hệ thống.";
                return;
            }
            _initialized = true;
            await LoadDriversAsync();
            await RefreshAsync();
        };
    }

    private string SelectedStatus => (StatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tất cả";

    private async Task RefreshAsync()
    {
        if (!CurrentUserService.Instance.IsStaff && !CurrentUserService.Instance.IsAdmin) return;
        try
        {
            var items = await _service.GetForStaffAsync(SelectedStatus);
            BookingsGrid.ItemsSource = items;
            StatusText.Text = $"Hiển thị {items.Count} đơn thuê.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Không tải được danh sách đơn.";
            MessageBox.Show(ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadDriversAsync()
    {
        try
        {
            DriversCombo.ItemsSource = await _service.GetActiveDriversAsync();
            if (DriversCombo.Items.Count > 0)
                DriversCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Không tải được danh sách tài xế.";
            MessageBox.Show(ex.Message, "Lỗi tải tài xế", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ConfirmCashDeposit_Click(object sender, RoutedEventArgs e)
    {
        if (!CurrentUserService.Instance.IsStaff && !CurrentUserService.Instance.IsAdmin)
        {
            MessageBox.Show("Chỉ Staff/Admin mới được xác nhận tiền cọc.");
            return;
        }

        if (BookingsGrid.SelectedItem is not BookingListItem selected)
        {
            MessageBox.Show("Hãy chọn một Booking trong danh sách.");
            return;
        }

        if (!selected.CanConfirmCashDeposit)
        {
            MessageBox.Show(
                "Chỉ Booking chọn phương thức Tiền mặt (Cash) và đang ở trạng thái PendingDeposit mới xác nhận tại đây.",
                "Không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Xác nhận đã thu đủ tiền cọc {selected.DepositAmount:N0} đ (Tiền mặt) cho đơn {selected.BookingCode}?",
            "Xác nhận thu cọc tiền mặt", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var actorId = CurrentUserService.Instance.UserId
                ?? throw new InvalidOperationException("Không xác định được tài khoản Staff/Admin hiện tại.");

            await _service.ConfirmCashDepositAsync(selected.BookingId, actorId);
            await RefreshAsync();
            MessageBox.Show(
                $"Đã xác nhận tiền cọc cho đơn {selected.BookingCode}. Trạng thái đơn đã chuyển sang Confirmed.",
                "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Không thể xác nhận cọc",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void AssignDriver_Click(object sender, RoutedEventArgs e)
    {
        if (!CurrentUserService.Instance.IsStaff && !CurrentUserService.Instance.IsAdmin)
        {
            MessageBox.Show("Chỉ Staff/Admin mới được phân công tài xế.");
            return;
        }

        if (BookingsGrid.SelectedItem is not BookingListItem selected)
        {
            MessageBox.Show("Hãy chọn một Booking trong danh sách.");
            return;
        }

        if (DriversCombo.SelectedValue is not long driverId)
        {
            MessageBox.Show("Hiện chưa có tài xế đang hoạt động để phân công.");
            return;
        }

        try
        {
            var actorId = CurrentUserService.Instance.UserId;
            if (!actorId.HasValue)
                throw new InvalidOperationException("Không xác định được tài khoản Staff/Admin hiện tại.");

            await _service.AssignDriverAsync(selected.BookingId, driverId, actorId.Value);
            await RefreshAsync();
            MessageBox.Show($"Đã phân công tài xế cho Booking {selected.BookingCode}.",
                "Phân công thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Không thể phân công tài xế",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void CancelBooking_Click(object sender, RoutedEventArgs e)
    {
        if (!CurrentUserService.Instance.IsStaff && !CurrentUserService.Instance.IsAdmin)
        {
            MessageBox.Show("Chỉ Staff/Admin mới được hủy đơn tại màn hình này.");
            return;
        }

        if (BookingsGrid.SelectedItem is not BookingListItem selected)
        {
            MessageBox.Show("Hãy chọn một Booking cần hủy.");
            return;
        }

        if (!selected.CanCancel)
        {
            MessageBox.Show("Chỉ có thể hủy đơn ở trạng thái PendingDeposit hoặc Confirmed (chưa bàn giao xe).");
            return;
        }

        var answer = MessageBox.Show(
            $"Xác nhận hủy đơn {selected.BookingCode}? Nếu đơn đã thu cọc, hệ thống sẽ tạo bản ghi chờ hoàn tiền (Chờ hoàn cọc).",
            "Xác nhận hủy đơn", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            var actorId = CurrentUserService.Instance.UserId
                ?? throw new InvalidOperationException("Không xác định được tài khoản Staff/Admin hiện tại.");

            await _service.CancelAsync(selected.BookingId, actorId, "Staff/Admin hủy đơn từ danh sách điều phối");
            await RefreshAsync();
            MessageBox.Show($"Đã hủy đơn {selected.BookingCode}.", "Đã hủy", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Không thể hủy đơn", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ConfirmRefund_Click(object sender, RoutedEventArgs e)
    {
        if (!CurrentUserService.Instance.IsStaff && !CurrentUserService.Instance.IsAdmin)
        {
            MessageBox.Show("Chỉ Staff/Admin mới được xác nhận hoàn tiền cọc.");
            return;
        }

        if (BookingsGrid.SelectedItem is not BookingListItem selected)
        {
            MessageBox.Show("Hãy chọn một Booking đang ở trạng thái Chờ hoàn cọc trong danh sách.");
            return;
        }

        if (!selected.CanConfirmRefund)
        {
            MessageBox.Show(
                "Chỉ những đơn đã hủy có trạng thái thanh toán 'Chờ hoàn cọc' mới cần xác nhận hoàn cọc.",
                "Không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            $"Xác nhận đã hoàn trả tiền cọc {selected.DepositAmount:N0} đ cho khách hàng của đơn {selected.BookingCode}?",
            "Xác nhận hoàn cọc", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            var actorId = CurrentUserService.Instance.UserId
                ?? throw new InvalidOperationException("Không xác định được tài khoản Staff/Admin hiện tại.");

            await _service.ConfirmRefundAsync(selected.BookingId, actorId);
            await RefreshAsync();
            MessageBox.Show(
                $"Đã xác nhận hoàn cọc thành công cho đơn {selected.BookingCode}. Trạng thái thanh toán đã chuyển sang 'Đã hoàn cọc'.",
                "Hoàn cọc thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Không thể xác nhận hoàn cọc", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initialized) await RefreshAsync();
    }
}
