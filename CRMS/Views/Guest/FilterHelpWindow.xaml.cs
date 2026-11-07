using System.Windows;
using CRMS.ViewModels;

namespace CRMS.Views.Guest;

public partial class FilterHelpWindow : Window
{
    public FilterHelpWindow(FilterHelpViewModel explanation)
    {
        InitializeComponent();
        DataContext = explanation;
        MaxWidth = SystemParameters.WorkArea.Width - 40;
        MaxHeight = SystemParameters.WorkArea.Height - 40;
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
