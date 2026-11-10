using System.Windows;
using CRMS.Services;
using CRMS.ViewModels;
namespace CRMS.Views.Guest;

public partial class VehiclePhotosWindow : Window
{
    public VehiclePhotosWindow(CarSearchItem vehicle)
    {
        InitializeComponent(); var vm = new VehiclePhotosViewModel(vehicle); DataContext = vm;
        MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
        Loaded += (_, _) => vm.LoadCommand.Execute(null); Closed += (_, _) => vm.Cancel();
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
