using System;
using System.Windows;
using System.Windows.Controls;
using CRMS.ViewModels;

namespace CRMS.Views.Admin
{
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
            if (viewModel == null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            InitializeComponent();
            DataContext = viewModel;
            Loaded += AvailabilityPage_Loaded;
        }

        private async void AvailabilityPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized)
            {
                return;
            }

            AvailabilityViewModel? vm = DataContext as AvailabilityViewModel;
            if (vm != null)
            {
                _isInitialized = true;
                await vm.InitializeAsync();
            }
        }
    }
}
