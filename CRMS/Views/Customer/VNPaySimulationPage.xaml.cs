using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CRMS.Services;
using CRMS.ViewModels;

namespace CRMS.Views.Customer;

public partial class VNPaySimulationPage : Page
{
    private readonly long _bookingId;
    private readonly DemoPaymentService _paymentService = new();
    private bool _processing;

    public VNPaySimulationPage(long bookingId)
    {
        InitializeComponent();
        _bookingId = bookingId;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var user = CurrentUserService.Instance;
            if (!user.IsCustomer || !user.UserId.HasValue)
                throw new InvalidOperationException("Vui lòng đăng nhập bằng tài khoản Customer.");

            var info = await _paymentService.GetInfoAsync(_bookingId, user.UserId.Value);
            BookingCodeText.Text = info.BookingCode;
            VehicleText.Text = info.VehicleName;
            AmountText.Text = info.DepositAmount.ToString("N0") + " đ";
            DeadlineText.Text = info.DepositDeadline.ToString("dd/MM/yyyy HH:mm");

            if (info.BookingStatus == "Confirmed" && info.PaymentStatus == "Success")
            {
                MessageText.Text = "Booking này đã được thanh toán thành công trước đó.";
                SuccessButton.IsEnabled = false;
                FailedButton.IsEnabled = false;
                CancelButton.IsEnabled = false;
            }
            else if (info.BookingStatus != "PendingDeposit")
            {
                MessageText.Text = "Booking hiện không ở trạng thái chờ thanh toán: " + info.BookingStatus;
                SetActionsEnabled(false);
            }
        }
        catch (Exception ex)
        {
            MessageText.Text = ex.Message;
            SetActionsEnabled(false);
        }
    }

    private async void Success_Click(object sender, RoutedEventArgs e) =>
        await ProcessAsync("Success");

    private async void Failed_Click(object sender, RoutedEventArgs e) =>
        await ProcessAsync("Failed");

    private async void Cancel_Click(object sender, RoutedEventArgs e) =>
        await ProcessAsync("Cancelled");

    private async Task ProcessAsync(string outcome)
    {
        if (_processing) return;
        var userId = CurrentUserService.Instance.UserId;
        if (!userId.HasValue) return;

        _processing = true;
        SetActionsEnabled(false);
        try
        {
            var result = await _paymentService.ProcessAsync(_bookingId, userId.Value, outcome);
            MessageText.Text = result;

            if (outcome == "Success")
            {
                MessageBox.Show(
                    "Thanh toán DEMO thành công. Không có tiền thật được chuyển.\nBooking đã chuyển sang Confirmed.",
                    "VNPay Demo", MessageBoxButton.OK, MessageBoxImage.Information);
                BackToBookings();
            }
            else
            {
                MessageBox.Show(result, "VNPay Demo",
                    MessageBoxButton.OK,
                    outcome == "Failed" ? MessageBoxImage.Warning : MessageBoxImage.Information);
                SetActionsEnabled(true);
            }
        }
        catch (Exception ex)
        {
            MessageText.Text = "Không thể xử lý giao dịch: " + ex.Message;
            MessageBox.Show(MessageText.Text, "VNPay Demo",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            SetActionsEnabled(true);
        }
        finally
        {
            _processing = false;
        }
    }

    private void SetActionsEnabled(bool enabled)
    {
        SuccessButton.IsEnabled = enabled;
        FailedButton.IsEnabled = enabled;
        CancelButton.IsEnabled = enabled;
    }

    private void Back_Click(object sender, RoutedEventArgs e) => BackToBookings();

    private void BackToBookings()
    {
        if (Window.GetWindow(this)?.DataContext is MainViewModel vm)
            vm.CurrentPage = new MyBookingsPage();
        else if (NavigationService?.CanGoBack == true)
            NavigationService.GoBack();
    }
}
