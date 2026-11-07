using CRMS.Services;
using CRMS.ViewModels;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CRMS.Views.Customer;

public partial class MyBookingsPage : Page
{
    private readonly BookingService _service = new();

    public MyBookingsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var user = CurrentUserService.Instance;
        if (!user.IsCustomer || !user.UserId.HasValue)
        {
            StatusText.Text = "Vui lòng đăng nhập bằng tài khoản Customer để xem đơn của bạn.";
            BookingsGrid.ItemsSource = null;
            return;
        }

        try
        {
            BookingsGrid.ItemsSource = await _service.GetForCustomerAsync(user.UserId.Value);
            StatusText.Text = "Dữ liệu đã được cập nhật.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Không tải được lịch sử đặt xe.";
            MessageBox.Show(ex.Message, "Lỗi tải đơn", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Home_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this)?.DataContext is MainViewModel vm
            && vm.NavigateCommand.CanExecute("Home"))
        {
            vm.NavigateCommand.Execute("Home");
            return;
        }

        MessageBox.Show("Không tìm thấy điều hướng trang chủ Customer. Vui lòng quay lại cửa sổ chính.",
            "Điều hướng", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void PayDeposit_Click(object sender, RoutedEventArgs e)
    {
        if (BookingsGrid.SelectedItem is not BookingListItem selected)
        {
            MessageBox.Show("Hãy chọn một Booking cần thanh toán.");
            return;
        }

        if (!selected.CanPayDeposit)
        {
            MessageBox.Show("Chỉ Booking VNPay đang ở trạng thái PendingDeposit mới có thể thanh toán.",
                "Không thể thanh toán", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (Window.GetWindow(this)?.DataContext is MainViewModel vm)
            vm.CurrentPage = new VNPaySimulationPage(selected.BookingId);
    }

    private async void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (BookingsGrid.SelectedItem is not BookingListItem selected)
        {
            MessageBox.Show("Hãy chọn một đơn cần hủy.");
            return;
        }
        if (!selected.CanCancel)
        {
            MessageBox.Show("Chỉ được yêu cầu hủy đơn PendingDeposit/Confirmed và chưa bàn giao xe.");
            return;
        }

        var answer = MessageBox.Show(
            $"Hủy đơn {selected.BookingCode}? Nếu đã thu cọc, việc hoàn tiền sẽ do bộ phận phụ trách xử lý.",
            "Xác nhận hủy", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            await _service.CancelAsync(selected.BookingId, CurrentUserService.Instance.UserId!.Value,
                "Customer yêu cầu hủy từ lịch sử đặt xe");
            await RefreshAsync();
            MessageBox.Show("Đã gửi yêu cầu hủy và cập nhật trạng thái theo quy tắc nghiệp vụ.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Không thể hủy đơn", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
