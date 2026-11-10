using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using CRMS.Models;
using CRMS.Services;
using CRMS.ViewModels;
using CRMS.Views.Customer;
using CRMS.Views.Guest;
using CRMS.Services.Payments;

internal static class TripNavigationChecks
{
    public static void Run()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                DispatcherOperation? startup = null;
                void CaptureStartup(object? sender, DispatcherHookEventArgs args) => startup = args.Operation;
                Dispatcher.CurrentDispatcher.Hooks.OperationPosted += CaptureStartup;
                var app = new CRMS.App();
                Dispatcher.CurrentDispatcher.Hooks.OperationPosted -= CaptureStartup;
                startup?.Abort();
                app.InitializeComponent();
                CurrentUserService.Instance.SetUser(new User { UserId = -1, UserRole = "Customer", FullName = "Navigation fixture" });
                var window = new CRMS.MainWindow();
                app.MainWindow = window;
                var main = (MainViewModel)window.DataContext;
                var root = (FrameworkElement)window.Content;
                void Layout()
                {
                    root.Measure(new Size(1100, 760)); root.Arrange(new Rect(0, 0, 1100, 760)); root.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                }
                for (var pass = 0; pass < 2; pass++)
                {
                    main.NavigateCommand.Execute("MyBookings");
                    Layout();
                    if (main.CurrentPage is not MyBookingsPage page) throw new Exception("Customer trip page did not open.");
                    if (pass == 1)
                    {
                        ((ItemsControl)page.FindName("BookingsList")).ItemsSource = new[]
                        {
                            new CRMS.Dtos.CustomerTrip("QA-EXPIRED", "Fixture", DateTime.UtcNow, DateTime.UtcNow.AddDays(1),
                                4000000, "PendingDeposit", DepositDeadlineUtc: DateTime.UtcNow.AddDays(-1))
                        };
                        ((UIElement)page.FindName("StatePanel")).Visibility = Visibility.Collapsed;
                        Layout();
                    }
                    var button = (Button)page.FindName("NewTripButton");
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Layout();
                    if (main.CurrentPage is not HomePage || !main.IsCustomerExplore || main.IsCustomerBookings)
                        throw new Exception("New-trip button did not navigate home and update the active navigation item.");
                    Console.WriteLine($"PASS: actual Frame-hosted NewTripButton navigates home from {(pass == 0 ? "empty trips" : "expired sample trips")}");
                }
                var service = new PaymentFixture();
                using var paymentVm = new DemoPaymentViewModel(11, service, true);
                string notice = "", noticeTitle = "";
                var noticeIcon = MessageBoxImage.None;
                var paymentWindow = new DemoPaymentWindow(11, paymentVm, (text, title, icon) => { notice = text; noticeTitle = title; noticeIcon = icon; });
                var checkButton = (Button)paymentWindow.FindName("CheckStatusButton");
                void CheckPayment()
                {
                    var content = (FrameworkElement)paymentWindow.Content;
                    content.Measure(new Size(924, 722)); content.Arrange(new Rect(0, 0, 924, 722)); content.UpdateLayout();
                    if (!ReferenceEquals(checkButton.Command, paymentVm.RefreshCommand)) throw new Exception("Status button command binding is missing.");
                    ((IInvokeProvider)new ButtonAutomationPeer(checkButton).GetPattern(PatternInterface.Invoke)).Invoke();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                }
                CheckPayment();
                if (!notice.Contains("Chưa có xác nhận")) throw new Exception("Pending status check gave no visible result.");
                Console.WriteLine("PASS: actual CheckStatusButton reports a pending payment explicitly");
                service.Session = service.Session with { BookingStatus = "Confirmed", PaymentStatus = "Success", ActualReceived = 1000, ConfirmedBooking = true };
                CheckPayment();
                if (noticeTitle != "Đặt cọc demo thành công" || !notice.Contains("1.000đ") || !paymentVm.IsSuccessful || !checkButton.IsEnabled)
                    throw new Exception("Staff-confirmed payment did not show success after the actual status button click.");
                Console.WriteLine("PASS: actual CheckStatusButton reports demo deposit success after Staff confirmation");
                service.Fail = true;
                CheckPayment();
                if (noticeIcon != MessageBoxImage.Warning || noticeTitle == "Đặt cọc demo thành công" || !notice.Contains("Chưa thể cập nhật"))
                    throw new Exception("Failed status read showed a stale successful result.");
                Console.WriteLine("PASS: actual CheckStatusButton reports connection failure rather than cached success");
                paymentWindow.Close();
                CurrentUserService.Instance.Logout();
                Console.WriteLine("SUCCESS: 5 customer navigation/status-button checks passed; no database writes or booking creation.");
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    private sealed class PaymentFixture : IDemoPaymentService
    {
        public bool Fail;
        public DemoPaymentSession Session = new(11, "TEST-STATUS", "PendingDeposit", 4000000, 2000000, 1000, 0,
            "DEMOSTATUS", "970436", "TEST BANK", "0000000001", "TEST RECEIVER", "Pending", false, DateTime.UtcNow.AddHours(1), null);
        public Task<DemoPaymentSession> GetSessionAsync(long id, CancellationToken ct = default)
            => Fail ? Task.FromException<DemoPaymentSession>(new InvalidOperationException("Fixture connection error")) : Task.FromResult(Session);
        public Task<DemoPaymentReceipt> ConfirmReceivedAsync(long id, decimal amount, string reference, Guid key) => throw new NotSupportedException();
    }
}
