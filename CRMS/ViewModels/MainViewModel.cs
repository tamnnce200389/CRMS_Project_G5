using CRMS.Helpers;
using CRMS.Services;
using CRMS.Views;
using CRMS.Views.Admin;
using CRMS.Views.Guest;
using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace CRMS.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        // Cac trang duoc su dung trong he thong
        private readonly HomePage _homePage = new();
        private readonly DashboardPage _dashboardPage = new();
        private readonly VehiclePage _vehiclePage = new();
        private readonly BookingPage _bookingPage = new();
        private readonly DriverPage _driverPage = new();
        private readonly ReportPage _reportPage = new();

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

                OnPropertyChanged(nameof(IsHome));
                OnPropertyChanged(nameof(IsDashboard));
                OnPropertyChanged(nameof(IsVehicles));
                OnPropertyChanged(nameof(IsBookings));
                OnPropertyChanged(nameof(IsDrivers));
                OnPropertyChanged(nameof(IsReports));
            }
        }

        // Thong tin dang nhap
        private CurrentUserService Session =>
            CurrentUserService.Instance;

        public string UserName => Session.FullName;
        public string CurrentRole => Session.Role;

        public bool IsLoggedIn => Session.IsLoggedIn;
        public bool IsGuest => !IsLoggedIn;

        public bool IsCustomer =>
            string.Equals(CurrentRole, "Customer",
                StringComparison.OrdinalIgnoreCase);

        public bool IsStaff =>
            string.Equals(CurrentRole, "Staff",
                StringComparison.OrdinalIgnoreCase);

        public bool IsAdmin =>
            string.Equals(CurrentRole, "Admin",
                StringComparison.OrdinalIgnoreCase);
        // Staff va Admin su dung giao dien quan ly
        public bool IsManagement => IsAdmin || IsStaff;

        // Quyen truy cap cac trang
        public bool CanViewHome => IsGuest || IsCustomer;

        public bool CanViewBookings =>
            IsCustomer || IsStaff || IsAdmin;

        public bool CanViewDrivers =>
            IsStaff || IsAdmin;

        // Trang dang duoc chon
        public bool IsHome => CurrentPage == _homePage;
        public bool IsDashboard => CurrentPage == _dashboardPage;
        public bool IsVehicles => CurrentPage == _vehiclePage;
        public bool IsBookings => CurrentPage == _bookingPage;
        public bool IsDrivers => CurrentPage == _driverPage;
        public bool IsReports => CurrentPage == _reportPage;

        public ICommand NavigateCommand { get; }

        public MainViewModel()
        {
            // Khi chua dang nhap, mo HomePage
            _currentPage = _homePage;

            NavigateCommand = new RelayCommand(parameter =>
            {
                switch (parameter?.ToString())
                {
                    case "Home" when CanViewHome:
                        CurrentPage = _homePage;
                        break;

                    case "Dashboard" when IsAdmin:
                        CurrentPage = _dashboardPage;
                        break;

                    case "Vehicles" when IsAdmin:
                        CurrentPage = _vehiclePage;
                        break;

                    case "Bookings" when CanViewBookings:
                        CurrentPage = _bookingPage;
                        break;

                    case "Drivers" when CanViewDrivers:
                        CurrentPage = _driverPage;
                        break;

                    case "Reports" when IsAdmin:
                        CurrentPage = _reportPage;
                        break;
                }
            });
        }

        // Cap nhat sau khi Login / Logout
        public void RefreshSession()
        {
            OnPropertyChanged(nameof(UserName));
            OnPropertyChanged(nameof(CurrentRole));
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(IsGuest));
            OnPropertyChanged(nameof(IsCustomer));
            OnPropertyChanged(nameof(IsStaff));
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(CanViewHome));
            OnPropertyChanged(nameof(CanViewBookings));
            OnPropertyChanged(nameof(CanViewDrivers));
            OnPropertyChanged(nameof(IsManagement));

            if (IsAdmin)
            {
                CurrentPage = _dashboardPage;
            }
            else if (IsStaff)
            {
                // Tam thoi mo DriverPage
                // Sau nay thay bang StaffDashboardPage
                CurrentPage = _driverPage;
            }
            else
            {
                // Guest va Customer
                CurrentPage = _homePage;
            }
        }
    }
}
