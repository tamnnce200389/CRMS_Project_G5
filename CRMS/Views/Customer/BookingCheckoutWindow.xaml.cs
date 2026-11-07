using System.Windows;
using CRMS.Services;
using CRMS.ViewModels;
namespace CRMS.Views.Customer;
public partial class BookingCheckoutWindow : Window
{
    public BookingCheckoutViewModel ViewModel { get; }
    public BookingCheckoutWindow(BookingSelection selection, string vehicleName, CustomerBookingService? service = null)
    {
        InitializeComponent();
        ViewModel = new(selection, vehicleName, service); DataContext = ViewModel;
        ViewModel.Completed += () => Dispatcher.BeginInvoke(new Action(() =>
        {
            MessageBox.Show(this, ViewModel.Status, "Đã tạo đơn thuê", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }));
        Closing += (_, e) => e.Cancel = !ViewModel.CanClose;
    }
}
