using CRMS.Services;
using CRMS.ViewModels;
using CRMS.Views.Admin;
using CRMS.Views.Customer;
using CRMS.Views.Guest;
using CRMS.Views.Staff;
namespace CRMS.Helpers;
// WPF view creation is kept outside ViewModels and domain services.
public sealed class PageNavigationService : INavigationService
{
    private readonly Dictionary<string, object> _pages = new();
    private object? _current;
    public event Action? Changed;
    public object? Current
    {
        get => _current; set
        {
            _current = value; CurrentRoute = value?.GetType().Name switch
            {
                "HomePage" => "Home",
                "CarSearchPage" => "CarSearch",
                "CarDetailsPage" => "CarDetails",
                "ProfilePage" => "Profile",
                "DashboardPage" or "StaffDashboardPage" => "Dashboard",
                "VehiclePage" or "StaffVehiclePage" => "Vehicles",
                "DriverPage" or "StaffDriverPage" => "Drivers",
                "BookingPage" => "Bookings",
                "ReportPage" => "Reports",
                "UserManagementPage" => "UserManagement",
                "MyBookingsPage" => "MyBookings",
                "PaymentSummaryPage" => "PaymentSummary",
                "AvailabilityPage" => "Availability",
                "MaintenancePage" => "Maintenance",
                _ => value?.GetType().Name ?? ""
            }; Changed?.Invoke();
        }
    }
    public string CurrentRoute { get; private set; } = "";
    public void Reset() { _pages.Clear(); Current = null; }
    public void Navigate(string route, object? argument = null)
    {
        bool admin = CurrentUserService.Instance.IsAdmin, staff = CurrentUserService.Instance.IsStaff, customer = CurrentUserService.Instance.IsCustomer;
        bool management = admin || staff, logged = CurrentUserService.Instance.IsLoggedIn;
        if (route is "Home" or "PaymentHome") route = management ? "Dashboard" : "Home";
        if ((route is "Dashboard" or "Vehicles" or "Drivers" or "Bookings" or "CashDeposit" or "OutstandingBalance" or "ManualRefund" or "Availability") && !management) return;
        if ((route is "UserManagement" or "Reports" or "Maintenance") && !admin) return;
        if ((route is "Profile" or "PaymentSummary") && !logged) return;
        if (route == "MyBookings" && !customer) return;
        if (route == "CarSearch" && argument is SearchNavigationRequest search) { Current = new CarSearchPage(search.Mode, search.Start, search.End, search.Context); return; }
        if (route == "Availability") { Current = new AvailabilityPage(argument as long?); return; }
        if (route == "Maintenance") { Current = new MaintenancePage(argument as long?); return; }
        if (route == "PaymentSummary" && argument is string code)
        {
            var page = new PaymentSummaryPage(() => Navigate("MyBookings")); if (page.DataContext is PaymentWorkspaceViewModel vm) { vm.BookingLookup = code; Current = page; vm.CalculateBalanceCommand.Execute(null); }
            return;
        }
        if (!_pages.TryGetValue(route, out var pageObject))
        {
            pageObject = route switch
            {
                "Home" => new HomePage(),
                "Profile" => new ProfilePage(),
                "Dashboard" => admin ? new DashboardPage() : new StaffDashboardPage(),
                "Vehicles" => admin ? new VehiclePage() : new StaffVehiclePage(),
                "Drivers" => admin ? new DriverPage() : new StaffDriverPage(),
                "Bookings" => new BookingPage(),
                "Reports" => new ReportPage(),
                "UserManagement" => new UserManagementPage(),
                "CashDeposit" => new CashDepositPage(),
                "OutstandingBalance" => new OutstandingBalancePage(),
                "ManualRefund" => new ManualRefundPage(),
                "MyBookings" => new MyBookingsPage(),
                "PaymentSummary" => new PaymentSummaryPage(),
                _ => null
            };
            if (pageObject == null) return; _pages[route] = pageObject;
        }
        Current = pageObject;
    }
}
