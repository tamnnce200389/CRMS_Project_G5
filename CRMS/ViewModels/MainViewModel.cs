
using CRMS.Helpers;
using CRMS.Views;
using System.Windows.Controls;
using System.Windows.Input;

namespace CRMS.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        // Tao cac Page mot lan
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
                if (_currentPage != value)
                {
                    _currentPage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDashboard));
                    OnPropertyChanged(nameof(IsVehicles));
                    OnPropertyChanged(nameof(IsBookings));
                    OnPropertyChanged(nameof(IsDrivers));
                    OnPropertyChanged(nameof(IsReports));
                }
            }
        }

        public ICommand NavigateCommand { get; }
        public bool IsDashboard => CurrentPage == _dashboardPage;
        public bool IsVehicles => CurrentPage == _vehiclePage;
        public bool IsBookings => CurrentPage == _bookingPage;
        public bool IsDrivers => CurrentPage == _driverPage;
        public bool IsReports => CurrentPage == _reportPage;

        public MainViewModel()
        {
            _currentPage = _dashboardPage;

            NavigateCommand = new RelayCommand(parameter =>
            {
                switch (parameter?.ToString())
                {
                    case "Dashboard":
                        CurrentPage = _dashboardPage;
                        break;

                    case "Vehicles":
                        CurrentPage = _vehiclePage;
                        break;

                    case "Bookings":
                        CurrentPage = _bookingPage;
                        break;

                    case "Drivers":
                        CurrentPage = _driverPage;
                        break;

                    case "Reports":
                        CurrentPage = _reportPage;
                        break;
                }
            });
        }
    }
}
