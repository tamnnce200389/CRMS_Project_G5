
using System;
using System.Collections.Generic;
using CRMS.Services;
using CRMS.ViewModels;
using CRMS.Views;
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
        get => _current;

        set
        {
            _current = value;

            CurrentRoute = value?.GetType().Name switch
            {
                // Guest / Customer
                "HomePage" => "Home",
                "CarSearchPage" => "CarSearch",
                "CarDetailsPage" => "CarDetails",
                "ProfilePage" => "Profile",
                "MyBookingsPage" => "MyBookings",
                "MyReviewsPage" => "MyReviews",
                "PaymentSummaryPage" => "PaymentSummary",

                // Admin / Staff
                "DashboardPage" or "StaffDashboardPage" => "Dashboard",
                "VehiclePage" or "StaffVehiclePage" => "Vehicles",
                "DriverPage" or "StaffDriverPage" => "Drivers",
                "BookingPage" => "Bookings",
                "ReportPage" => "Reports",
                "UserManagementPage" => "UserManagement",

                // TV7
                "VoucherManagementPage" => "VoucherManagement",
                "ReviewModerationPage" => "ReviewModeration",
                "ManualRefundPage" => "ManualRefund",

                // Payment
                "CashDepositPage" => "CashDeposit",
                "OutstandingBalancePage" => "OutstandingBalance",

                // Other
                "AvailabilityPage" => "Availability",
                "MaintenancePage" => "Maintenance",

                _ => value?.GetType().Name ?? ""
            };

            Changed?.Invoke();
        }
    }

    public string CurrentRoute { get; private set; } = "";

    public void Reset()
    {
        _pages.Clear();
        Current = null;
    }

    public void Navigate(string route, object? argument = null)
    {
        bool admin = CurrentUserService.Instance.IsAdmin;
        bool staff = CurrentUserService.Instance.IsStaff;
        bool customer = CurrentUserService.Instance.IsCustomer;
        bool logged = CurrentUserService.Instance.IsLoggedIn;

        bool management = admin || staff;

        // Home
        if (route is "Home" or "PaymentHome")
        {
            route = management ? "Dashboard" : "Home";
        }

        // Admin / Staff
        if ((route is
                "Dashboard" or
                "Vehicles" or
                "Drivers" or
                "Bookings" or
                "CashDeposit" or
                "OutstandingBalance" or
                "ManualRefund" or
                "Availability")
            && !management)
        {
            return;
        }

        // Admin only
        if ((route is
                "UserManagement" or
                "Reports" or
                "Maintenance" or
                "VoucherManagement" or
                "ReviewModeration")
            && !admin)
        {
            return;
        }

        // Logged-in users
        if ((route is "Profile" or "PaymentSummary")
            && !logged)
        {
            return;
        }

        // Customer only
        if ((route is "MyBookings" or "MyReviews")
            && !customer)
        {
            return;
        }

        // Car search with arguments
        if (route == "CarSearch" &&
            argument is SearchNavigationRequest search)
        {
            Current = new CarSearchPage(
                search.Mode,
                search.Start,
                search.End,
                search.Context);

            return;
        }

        // Availability
        if (route == "Availability")
        {
            Current = new AvailabilityPage(argument as long?);
            return;
        }

        // Maintenance
        if (route == "Maintenance")
        {
            Current = new MaintenancePage(argument as long?);
            return;
        }

        // Payment summary with booking code
        if (route == "PaymentSummary" &&
            argument is string code)
        {
            var page = new PaymentSummaryPage(
                () => Navigate("MyBookings"));

            if (page.DataContext is PaymentWorkspaceViewModel vm)
            {
                vm.BookingLookup = code;
                Current = page;
                vm.CalculateBalanceCommand.Execute(null);
            }

            return;
        }

        // Create or reuse pages
        if (!_pages.TryGetValue(route, out var pageObject))
        {
            pageObject = route switch
            {
                // Guest / Customer
                "Home" => new HomePage(),
                "Profile" => new ProfilePage(),
                "MyBookings" => new MyBookingsPage(),
                "MyReviews" => new MyReviewsPage(),
                "PaymentSummary" => new PaymentSummaryPage(),

                // Admin / Staff
                "Dashboard" => admin
                    ? new DashboardPage()
                    : new StaffDashboardPage(),

                "Vehicles" => admin
                    ? new VehiclePage()
                    : new StaffVehiclePage(),

                "Drivers" => admin
                    ? new DriverPage()
                    : new StaffDriverPage(),

                "Bookings" => new BookingPage(),
                "Reports" => new ReportPage(),
                "UserManagement" => new UserManagementPage(),

                // TV7
                "VoucherManagement" => new VoucherManagementPage(),
                "ReviewModeration" => new ReviewModerationPage(),
                "ManualRefund" => new ManualRefundPage(),

                // Payment
                "CashDeposit" => new CashDepositPage(),
                "OutstandingBalance" => new OutstandingBalancePage(),

                _ => null
            };

            if (pageObject == null)
                return;

            _pages[route] = pageObject;
        }

        Current = pageObject;
    }
}
