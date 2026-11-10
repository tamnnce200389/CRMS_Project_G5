using System.Windows;
using CRMS.ViewModels;
namespace CRMS.Views.Guest;

public partial class RentalTimeWindow : Window
{
    private RentalTimeViewModel ViewModel => (RentalTimeViewModel)DataContext;
    public DateTime StartAt => ViewModel.StartAt;
    public DateTime EndAt => ViewModel.EndAt;
    public string RentalUnit => ViewModel.RentalUnit;
    public RentalTimeWindow(bool withDriver = false, bool preferHourly = false)
    {
        InitializeComponent(); DataContext = new RentalTimeViewModel(withDriver, preferHourly);
        if (withDriver) { DayTab.Visibility = Visibility.Collapsed; Title = "Thời gian - Xe có tài xế"; }
        ViewModel.Confirmed += () => DialogResult = true;
    }
    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
