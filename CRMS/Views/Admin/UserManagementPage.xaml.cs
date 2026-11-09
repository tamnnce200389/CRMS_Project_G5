using System.Windows;
using System.Windows.Controls;
using CRMS.ViewModels;
namespace CRMS.Views.Admin;

public partial class UserManagementPage : Page
{
    public UserManagementPage() { InitializeComponent(); DataContext = new UserManagementViewModel(); }
    private void Page_Loaded(object sender, RoutedEventArgs e) { if (DataContext is UserManagementViewModel vm) vm.LoadCommand.Execute(null); }
    private void Page_Unloaded(object sender, RoutedEventArgs e) { if (DataContext is UserManagementViewModel vm) vm.Cancel(); }
}
