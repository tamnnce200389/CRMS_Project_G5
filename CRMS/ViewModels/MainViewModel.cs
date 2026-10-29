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
        // =====================================
        // 1. GUEST / CUSTOMER
        // =====================================
        private readonly HomePage _homePage = new();
        private readonly ProfilePage _profilePage = new();

        // =====================================
        // 2. ADMIN PAGES
        // =====================================
        private readonly DashboardPage _adminDashboard = new();

        private readonly VehiclePage _adminVehicles = new();

        private readonly DriverPage _adminDrivers = new();

        private readonly BookingPage _bookingPage = new();

        private readonly ReportPage _reportPage = new();
        private readonly UserManagementPage _userManagementPage = new();

        // =====================================
        // 3. STAFF PAGES
        // =====================================
        private readonly StaffDashboardPage _staffDashboard = new();

        private readonly StaffVehiclePage _staffVehicles = new();

        private readonly StaffDriverPage _staffDrivers = new();

        // =====================================
        // 4. CURRENT USER ROLE
        // =====================================
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

        // =====================================
        // 5. CURRENT PAGE
        // =====================================
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

        // =====================================
        // 6. NAVIGATION STATES
        // =====================================
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

        // =====================================
        // 7. NAVIGATION COMMAND
        // =====================================
        public ICommand NavigateCommand { get; }

        public MainViewModel()
        {
            // Mac dinh theo vai tro dang dang nhap
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

        // =====================================
        // 8. NAVIGATION
        // =====================================
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

                // Dashboard cua tung Role
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

                // Admin quan ly - Staff tra cuu
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

                // Admin quan ly - Staff tra cuu
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

                // Trang danh sach don thue dung chung
                case "Bookings":
                    if (IsManagement)
                    {
                        CurrentPage = _bookingPage;
                    }
                    break;

                // Bao cao chi Admin duoc mo
                case "Reports":
                    if (IsAdmin)
                    {
                        CurrentPage = _reportPage;
                    }
                    break;
                // User management (Admin only)B
                case "UserManagement":
                    if (IsAdmin)
                    {
                        CurrentPage = _userManagementPage;
                    }
                    break;

                // Cac chuc nang chua tich hop
                // khong mo Page khac
                default:
                    break;
            }
        }

        // =====================================
        // 9. CAP NHAT TRANG THAI NAVIGATION
        // =====================================
        private void RefreshNavigationStates()
        {
            OnPropertyChanged(nameof(IsDashboard));
            OnPropertyChanged(nameof(IsVehicles));
            OnPropertyChanged(nameof(IsDrivers));
            OnPropertyChanged(nameof(IsBookings));
            OnPropertyChanged(nameof(IsReports));
            OnPropertyChanged(nameof(IsUserManagement));
        }

        // =====================================
        // 10. REFRESH SAU LOGIN / LOGOUT
        // =====================================
        public void RefreshSession()
        {
            // Cap nhat Role cho MainWindow
            OnPropertyChanged(nameof(IsGuest));
            OnPropertyChanged(nameof(IsCustomer));
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(IsStaff));
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(IsManagement));

            // Dieu huong theo Role
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
