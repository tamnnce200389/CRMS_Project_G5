
using CRMS.Views;
using CRMS.Views.Customer;
using CRMS.Helpers;
using CRMS.Services;
using CRMS.Views.Admin;
using CRMS.Views.Guest;
using CRMS.Views.Staff;
using System.Windows.Controls;
using System.Windows.Input;

namespace CRMS.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        // 1. GUEST / CUSTOMER
        private readonly HomePage _homePage = new();
        private readonly ProfilePage _profilePage = new();

        // TV7 - F22: My Reviews
        private MyReviewsPage? _myReviewsPage;

        // 2. ADMIN PAGES
        private readonly DashboardPage _adminDashboard = new();
        private readonly VehiclePage _adminVehicles = new();
        private readonly DriverPage _adminDrivers = new();
        private readonly BookingPage _bookingPage = new();
        private readonly ReportPage _reportPage = new();

        private UserManagementPage? _userManagementPage;

        // TV7 - F20: Voucher Management
        private VoucherManagementPage? _voucherManagementPage;

        // TV7 - F23: Review Moderation
        private ReviewModerationPage? _reviewModerationPage;

        // 3. STAFF PAGES
        private readonly StaffDashboardPage _staffDashboard = new();
        private readonly StaffVehiclePage _staffVehicles = new();
        private readonly StaffDriverPage _staffDrivers = new();

        // 4. CURRENT USER ROLE
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

        // 5. CURRENT PAGE
        private Page _currentPage;

        public Page CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage == value)
                    return;

                _currentPage = value;
                OnPropertyChanged();
                RefreshNavigationStates();
            }
        }

        // 6. NAVIGATION STATES
        public bool IsCustomerExplore =>
            CurrentPage is HomePage
            or CarSearchPage
            or CarDetailsPage;

        public bool IsCustomerBookings =>
            CurrentPage is MyBookingsPage;

        public bool IsCustomerPayments =>
            CurrentPage is PaymentSummaryPage;

        public bool IsCustomerProfile =>
            CurrentPage is ProfilePage;

        // TV7 - F22
        public bool IsCustomerReviews =>
            CurrentPage is MyReviewsPage;

        public bool IsDashboard =>
            CurrentPage == _adminDashboard ||
            CurrentPage == _staffDashboard;

        public bool IsVehicles =>
            CurrentPage == _adminVehicles ||
            CurrentPage == _staffVehicles;

        public bool IsDrivers =>
            CurrentPage == _adminDrivers ||
            CurrentPage == _staffDrivers;

        public bool IsBookings =>
            CurrentPage == _bookingPage;

        public bool IsReports =>
            CurrentPage == _reportPage;

        public bool IsUserManagement =>
            CurrentPage == _userManagementPage;

        public bool IsVoucherManagement =>
            CurrentPage == _voucherManagementPage;

        // TV7 - F23
        public bool IsReviewModeration =>
            CurrentPage == _reviewModerationPage;

        // 7. NAVIGATION COMMAND
        public ICommand NavigateCommand { get; }

        public MainViewModel()
        {
            if (IsAdmin)
            {
                _currentPage = _adminDashboard;
            }
            else if (IsStaff)
            {
                _currentPage = _staffDashboard;
            }
            else
            {
                _currentPage = _homePage;
            }

            NavigateCommand = new RelayCommand(
                parameter => Navigate(parameter?.ToString()));
        }

        // 8. NAVIGATION
        private void Navigate(string? destination)
        {
            switch (destination)
            {
                // Guest / Customer
                case "Home":
                    if (!IsManagement)
                    {
                        CurrentPage = _homePage;
                    }
                    break;

                case "Profile":
                    if (IsLoggedIn)
                    {
                        CurrentPage = _profilePage;
                    }
                    break;

                // TV7 - F22: My Reviews
                case "MyReviews":
                    if (IsCustomer)
                    {
                        CurrentPage = _myReviewsPage
                            ??= new MyReviewsPage();
                    }
                    break;

                // Dashboard
                case "Dashboard":
                    if (IsAdmin)
                    {
                        CurrentPage = _adminDashboard;
                    }
                    else if (IsStaff)
                    {
                        CurrentPage = _staffDashboard;
                    }
                    break;

                // Vehicles
                case "Vehicles":
                    if (IsAdmin)
                    {
                        CurrentPage = _adminVehicles;
                    }
                    else if (IsStaff)
                    {
                        CurrentPage = _staffVehicles;
                    }
                    break;

                // Drivers
                case "Drivers":
                    if (IsAdmin)
                    {
                        CurrentPage = _adminDrivers;
                    }
                    else if (IsStaff)
                    {
                        CurrentPage = _staffDrivers;
                    }
                    break;

                // Bookings
                case "Bookings":
                    if (IsManagement)
                    {
                        CurrentPage = _bookingPage;
                    }
                    break;

                // Reports
                case "Reports":
                    if (IsAdmin)
                    {
                        CurrentPage = _reportPage;
                    }
                    break;

                // User Management
                case "UserManagement":
                    if (IsAdmin)
                    {
                        CurrentPage = _userManagementPage
                            ??= new UserManagementPage();
                    }
                    break;

                // TV7 - F20: Voucher Management
                case "VoucherManagement":
                    if (IsAdmin)
                    {
                        CurrentPage = _voucherManagementPage
                            ??= new VoucherManagementPage();
                    }
                    break;

                // TV7 - F23: Review Moderation
                case "ReviewModeration":
                    if (IsAdmin)
                    {
                        CurrentPage = _reviewModerationPage
                            ??= new ReviewModerationPage();
                    }
                    break;

                // Cash Deposit
                case "CashDeposit":
                    if (IsManagement)
                    {
                        CurrentPage = new CashDepositPage();
                    }
                    break;

                // Payment Home
                case "PaymentHome":
                    CurrentPage = IsAdmin
                        ? _adminDashboard
                        : IsStaff
                            ? _staffDashboard
                            : _homePage;
                    break;

                // Outstanding Balance
                case "OutstandingBalance":
                    if (IsManagement)
                    {
                        CurrentPage = new OutstandingBalancePage();
                    }
                    break;

                // Manual Refund
                case "ManualRefund":
                    if (IsManagement)
                    {
                        CurrentPage = new ManualRefundPage();
                    }
                    break;

                // My Bookings
                case "MyBookings":
                    if (IsCustomer)
                    {
                        CurrentPage = new MyBookingsPage();
                    }
                    break;

                // Payment Summary
                case "PaymentSummary":
                    if (IsLoggedIn)
                    {
                        CurrentPage = new PaymentSummaryPage();
                    }
                    break;

                // Availability
                case "Availability":
                    if (IsManagement)
                    {
                        CurrentPage = new AvailabilityPage();
                    }
                    break;

                // Maintenance
                case "Maintenance":
                    if (IsAdmin)
                    {
                        CurrentPage = new MaintenancePage();
                    }
                    break;

                default:
                    break;
            }
        }

        // 9. REFRESH NAVIGATION STATES
        private void RefreshNavigationStates()
        {
            OnPropertyChanged(nameof(IsCustomerExplore));
            OnPropertyChanged(nameof(IsCustomerBookings));
            OnPropertyChanged(nameof(IsCustomerPayments));
            OnPropertyChanged(nameof(IsCustomerProfile));
            OnPropertyChanged(nameof(IsCustomerReviews));

            OnPropertyChanged(nameof(IsDashboard));
            OnPropertyChanged(nameof(IsVehicles));
            OnPropertyChanged(nameof(IsDrivers));
            OnPropertyChanged(nameof(IsBookings));
            OnPropertyChanged(nameof(IsReports));
            OnPropertyChanged(nameof(IsUserManagement));
            OnPropertyChanged(nameof(IsVoucherManagement));

            // TV7 - F23
            OnPropertyChanged(nameof(IsReviewModeration));
        }

        // 10. OPEN AVAILABILITY
        public void OpenAvailability(long? vehicleId = null)
        {
            if (IsManagement)
            {
                CurrentPage = new AvailabilityPage(vehicleId);
            }
        }

        // 11. OPEN MAINTENANCE
        public void OpenMaintenance(long? vehicleId = null)
        {
            if (IsAdmin)
            {
                CurrentPage = new MaintenancePage(vehicleId);
            }
        }

        // 12. REFRESH SESSION
        public void RefreshSession()
        {
            OnPropertyChanged(nameof(IsGuest));
            OnPropertyChanged(nameof(IsCustomer));
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(IsStaff));
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(IsManagement));

            if (IsAdmin)
            {
                CurrentPage = _adminDashboard;
            }
            else if (IsStaff)
            {
                CurrentPage = _staffDashboard;
            }
            else
            {
                CurrentPage = _homePage;
            }

            RefreshNavigationStates();
        }
    }
}
