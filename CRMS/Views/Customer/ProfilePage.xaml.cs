using System.Windows;
using System.Windows.Controls;
using CRMS.ViewModels;
namespace CRMS.Views.Customer;

public partial class ProfilePage : Page
{
    public ProfilePage() { InitializeComponent(); DataContext = new ProfileViewModel(); }
    private void Page_Loaded(object sender, RoutedEventArgs e) { if (DataContext is ProfileViewModel vm) vm.LoadCommand.Execute(null); }
    private void Page_Unloaded(object sender, RoutedEventArgs e) { if (DataContext is ProfileViewModel vm) vm.Cancel(); }
}
