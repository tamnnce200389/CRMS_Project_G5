using System.Windows;
using System.Windows.Controls;
using CRMS.ViewModels;

namespace CRMS.Views.Admin;

public partial class AvailabilityPage : Page
{
    private bool _isInitialized;

    public AvailabilityPage()
        : this(new AvailabilityViewModel())
    {
    }

    public AvailabilityPage(long? initialVehicleId)
        : this(new AvailabilityViewModel(initialVehicleId))
    {
    }

    public AvailabilityPage(AvailabilityViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Loaded += AvailabilityPage_Loaded;
    }

    private async void AvailabilityPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
            return;

        if (DataContext is AvailabilityViewModel vm)
        {
            _isInitialized = true;
            await vm.InitializeAsync();
        }
    }
}
