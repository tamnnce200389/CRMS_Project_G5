using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CRMS.Services.Vehicles;
using CRMS.ViewModels;

namespace CRMS.Views.Admin;

public partial class VehiclePage : Page
{
    private bool _isLoaded;

    public VehiclePage()
    {
        InitializeComponent();
        Loaded += VehiclePage_Loaded;
        DataContextChanged += (_, _) => AttachViewModelHooks();
        AttachViewModelHooks();
    }

    private void AttachViewModelHooks()
    {
        if (DataContext is not VehicleViewModel vm)
            return;

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
            if (Window.GetWindow(this)?.DataContext is MainViewModel mainVm)
                mainVm.OpenAvailability(vehicleId);
        };

        vm.NavigateToMaintenance = vehicleId =>
        {
            if (Window.GetWindow(this)?.DataContext is MainViewModel mainVm)
                mainVm.OpenMaintenance(vehicleId);
        };
    }

    private async void VehiclePage_Loaded(object sender, RoutedEventArgs e)
    {
        AttachViewModelHooks();
        if (_isLoaded)
            return;

        if (DataContext is VehicleViewModel vm)
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
                await editVm.InitializeForEditAsync(vehicleId.Value);
            else
                await editVm.InitializeForCreateAsync();

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
        if (sender is DataGrid grid &&
            ItemsControl.ContainerFromElement(grid, e.OriginalSource as DependencyObject) is DataGridRow &&
            DataContext is VehicleViewModel vm &&
            vm.SelectedVehicle is VehicleListItem selected &&
            vm.EditVehicleCommand.CanExecute(selected))
        {
            vm.EditVehicleCommand.Execute(selected);
        }
    }
}
