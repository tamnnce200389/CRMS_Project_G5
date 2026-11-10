using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.Diagnostics;
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
                            new TripItemViewModel(new CRMS.Dtos.CustomerTrip("QA-EXPIRED", "Fixture", DateTime.UtcNow, DateTime.UtcNow.AddDays(1),
                                4000000, "PendingDeposit", DepositDeadlineUtc: DateTime.UtcNow.AddDays(-1)))
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
                CheckTripBindings(main, Layout);
                CheckDashboardBindings(main, Layout);
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
                Console.WriteLine("SUCCESS: customer navigation, trip binding and status-button checks passed; no database writes or booking creation.");
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    private static void CheckDashboardBindings(MainViewModel main, Action layout)
    {
        var session = new DashboardSession();
        var admin = new AdminDashboardViewModel((_, _) => Task.FromResult(new DashboardSnapshot { TotalBookings = 1234, DepositCollected = 2000 }), session);
        var staff = new StaffDashboardViewModel((_, _) => Task.FromResult(new StaffDashboardSnapshot { TotalBookings = 1234 }), session);
        admin.LoadAsync().GetAwaiter().GetResult();
        staff.LoadAsync().GetAwaiter().GetResult();
        foreach (var (page, vm, command) in new (Page, object, System.Windows.Input.ICommand)[]
        {
            (new CRMS.Views.Admin.DashboardPage(), admin, admin.RefreshCommand),
            (new CRMS.Views.Staff.StaffDashboardPage(), staff, staff.RefreshCommand)
        })
        {
            page.DataContext = vm;
            main.CurrentPage = page;
            layout();
            var count = Descendants<TextBlock>(page).First(t => System.Windows.Data.BindingOperations.GetBinding(t, TextBlock.TextProperty)?.Path.Path == "Snapshot.TotalBookings");
            if (string.Concat(count.Text.Where(char.IsDigit)) != "1234" ||
                !Descendants<Button>(page).Any(b => ReferenceEquals(b.Command, command)) ||
                Descendants<DatePicker>(page).Count(p => p.SelectedDate.HasValue) != 2)
                throw new Exception("Dashboard inherited snapshot/date/command bindings failed: " + page.GetType().Name);
            Console.WriteLine("PASS: actual " + page.GetType().Name + " retains inherited snapshot, dates and refresh-command bindings");
        }
    }

    private sealed class DashboardSession : IUserSession
    {
        public long? UserId => -1;
        public string Role => "Admin";
        public long SessionVersion => 0;
    }

    private static void CheckTripBindings(MainViewModel main, Action layout)
    {
        using var errors = new System.IO.StringWriter();
        using var trace = new TextWriterTraceListener(errors);
        var source = PresentationTraceSources.DataBindingSource;
        var previousLevel = source.Switch.Level;
        source.Switch.Level = SourceLevels.Warning;
        source.Listeners.Add(trace);
        try
        {
            main.NavigateCommand.Execute("MyBookings");
            layout();
            var page = (MyBookingsPage)main.CurrentPage!;
            var list = (ItemsControl)page.FindName("BookingsList");
            ((UIElement)page.FindName("StatePanel")).Visibility = Visibility.Collapsed;
            foreach (var (status, label) in new[]
            {
                ("PendingDeposit", "Thanh toán"), ("Confirmed", "Xem biên nhận demo"),
                ("Expired", "Xem trạng thái demo"), ("Cancelled", "Xem trạng thái demo"),
                ("InUse", "Xem biên nhận demo"), ("Returned", "Xem biên nhận demo"),
                ("Completed", "Xem biên nhận demo")
            })
            {
                var row = new TripItemViewModel(new CRMS.Dtos.CustomerTrip("BINDING-TRIP", "Toyota Vios", DateTime.UtcNow,
                    DateTime.UtcNow.AddDays(1), 700000, status, IsDemo: true, DepositDeadlineUtc: DateTime.UtcNow.AddHours(1)));
                list.ItemsSource = new[] { row };
                layout();
                var buttons = Descendants<Button>(list).ToArray();
                var qr = buttons.Single(b => b.Name == "TripPaymentButton");
                if (!Equals(qr.Content, label) || qr.Visibility != Visibility.Visible)
                    throw new Exception("Trip QR button changed for " + status);
                if (!Equals(buttons.Single(b => Equals(b.Content, "Xem thanh toán")).Tag, "BINDING-TRIP"))
                    throw new Exception("Trip payment lookup binding is broken.");
                if (!Descendants<TextBlock>(list).Any(t => t.Text == "Toyota Vios"))
                    throw new Exception("Trip vehicle name binding is broken.");
                Console.WriteLine("PASS: actual trip template preserves " + status + " button text, visibility, name and payment lookup");
            }
            var expired = new CRMS.Dtos.CustomerTrip("BINDING-EXPIRED", "Fixture", DateTime.UtcNow, DateTime.UtcNow.AddDays(1),
                700000, "PendingDeposit", IsDemo: true, DepositDeadlineUtc: DateTime.UtcNow.AddDays(-1));
            list.ItemsSource = new[] { new TripItemViewModel(expired) };
            layout();
            if (!Equals(Descendants<Button>(list).Single(b => b.Name == "TripPaymentButton").Content, "Xem trạng thái demo"))
                throw new Exception("Expired pending trip still invites payment.");
            Console.WriteLine("PASS: actual trip template keeps expired-deadline QR action");
            list.ItemsSource = new[] { new TripItemViewModel(expired with { IsDemo = false }) };
            layout();
            if (Descendants<Button>(list).Single(b => b.Name == "TripPaymentButton").Visibility != Visibility.Collapsed)
                throw new Exception("Non-demo trip exposes the QR demo action.");
            trace.Flush();
            if (errors.ToString().Length != 0) throw new Exception("Trip binding errors: " + errors);
            Console.WriteLine("PASS: actual trip template hides non-demo QR and emits no binding warnings/errors");
        }
        finally { source.Listeners.Remove(trace); source.Switch.Level = previousLevel; }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
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
