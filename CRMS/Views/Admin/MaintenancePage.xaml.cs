using System.Windows;
using System.Windows.Controls;
using CRMS.ViewModels;

namespace CRMS.Views.Admin;

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
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Loaded += MaintenancePage_Loaded;
        DataContextChanged += (_, _) => AttachHooks();
        AttachHooks();
    }

    private void AttachHooks()
    {
        if (DataContext is not MaintenanceViewModel vm)
            return;

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
            return;

        if (DataContext is MaintenanceViewModel vm)
        {
            _isInitialized = true;
            await vm.InitializeAsync();
        }
    }
}
