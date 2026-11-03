using System.Windows;
using System.Windows.Controls;
using CRMS.Data;
using CRMS.Services;
using CRMS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Views.Customer;

public partial class MyBookingsPage : Page
{
    private CancellationTokenSource? _load;

    public MyBookingsPage() => InitializeComponent();

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _load?.Cancel();
        using var request = new CancellationTokenSource();
        _load = request;
        var userId = CurrentUserService.Instance.UserId;
        BookingsList.ItemsSource = null;
        StatePanel.Visibility = Visibility.Visible;
        StateTitle.Text = "Đang tải chuyến thuê…";
        StateDescription.Text = "Vui lòng đợi trong giây lát.";
        RetryButton.Visibility = Visibility.Collapsed;
        try
        {
            if (!CurrentUserService.Instance.IsCustomer || !userId.HasValue) return;
            using var db = CrmsContextFactory.Create();
            var bookings = await db.Bookings.AsNoTracking()
                .Where(b => b.CustomerId == userId.Value)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new TripRow(b.BookingCode, b.Vehicle.Brand + " " + b.Vehicle.Model,
                    b.StartAt, b.EndAt, b.TotalAmount, b.BookingStatus))
                .ToListAsync(request.Token);
            if (request.IsCancellationRequested || CurrentUserService.Instance.UserId != userId) return;
            BookingsList.ItemsSource = bookings;
            StatePanel.Visibility = bookings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StateTitle.Text = "Bạn chưa có chuyến thuê nào";
            StateDescription.Text = "Chọn “Tìm chuyến đi mới” để khám phá chiếc xe phù hợp với bạn.";
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (request.IsCancellationRequested || CurrentUserService.Instance.UserId != userId) return;
            StateTitle.Text = "Chưa thể tải chuyến thuê";
            StateDescription.Text = "Vui lòng kiểm tra kết nối và thử lại.";
            RetryButton.Visibility = Visibility.Visible;
        }
        finally { if (ReferenceEquals(_load, request)) _load = null; }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e) => _load?.Cancel();

    private void Payment_Click(object sender, RoutedEventArgs e)
    {
        if (!CurrentUserService.Instance.IsCustomer) return;
        if (sender is Button { Tag: string code } && Window.GetWindow(this)?.DataContext is MainViewModel main)
        {
            var page = new PaymentSummaryPage();
            if (page.DataContext is PaymentWorkspaceViewModel payment)
            {
                payment.BookingLookup = code;
                main.CurrentPage = page;
                payment.CalculateBalanceCommand.Execute(null);
            }
        }
    }

    public sealed record TripRow(string BookingCode, string VehicleName, DateTime StartAt,
        DateTime EndAt, decimal TotalAmount, string Status)
    {
        public string StatusLabel => Status switch
        {
            "PendingDeposit" => "Chờ đặt cọc", "Confirmed" => "Đã xác nhận", "InUse" => "Đang thuê",
            "Returned" => "Đã trả xe", "Completed" => "Hoàn tất", "Cancelled" => "Đã hủy",
            "Expired" => "Hết hạn", _ => Status
        };
    }
}
