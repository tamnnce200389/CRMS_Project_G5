
using CRMS.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CRMS.Views.Staff
{
    public partial class StaffDriverPage : Page
    {
        private bool _loaded;

        public StaffDriverPage()
        {
            InitializeComponent();

            Loaded += StaffDriverPage_Loaded;
        }

        private async void StaffDriverPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_loaded)
                return;

            _loaded = true;

            if (DataContext is DriverViewModel vm)
            {
                await vm.LoadDriversAsync();
            }
        }
    }
}
