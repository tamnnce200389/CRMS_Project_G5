using System.Windows;
using CRMS.ViewModels;
namespace CRMS.Views.Guest;
public partial class SelfDriveFilterWindow : Window
{
    public SelfDriveFilterDialogViewModel ViewModel { get; }
    private bool _closingAfterApply;
    public SelfDriveFilterWindow(CarSearchViewModel parent, string kind)
    {
        InitializeComponent();
        ViewModel = new(parent,kind); DataContext = ViewModel;
        if (kind == "ChildSeat") { Width = 600; Height = 590; }
        if (kind == "Sort") { Width = 820; Height = 285; }
        if (kind == "Transmission") { Width = 820; Height = 370; }
        if (kind is "Delivery" or "Pickup") { Width = 620; Height = 520; }
        if (kind == "Route") { Width = 660; Height = 650; }
        if (kind == "Travel") { Width = 620; Height = 520; }
        MaxWidth = SystemParameters.WorkArea.Width - 40; MaxHeight = SystemParameters.WorkArea.Height - 40;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
        ViewModel.Applied += () => { _closingAfterApply = true; DialogResult = true; };
        Closing += (_, e) => { if (ViewModel.IsApplying && !_closingAfterApply) e.Cancel = true; };
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void RouteTime_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RentalTimeWindow(true) { Owner = this };
        if (dialog.ShowDialog() == true) ViewModel.SetRentalPeriod(dialog.StartAt, dialog.EndAt);
    }
}
