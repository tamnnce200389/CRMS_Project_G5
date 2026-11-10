using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CRMS.Services;
using CRMS.ViewModels;
namespace CRMS.Views.Customer;

public partial class MyBookingsPage : Page
{
    private readonly DispatcherTimer _deadlineTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private MyBookingsViewModel ViewModel => (MyBookingsViewModel)DataContext;
    public MyBookingsPage()
    {
        InitializeComponent();
        DataContext = new MyBookingsViewModel();
        _deadlineTimer.Tick += (_, _) => ViewModel.RefreshDeadlineLabels();
    }
    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _deadlineTimer.Start();
        ViewModel.LoadCommand.Execute(null);
    }
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _deadlineTimer.Stop();
        ViewModel.Cancel();
    }
    private MainViewModel? Main => (Window.GetWindow(this) ?? Application.Current?.MainWindow)?.DataContext as MainViewModel;
    private void NewTrip_Click(object sender, RoutedEventArgs e) => Main?.NavigateCommand.Execute("Home");
    private void Payment_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentUserService.Instance.IsCustomer && sender is Button { Tag: string code }) Main?.OpenPayment(code);
    }
    private void DemoPayment_Click(object sender, RoutedEventArgs e)
    {
        if (!CurrentUserService.Instance.IsCustomer || sender is not Button { DataContext: TripItemViewModel { Trip.IsDemo: true } row }) return;
        var actor = CurrentUserService.Instance.UserId;
        var window = new DemoPaymentWindow(row.Trip.BookingId) { Owner = Window.GetWindow(this) ?? Application.Current?.MainWindow };
        window.ViewModel.SessionUpdated += session =>
        {
            if (CurrentUserService.Instance.UserId == actor) ViewModel.UpdatePaymentSession(session);
        };
        window.ShowDialog();
        if (CurrentUserService.Instance.UserId == actor) ViewModel.LoadCommand.Execute(null);
    }
}
