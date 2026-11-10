using System.Windows;
using CRMS.Services;
using CRMS.ViewModels;
using CRMS.Views.Customer;
namespace CRMS.Views.Guest;

public partial class LongTermDetailWindow : Window
{
    public LongTermDetailWindow(CarSearchItem car, DateTime? initialStart = null, int initialMonths = 1)
    {
        InitializeComponent(); var vm = new LongTermDetailViewModel(car, initialStart, initialMonths); DataContext = vm;
        Loaded += (_, _) => vm.LoadCommand.Execute(null); Closed += (_, _) => vm.Cancel();
        vm.CheckoutRequested += selection => { var checkout = new BookingCheckoutWindow(selection, $"{car.Brand} {car.Model}") { Owner = this }; if (checkout.ShowDialog() == true) DialogResult = true; };
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
