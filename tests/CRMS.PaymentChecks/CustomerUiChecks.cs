using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CRMS.Models;
using CRMS.Services;
using CRMS.ViewModels;
using CRMS.Views.Customer;
using CRMS.Views.Guest;

// Offline visual fixtures: no login, database writes, or customer data are required.
internal static class CustomerUiChecks
{
    public static void Run(string output)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                Directory.CreateDirectory(output);
                var app = new CRMS.App();
                app.InitializeComponent();
                var window = new CRMS.MainWindow();
                app.MainWindow = window;
                var main = (MainViewModel)window.DataContext;
                var root = (FrameworkElement)window.Content;

                void Check(bool condition, string name)
                {
                    if (!condition) throw new InvalidOperationException(name);
                    Console.WriteLine("PASS: " + name);
                }
                void Render(string name, int width, int height = 860)
                {
                    root.Measure(new Size(width, height));
                    root.Arrange(new Rect(0, 0, width, height));
                    root.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    root.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(output, name + ".png"));
                    encoder.Save(file);
                    Console.WriteLine("RENDER: " + name);
                }

                Check(main.IsGuest && main.CurrentPage is HomePage, "Guest starts on home");
                Render("guest-1400", 1400);
                Render("guest-1100", 1100, 680);
                var home = (HomeViewModel)((FrameworkElement)main.CurrentPage!).DataContext;
                home.SelectModeCommand.Execute("WithDriver");
                Check(home.IsWithDriver && !home.IsSelfDrive, "Driver tab selects the correct form");
                Render("driver-1100", 1100);
                home.SelectModeCommand.Execute("LongTerm");
                Check(home.IsLongTerm && !home.IsWithDriver, "Long-term tab selects the correct form");
                Render("long-term-1100", 1100, 680);
                home.SelectModeCommand.Execute("SelfDrive");

                CurrentUserService.Instance.SetUser(new User { UserId = -1, FullName = "Khách hàng minh họa", UserRole = "Customer" });
                main.RefreshSession();
                Check(main.IsCustomer && main.IsCustomerExplore, "Customer starts with explore navigation selected");
                Render("customer-1400", 1400);
                Render("customer-full", 1400, 1200);
                Render("customer-1100", 1100, 680);

                // Render the popup separately: it occupies its own native window when opened.
                var accountButton = (Button)window.FindName("AccountButton");
                var accountMenu = accountButton.ContextMenu;
                accountMenu.PlacementTarget = accountButton;
                accountMenu.Measure(new Size(272, double.PositiveInfinity));
                var menuHeight = (int)Math.Ceiling(accountMenu.DesiredSize.Height);
                accountMenu.Arrange(new Rect(0, 0, 272, menuHeight));
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                accountMenu.UpdateLayout();
                var menuBitmap = new RenderTargetBitmap(272, menuHeight, 96, 96, PixelFormats.Pbgra32);
                menuBitmap.Render(accountMenu);
                var menuEncoder = new PngBitmapEncoder();
                menuEncoder.Frames.Add(BitmapFrame.Create(menuBitmap));
                using (var menuFile = File.Create(Path.Combine(output, "account-menu.png"))) menuEncoder.Save(menuFile);

                main.NavigateCommand.Execute("Profile");
                Check(main.IsCustomerProfile && !main.IsCustomerExplore, "Profile navigation state");
                var profile = (ProfilePage)main.CurrentPage;
                foreach (var item in new[] { ("txtUserId", "DEMO"), ("txtFullName", "Khách hàng minh họa"), ("txtEmail", "demo@example.com"), ("txtPhone", ""), ("txtRole", "Khách hàng"), ("txtStatus", "Đang hoạt động") })
                    ((TextBox)profile.FindName(item.Item1)).Text = item.Item2;
                Render("profile-1100", 1100);

                main.NavigateCommand.Execute("MyBookings");
                Check(main.IsCustomerBookings, "Bookings navigation state");
                var trips = (MyBookingsPage)main.CurrentPage;
                ((TextBlock)trips.FindName("StateTitle")).Text = "Bạn chưa có chuyến thuê nào";
                ((TextBlock)trips.FindName("StateDescription")).Text = "Chọn “Tìm chuyến đi mới” để khám phá chiếc xe phù hợp với bạn.";
                ((Button)trips.FindName("RetryButton")).Visibility = Visibility.Collapsed;
                Render("trips-empty-1100", 1100);
                ((Border)trips.FindName("StatePanel")).Visibility = Visibility.Collapsed;
                ((ItemsControl)trips.FindName("BookingsList")).ItemsSource = new[] {
                    new CRMS.Dtos.CustomerTrip("DEMO-001", "Xe minh họa", new DateTime(2026,11,3,8,0,0), new DateTime(2026,11,4,8,0,0), 900000, "Confirmed", "SelfDrive", "Day", 1) };
                Render("trips-1100", 1100);

                main.NavigateCommand.Execute("PaymentSummary");
                Check(main.IsCustomerPayments, "Payment navigation state");
                Render("payment-1100", 1100);
                var payment = (PaymentWorkspaceViewModel)((FrameworkElement)main.CurrentPage!).DataContext;
                payment.BookingLookup = "DEMO-001";
                var summary = new CRMS.Services.Payments.PaymentSummary(-1, "DEMO-001", "Confirmed", 900000, 0, 450000, 0, 450000, 0, 0, false);
                typeof(PaymentWorkspaceViewModel).GetProperty("Summary")!.SetValue(payment, summary);
                Render("payment-summary-1100", 1100);

                CurrentUserService.Instance.Logout();
                main.RefreshSession();
                main.NavigateCommand.Execute("MyBookings");
                Check(main.IsGuest && main.CurrentPage is HomePage, "Logout returns home and guest cannot open customer trips");

                // Offline management shell previews use the same account menu with role-specific entries.
                CurrentUserService.Instance.SetUser(new User { UserId = -2, FullName = "Quản trị viên minh họa", UserRole = "Admin" });
                main.RefreshSession();
                Render("admin-1400", 1400);
                Render("admin-1100", 1100, 760);
                var managementButton = (Button)window.FindName("ManagementAccountButton");
                accountMenu.PlacementTarget = managementButton;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                accountMenu.Measure(new Size(272, double.PositiveInfinity));
                menuHeight = (int)Math.Ceiling(accountMenu.DesiredSize.Height);
                accountMenu.Arrange(new Rect(0, 0, 272, menuHeight));
                accountMenu.UpdateLayout();
                menuBitmap = new RenderTargetBitmap(272, menuHeight, 96, 96, PixelFormats.Pbgra32);
                menuBitmap.Render(accountMenu);
                menuEncoder = new PngBitmapEncoder();
                menuEncoder.Frames.Add(BitmapFrame.Create(menuBitmap));
                using (var menuFile = File.Create(Path.Combine(output, "admin-account-menu.png"))) menuEncoder.Save(menuFile);
                CurrentUserService.Instance.Logout();
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
