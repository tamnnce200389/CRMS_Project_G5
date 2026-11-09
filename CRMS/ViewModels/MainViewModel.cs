using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services;
namespace CRMS.ViewModels;

public sealed class MainViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    public MainViewModel() : this(ApplicationServices.NavigationFactory?.Invoke() ?? throw new InvalidOperationException("Navigation is not configured.")) { }
    public MainViewModel(INavigationService navigation)
    {
        _navigation = navigation; _navigation.Changed += RefreshNavigationStates;
        PropertyChangedEventManager.AddHandler(CurrentUserService.Instance, SessionChanged, nameof(CurrentUserService.SessionVersion));
        LogoutCommand = new RelayCommand(_ => CurrentUserService.Instance.Logout());
        NavigateCommand = new RelayCommand(p => _navigation.Navigate(p?.ToString() ?? "Home"));
        RefreshSession();
    }
    private void SessionChanged(object? sender, PropertyChangedEventArgs e) => RefreshSession();
    public object? CurrentPage { get => _navigation.Current; set => _navigation.Current = value; }
    public bool IsGuest => CurrentUserService.Instance.IsGuest;
    public bool IsCustomer => CurrentUserService.Instance.IsCustomer;
    public bool IsLoggedIn => CurrentUserService.Instance.IsLoggedIn;
    public bool IsStaff => CurrentUserService.Instance.IsStaff;
    public bool IsAdmin => CurrentUserService.Instance.IsAdmin;
    public bool IsManagement => IsStaff || IsAdmin;
    public bool IsCustomerExplore => _navigation.CurrentRoute is "Home" or "CarSearch" or "CarDetails";
    public bool IsCustomerBookings => _navigation.CurrentRoute == "MyBookings";
    public bool IsCustomerPayments => _navigation.CurrentRoute == "PaymentSummary";
    public bool IsCustomerProfile => _navigation.CurrentRoute == "Profile";
    public bool IsDashboard => _navigation.CurrentRoute == "Dashboard";
    public bool IsVehicles => _navigation.CurrentRoute == "Vehicles";
    public bool IsDrivers => _navigation.CurrentRoute == "Drivers";
    public bool IsBookings => _navigation.CurrentRoute == "Bookings";
    public bool IsReports => _navigation.CurrentRoute == "Reports";
    public bool IsUserManagement => _navigation.CurrentRoute == "UserManagement";
    public ICommand NavigateCommand { get; }
    public ICommand LogoutCommand { get; }
    public void OpenAvailability(long? vehicleId = null) => _navigation.Navigate("Availability", vehicleId);
    public void OpenMaintenance(long? vehicleId = null) => _navigation.Navigate("Maintenance", vehicleId);
    public void OpenPayment(string code) => _navigation.Navigate("PaymentSummary", code);
    public void OpenSearch(SearchNavigationRequest request) => _navigation.Navigate("CarSearch", request);
    private void RefreshNavigationStates()
    {
        foreach (var name in new[] { nameof(CurrentPage), nameof(IsCustomerExplore), nameof(IsCustomerBookings), nameof(IsCustomerPayments), nameof(IsCustomerProfile), nameof(IsDashboard), nameof(IsVehicles), nameof(IsDrivers), nameof(IsBookings), nameof(IsReports), nameof(IsUserManagement) }) OnPropertyChanged(name);
    }
    public void RefreshSession()
    {
        _navigation.Reset(); foreach (var name in new[] { nameof(IsGuest), nameof(IsCustomer), nameof(IsLoggedIn), nameof(IsStaff), nameof(IsAdmin), nameof(IsManagement) }) OnPropertyChanged(name);
        _navigation.Navigate(IsManagement ? "Dashboard" : "Home");
    }
}
