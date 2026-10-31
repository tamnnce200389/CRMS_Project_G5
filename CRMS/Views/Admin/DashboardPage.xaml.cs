
using System.Windows;
using System.Windows.Controls;
using CRMS.ViewModels;

namespace CRMS.Views.Admin
{
    public partial class DashboardPage : Page
    {
        private readonly AdminDashboardViewModel _viewModel = new();

        public DashboardPage()
        {
            InitializeComponent();

            DataContext = _viewModel;

            Loaded += DashboardPage_Loaded;
        }

        private async void DashboardPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await _viewModel.LoadAsync();
        }
    }
}
