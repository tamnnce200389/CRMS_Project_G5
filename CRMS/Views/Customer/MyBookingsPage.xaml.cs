using System.Windows;
using System.Windows.Controls;
using CRMS.ViewModels;
namespace CRMS.Views.Customer;

public partial class MyBookingsPage : Page
{
    public MyBookingsPage() { InitializeComponent(); DataContext = new MyBookingsViewModel(); }
    private void Page_Loaded(object sender, RoutedEventArgs e) { if (DataContext is MyBookingsViewModel vm) vm.LoadCommand.Execute(null); }
    private void Page_Unloaded(object sender, RoutedEventArgs e) { if (DataContext is MyBookingsViewModel vm) vm.Cancel(); }
    private void Payment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string code } && Window.GetWindow(this)?.DataContext is MainViewModel main) main.OpenPayment(code);
    }
}
