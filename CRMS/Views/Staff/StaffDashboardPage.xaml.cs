
using System.Windows;
using System.Windows.Controls;
using CRMS.ViewModels;

namespace CRMS.Views.Staff
{
    public partial class StaffDashboardPage : Page
    {
        private readonly StaffDashboardViewModel _viewModel = new();

        public StaffDashboardPage()
        {
            InitializeComponent();

            DataContext = _viewModel;

            Loaded += StaffDashboardPage_Loaded;
        }

        private async void StaffDashboardPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await _viewModel.LoadAsync();
        }
    }
}
