using System;
using System.Windows;
using System.Windows.Controls;
using CRMS.ViewModels;

namespace CRMS.Views.Admin
{
    public partial class MaintenancePage : Page
    {
        private bool _isInitialized;

        public MaintenancePage()
            : this(new MaintenanceViewModel())
        {
        }

        public MaintenancePage(long? initialVehicleId)
            : this(new MaintenanceViewModel(initialVehicleId))
        {
        }

        public MaintenancePage(MaintenanceViewModel viewModel)
        {
            if (viewModel == null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            InitializeComponent();
            DataContext = viewModel;
            Loaded += MaintenancePage_Loaded;
            DataContextChanged += MaintenancePage_DataContextChanged;
            AttachHooks();
        }

        private void MaintenancePage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachHooks();
        }

        private void AttachHooks()
        {
            MaintenanceViewModel? vm = DataContext as MaintenanceViewModel;
            if (vm == null)
            {
                return;
            }

            vm.ConfirmAction = (message, title) =>
                MessageBox.Show(
                    Window.GetWindow(this),
                    message,
                    title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No) == MessageBoxResult.Yes;
        }

        private async void MaintenancePage_Loaded(object sender, RoutedEventArgs e)
        {
            AttachHooks();
            if (_isInitialized)
            {
                return;
            }

            MaintenanceViewModel? vm = DataContext as MaintenanceViewModel;
            if (vm != null)
            {
                _isInitialized = true;
                await vm.InitializeAsync();
            }
        }
    }
}
