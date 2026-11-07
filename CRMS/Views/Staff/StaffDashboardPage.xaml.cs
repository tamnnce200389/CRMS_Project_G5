using CRMS.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CRMS.Views.Staff
{
    public partial class StaffDashboardPage : Page
    {
        public StaffDashboardPage()
        {
            InitializeComponent();
        }

        private void NavigateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string target)
                return;

            var win = Window.GetWindow(this) ?? Application.Current?.MainWindow;
            if (win?.DataContext is MainViewModel vm && vm.NavigateCommand.CanExecute(target))
            {
                vm.NavigateCommand.Execute(target);
            }
        }
    }
}
