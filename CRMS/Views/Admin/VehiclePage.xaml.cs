using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CRMS.Services.Vehicles;
using CRMS.ViewModels;

namespace CRMS.Views.Admin
{
    public partial class VehiclePage : Page
    {
        private bool _isLoaded;

        public VehiclePage()
        {
            InitializeComponent();
            Loaded += VehiclePage_Loaded;
            DataContextChanged += VehiclePage_DataContextChanged;
            AttachViewModelHooks();
        }

        private void VehiclePage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachViewModelHooks();
        }

        private void AttachViewModelHooks()
        {
            VehicleViewModel? vm = DataContext as VehicleViewModel;
            if (vm == null)
            {
                return;
            }

            vm.OpenVehicleEditorAsync = OpenVehicleEditorDialogAsync;
            vm.OpenCategoryManagerAsync = OpenCategoryManagerDialogAsync;
            vm.ConfirmAction = (message, title) =>
                MessageBox.Show(
                    Window.GetWindow(this),
                    message,
                    title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No) == MessageBoxResult.Yes;

            vm.ShowError = (message, title) =>
                MessageBox.Show(
                    Window.GetWindow(this),
                    message,
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

            vm.NavigateToAvailability = vehicleId =>
            {
                Window? host = Window.GetWindow(this);
                MainViewModel? mainVm = host != null ? host.DataContext as MainViewModel : null;
                if (mainVm != null)
                {
                    mainVm.OpenAvailability(vehicleId);
                }
            };

            vm.NavigateToMaintenance = vehicleId =>
            {
                Window? host = Window.GetWindow(this);
                MainViewModel? mainVm = host != null ? host.DataContext as MainViewModel : null;
                if (mainVm != null)
                {
                    mainVm.OpenMaintenance(vehicleId);
                }
            };
        }

        private async void VehiclePage_Loaded(object sender, RoutedEventArgs e)
        {
            AttachViewModelHooks();
            if (_isLoaded)
            {
                return;
            }

            VehicleViewModel? vm = DataContext as VehicleViewModel;
            if (vm != null)
            {
                await vm.LoadVehiclesAsync();
                _isLoaded = true;
            }
        }

        private async Task<bool> OpenVehicleEditorDialogAsync(long? vehicleId)
        {
            try
            {
                var editVm = new VehicleEditViewModel();
                if (vehicleId.HasValue)
                {
                    await editVm.InitializeForEditAsync(vehicleId.Value);
                }
                else
                {
                    await editVm.InitializeForCreateAsync();
                }

                var dialog = new VehicleEditWindow(editVm)
                {
                    Owner = Window.GetWindow(this)
                };

                return dialog.ShowDialog() == true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Window.GetWindow(this),
                    ex.Message,
                    "Không thể mở biểu mẫu xe",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        private async Task<bool> OpenCategoryManagerDialogAsync()
        {
            try
            {
                var catVm = new CategoryManagementViewModel();
                await catVm.LoadAsync();

                var dialog = new CategoryManagementWindow(catVm)
                {
                    Owner = Window.GetWindow(this)
                };

                dialog.ShowDialog();
                return catVm.HasChanges;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Window.GetWindow(this),
                    ex.Message,
                    "Không thể mở danh mục loại xe",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        private void VehiclesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGrid? grid = sender as DataGrid;
            VehicleViewModel? vm = DataContext as VehicleViewModel;
            if (grid == null || vm == null || vm.SelectedVehicle == null)
            {
                return;
            }

            DependencyObject? source = e.OriginalSource as DependencyObject;
            if (source == null)
            {
                return;
            }

            DataGridRow? row = ItemsControl.ContainerFromElement(grid, source) as DataGridRow;
            if (row != null && vm.EditVehicleCommand.CanExecute(vm.SelectedVehicle))
            {
                vm.EditVehicleCommand.Execute(vm.SelectedVehicle);
            }
        }
    }
}
