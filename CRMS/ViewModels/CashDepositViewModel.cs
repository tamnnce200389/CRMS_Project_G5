using CRMS.Helpers;
using CRMS.Services;
using CRMS.Services.Payments;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace CRMS.ViewModels;

public class CashDepositViewModel : BaseViewModel
{
    private readonly CashDepositService _service = new();
    private readonly PaymentService _payments = new();
    private readonly ObservableCollection<CashDepositItem> _items = new();
    private readonly Dictionary<long, Guid> _requestKeys = new();
    private long? _loadedUser;
    public ICollectionView Bookings { get; }
    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); Bookings.Refresh(); }
    }
    private CashDepositItem? _selectedBooking;
    public CashDepositItem? SelectedBooking
    {
        get => _selectedBooking;
        set { _selectedBooking = value; OnPropertyChanged(); RefreshCommands(); }
    }
    private string _message = "";
    public string Message { get => _message; private set { _message = value; OnPropertyChanged(); } }
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); RefreshCommands(); }
    }
    public bool CanConfirmDeposit => !IsLoading && SelectedBooking is not null &&
        _loadedUser.HasValue && _loadedUser == CurrentUserService.Instance.UserId;
    public ICommand LoadCommand { get; }
    public ICommand ConfirmDepositCommand { get; }

    public CashDepositViewModel()
    {
        Bookings = CollectionViewSource.GetDefaultView(_items);
        Bookings.Filter = item => item is CashDepositItem row &&
            (row.BookingCode.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
             row.CustomerName.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase));
        LoadCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsLoading);
        ConfirmDepositCommand = new RelayCommand(async _ => await ConfirmAsync(), _ => CanConfirmDeposit);
    }
    private void RefreshCommands()
    {
        OnPropertyChanged(nameof(CanConfirmDeposit));
        CommandManager.InvalidateRequerySuggested();
    }
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        SelectedBooking = null;
        _items.Clear();
        _loadedUser = null;
        var actor = CurrentUserService.Instance.UserId;
        Message = "Đang tải danh sách…";
        try
        {
            var items = await _service.GetPendingDepositsAsync();
            if (actor != CurrentUserService.Instance.UserId)
                throw new InvalidOperationException("Phiên đăng nhập đã thay đổi. Hãy tải lại.");
            foreach (var item in items) _items.Add(item);
            _loadedUser = actor;
            Message = items.Count == 0 ? "Không có đơn chờ cọc tiền mặt còn hạn." : $"Có {items.Count} đơn chờ cọc tiền mặt.";
        }
        catch (Exception ex) { Message = "Không tải được danh sách: " + ex.GetBaseException().Message; }
        finally { IsLoading = false; }
    }
    private async Task ConfirmAsync()
    {
        if (!CanConfirmDeposit || SelectedBooking is not { } row) return;
        if (MessageBox.Show($"Bạn đã thực nhận {row.DepositAmount:N2} đ tiền mặt cho đơn {row.BookingCode}?",
            "Xác nhận nhận cọc", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        if (!CanConfirmDeposit) return;
        IsLoading = true;
        if (!_requestKeys.TryGetValue(row.BookingId, out var key)) _requestKeys[row.BookingId] = key = Guid.NewGuid();
        string result;
        try
        {
            var receipt = await _payments.ConfirmCashDeposit(row.BookingId, row.DepositAmount, key);
            result = receipt.NeedsManualRefund
                ? "Cọc đã được ghi nhận nhưng đơn đã hết hạn/hủy. Không giao xe; chuyển đối soát hoàn tiền."
                : receipt.AlreadyRecorded ? "Giao dịch đã được ghi nhận trước đó; không thu cọc lần nữa."
                : "Đã ghi nhận cọc và xác nhận đơn thuê.";
        }
        catch (Exception ex) { Message = "Chưa xác nhận: " + ex.GetBaseException().Message; return; }
        finally { IsLoading = false; }
        await LoadAsync();
        Message = result;
    }
}
