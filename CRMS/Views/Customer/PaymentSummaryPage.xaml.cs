using CRMS.ViewModels;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
namespace CRMS.Views.Customer;
public partial class PaymentSummaryPage : Page
{
    private readonly Action? _goBack;
    private readonly DispatcherTimer _deadlineTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    public PaymentSummaryPage(Action? goBack = null)
    {
        _goBack = goBack;
        InitializeComponent();
        DataContext = new PaymentWorkspaceViewModel("Summary");
        _deadlineTimer.Tick += (_, _) => (DataContext as PaymentWorkspaceViewModel)?.RefreshTimeSensitiveState();
        Loaded += (_, _) => _deadlineTimer.Start();
        Unloaded += (_, _) => _deadlineTimer.Stop();
    }
    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_goBack != null) _goBack();
        else if (Window.GetWindow(this)?.DataContext is MainViewModel main)
            main.CurrentPage = new MyBookingsPage();
    }
}
