using CRMS.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CRMS.Views.Staff
{
    public partial class CashDepositPage : Page
    {
        private readonly CashDepositViewModel _viewModel = new();
        private readonly DispatcherTimer _deadlineTimer = new() { Interval = TimeSpan.FromSeconds(5) };

        public CashDepositPage()
        {
            InitializeComponent();

            DataContext = _viewModel;
            Loaded += CashDepositPage_Loaded;
            _deadlineTimer.Tick += (_, _) => _viewModel.RefreshTimeSensitiveState();
            Unloaded += (_, _) => _deadlineTimer.Stop();
        }

        private async void CashDepositPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            _deadlineTimer.Start();
            await _viewModel.LoadAsync();
        }
    }
}
