
using System.Windows;
using System.Windows.Controls;
using CRMS.ViewModels;

namespace CRMS.Views.Admin
{
    public partial class ReportPage : Page
    {
        private readonly RevenueReportViewModel _viewModel = new();

        public ReportPage()
        {
            InitializeComponent();

            DataContext = _viewModel;

            Loaded += ReportPage_Loaded;
        }

        private async void ReportPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await _viewModel.LoadAsync();
        }
    }
}
