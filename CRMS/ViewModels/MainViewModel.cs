
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services;

namespace CRMS.ViewModels;

public sealed class MainViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;

    public MainViewModel()
        : this(
            ApplicationServices.NavigationFactory?.Invoke()
            ?? throw new InvalidOperationException(
                "Navigation is not configured."))
    {
    }

    public MainViewModel(INavigationService navigation)
    {
        _navigation = navigation
            ?? throw new ArgumentNullException(nameof(navigation));

        _navigation.Changed += RefreshNavigationStates;

        PropertyChangedEventManager.AddHandler(
            CurrentUserService.Instance,
            SessionChanged,
            nameof(CurrentUserService.SessionVersion));

        LogoutCommand = new RelayCommand(
            _ => CurrentUserService.Instance.Logout());

        NavigateCommand = new RelayCommand(
            parameter =>
                _navigation.Navigate(
                    parameter?.ToString() ?? "Home"));

        RefreshSession();
    }
    private void SessionChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        RefreshSession();
    }

    // CURRENT PAGE
    public object? CurrentPage
    {
        get => _navigation.Current;
        set => _navigation.Current = value;
    }

    // USER ROLES
    public bool IsGuest =>
        CurrentUserService.Instance.IsGuest;

    public bool IsCustomer =>
        CurrentUserService.Instance.IsCustomer;

    public bool IsLoggedIn =>
        CurrentUserService.Instance.IsLoggedIn;

    public bool IsStaff =>
        CurrentUserService.Instance.IsStaff;

    public bool IsAdmin =>
        CurrentUserService.Instance.IsAdmin;

    public bool IsManagement =>
        IsStaff || IsAdmin;

    // CUSTOMER NAVIGATION
    public bool IsCustomerExplore =>
        _navigation.CurrentRoute is
            "Home" or "CarSearch" or "CarDetails";

    public bool IsCustomerBookings =>
        _navigation.CurrentRoute == "MyBookings";

    public bool IsCustomerPayments =>
        _navigation.CurrentRoute == "PaymentSummary";

    public bool IsCustomerProfile =>
        _navigation.CurrentRoute == "Profile";

    // TV7 - F22: MY REVIEWS
    public bool IsCustomerReviews =>
        _navigation.CurrentRoute == "MyReviews";

    // ADMIN / STAFF NAVIGATION
    public bool IsDashboard =>
        _navigation.CurrentRoute == "Dashboard";

    public bool IsVehicles =>
        _navigation.CurrentRoute == "Vehicles";

    public bool IsAvailability =>
        _navigation.CurrentRoute == "Availability";

    public bool IsMaintenance =>
        _navigation.CurrentRoute == "Maintenance";

    public bool IsDrivers =>
        _navigation.CurrentRoute == "Drivers";

    public bool IsBookings =>
        _navigation.CurrentRoute == "Bookings";

    public bool IsReports =>
        _navigation.CurrentRoute == "Reports";

    public bool IsUserManagement =>
        _navigation.CurrentRoute == "UserManagement";

    // TV7 - F20: VOUCHER MANAGEMENT
    public bool IsVoucherManagement =>
        _navigation.CurrentRoute == "VoucherManagement";

    // TV7 - F23: REVIEW MODERATION
    public bool IsReviewModeration =>
        _navigation.CurrentRoute == "ReviewModeration";

    // PAYMENT NAVIGATION
    public bool IsCashDeposit =>
        _navigation.CurrentRoute == "CashDeposit";

    public bool IsOutstandingBalance =>
        _navigation.CurrentRoute == "OutstandingBalance";

    // TV7 - F15: MANUAL REFUND
    public bool IsManualRefund =>
        _navigation.CurrentRoute == "ManualRefund";

    // COMMANDS
    public ICommand NavigateCommand { get; }

    public ICommand LogoutCommand { get; }

    // OPEN AVAILABILITY
    public void OpenAvailability(long? vehicleId = null)
    {
        _navigation.Navigate("Availability", vehicleId);
    }

    // OPEN MAINTENANCE
    public void OpenMaintenance(long? vehicleId = null)
    {
        _navigation.Navigate("Maintenance", vehicleId);
    }

    // OPEN PAYMENT SUMMARY
    public void OpenPayment(string code)
    {
        _navigation.Navigate("PaymentSummary", code);
    }

    // OPEN SEARCH
    public void OpenSearch(SearchNavigationRequest request)
    {
        _navigation.Navigate("CarSearch", request);
    }

    // REFRESH NAVIGATION STATES
    private void RefreshNavigationStates()
    {
        OnPropertyChanged(nameof(CurrentPage));

        OnPropertyChanged(nameof(IsCustomerExplore));
        OnPropertyChanged(nameof(IsCustomerBookings));
        OnPropertyChanged(nameof(IsCustomerPayments));
        OnPropertyChanged(nameof(IsCustomerProfile));
        OnPropertyChanged(nameof(IsCustomerReviews));

        OnPropertyChanged(nameof(IsDashboard));
        OnPropertyChanged(nameof(IsVehicles));
        OnPropertyChanged(nameof(IsAvailability));
        OnPropertyChanged(nameof(IsMaintenance));
        OnPropertyChanged(nameof(IsDrivers));
        OnPropertyChanged(nameof(IsBookings));
        OnPropertyChanged(nameof(IsReports));
        OnPropertyChanged(nameof(IsUserManagement));

        OnPropertyChanged(nameof(IsVoucherManagement));
        OnPropertyChanged(nameof(IsReviewModeration));

        OnPropertyChanged(nameof(IsCashDeposit));
        OnPropertyChanged(nameof(IsOutstandingBalance));
        OnPropertyChanged(nameof(IsManualRefund));
    }

    // REFRESH SESSION
    public void RefreshSession()
    {
        _navigation.Reset();

        OnPropertyChanged(nameof(IsGuest));
        OnPropertyChanged(nameof(IsCustomer));
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsStaff));
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsManagement));

        _navigation.Navigate(
            IsManagement ? "Dashboard" : "Home");

        RefreshNavigationStates();
    }
}
