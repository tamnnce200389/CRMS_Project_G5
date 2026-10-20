
using CRMS.ViewModels;
using System.Windows.Controls;

namespace CRMS.Views
{
    public partial class VehiclePage : Page
    {
        private bool _isLoaded = false;

        public VehiclePage()
        {
            InitializeComponent();
            Loaded += VehiclePage_Loaded;
        }

        private async void VehiclePage_Loaded(
            object sender, System.Windows.RoutedEventArgs e)
        {
            if (_isLoaded)
                return;

            if (DataContext is VehicleViewModel vm)
            {
                await vm.LoadVehiclesAsync();
                _isLoaded = true;
            }
        }
    }
}
