using System.Windows;
using System.Windows.Controls;
using CRMS.ViewModels;

namespace CRMS.Views.Staff
{
    public partial class StaffVehiclePage : Page
    {
        private bool _loaded;

        public StaffVehiclePage()
        {
            InitializeComponent();
            Loaded += StaffVehiclePage_Loaded;
        }

        private async void StaffVehiclePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            VehicleViewModel? vm = DataContext as VehicleViewModel;
            if (vm != null)
            {
                await vm.LoadVehiclesAsync();
            }
        }
    }
}
