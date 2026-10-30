using CRMS.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CRMS.Views.Staff
{
    public partial class CashDepositPage : Page
    {
        private readonly CashDepositViewModel _viewModel = new();

        public CashDepositPage()
        {
            InitializeComponent();

            DataContext = _viewModel;
            Loaded += CashDepositPage_Loaded;
        }

        private async void CashDepositPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await _viewModel.LoadAsync();
        }
    }
}