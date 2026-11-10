using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CRMS.ViewModels;

namespace CRMS.Views.Customer;

public partial class DemoPaymentWindow : Window
{
    public DemoPaymentViewModel ViewModel { get; }
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool _closed;
    private readonly Action<string, string, MessageBoxImage> _showStatus;
    public DemoPaymentWindow(long bookingId, DemoPaymentViewModel? viewModel = null,
        Action<string, string, MessageBoxImage>? showStatus = null)
    {
        InitializeComponent();
        ViewModel = viewModel ?? new DemoPaymentViewModel(bookingId);
        DataContext = ViewModel;
        _showStatus = showStatus ?? ((text, title, icon) => MessageBox.Show(this, text, title, MessageBoxButton.OK, icon));
        ViewModel.StatusCheckCompleted += ShowStatus;
        Loaded += async (_, _) => { await ViewModel.RefreshAsync(); if (!_closed) _timer.Start(); };
        _timer.Tick += async (_, _) => await ViewModel.RefreshAsync();
        Closed += (_, _) => { _closed = true; _timer.Stop(); ViewModel.StatusCheckCompleted -= ShowStatus; ViewModel.Dispose(); };
    }
    private void ShowStatus(PaymentStatusNotice notice)
    {
        if (!_closed) _showStatus(notice.Message, notice.Title, notice.IsError ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }
    private void Back_Click(object sender, RoutedEventArgs e) => Close();
    private void CopyText_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string text } && !string.IsNullOrWhiteSpace(text))
        {
            try { Clipboard.SetText(text); }
            catch { MessageBox.Show(this, "Chưa thể sao chép. Bạn có thể nhập nội dung chuyển khoản đang hiển thị."); }
        }
    }
}
