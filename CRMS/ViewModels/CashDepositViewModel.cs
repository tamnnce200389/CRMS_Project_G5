using CRMS.Helpers;
using CRMS.Services;
using CRMS.Services.Payments;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;

namespace CRMS.ViewModels;

public class CashDepositViewModel : BaseViewModel
{
    private readonly ICashDepositService _deposits;
    private readonly IConfirmationService _dialogs;
    private readonly IPaymentService _payments;
    private readonly IDemoPaymentService _demoPayments;
    private readonly bool _demoEnabled;
    private readonly ObservableCollection<CashDepositItem> _items = new();
    private readonly Dictionary<long, Guid> _requestKeys = new();
    private long? _loadedUser;
    private long? _displayedUser;
    public ICollectionView Bookings { get; }
    private bool _includeUnavailable;
    public bool IncludeUnavailable
    {
        get => _includeUnavailable;
        set { if (_includeUnavailable == value || IsLoading) return; _includeUnavailable = value; OnPropertyChanged(); _ = LoadAsync(); }
    }
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
        set { if (ReferenceEquals(_selectedBooking, value)) return; _selectedBooking = value; BankTransactionReference = ""; HasVerifiedDemoReceipt = false; OnPropertyChanged(); OnPropertyChanged(nameof(ConfirmButtonText)); RefreshCommands(); }
    }
    private string _bankTransactionReference = "";
    public string BankTransactionReference { get => _bankTransactionReference; set { _bankTransactionReference = value; OnPropertyChanged(); RefreshCommands(); } }
    private bool _hasVerifiedDemoReceipt;
    public bool HasVerifiedDemoReceipt { get => _hasVerifiedDemoReceipt; set { _hasVerifiedDemoReceipt = value; OnPropertyChanged(); RefreshCommands(); } }
    public string ConfirmButtonText => SelectedBooking?.IsDemo == true
        ? SelectedBooking.IsExpired || SelectedBooking.BookingStatus == "Cancelled" ? "Ghi nhận tiền demo để đối soát" : "Xác nhận đã nhận tiền demo"
        : "Xác nhận đã nhận tiền cọc";
    private string _message = "";
    public string Message { get => _message; private set { _message = value; OnPropertyChanged(); } }
    private string _lastConfirmation = "";
    public string LastConfirmation { get => _lastConfirmation; private set { _lastConfirmation = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLastConfirmation)); } }
    public bool HasLastConfirmation => LastConfirmation.Length > 0;
    public bool CanEdit => !IsLoading;
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanEdit)); RefreshCommands(); }
    }
    public bool CanConfirmDeposit => !IsLoading && SelectedBooking?.CanReceive == true &&
        _loadedUser.HasValue && _loadedUser == CurrentUserService.Instance.UserId &&
        (!SelectedBooking.IsDemo || (_demoEnabled && HasVerifiedDemoReceipt && BankTransactionReference.Trim().Length is >= 3 and <= 100));
    public ICommand LoadCommand { get; }
    public ICommand ConfirmDepositCommand { get; }

    public CashDepositViewModel(IConfirmationService dialogs) : this(confirmation: dialogs) { }
    public CashDepositViewModel(bool? demoEnabled = null,
        ICashDepositService? deposits = null, IPaymentService? payments = null,
        IDemoPaymentService? demoPayments = null, IConfirmationService? confirmation = null)
    {
        _deposits = deposits ?? ApplicationServices.CreateCashDepositService();
        _payments = payments ?? ApplicationServices.CreatePaymentService();
        _dialogs = confirmation ?? ApplicationServices.Confirmation;
        _demoPayments = demoPayments ?? ApplicationServices.CreateDemoPaymentService();
        _demoEnabled = demoEnabled ?? DemoPaymentOptions.Load().Enabled;
        Bookings = CollectionViewSource.GetDefaultView(_items);
        Bookings.Filter = item => item is CashDepositItem row &&
            (row.BookingCode.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
             row.TransferContent.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
             row.CustomerName.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase));
        LoadCommand = new AsyncRelayCommand(async _ => await LoadAsync(), _ => !IsLoading);
        ConfirmDepositCommand = new AsyncRelayCommand(async _ => await ConfirmAsync(), _ => CanConfirmDeposit);
    }
    private void RefreshCommands()
    {
        OnPropertyChanged(nameof(CanConfirmDeposit));
        CommandManager.InvalidateRequerySuggested();
    }
    public void RefreshTimeSensitiveState()
    {
        foreach (var item in _items) item.RefreshDeadline();
        OnPropertyChanged(nameof(ConfirmButtonText));
        RefreshCommands();
    }
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        var actor = CurrentUserService.Instance.UserId;
        var selected = SelectedBooking;
        var reference = BankTransactionReference;
        var verified = HasVerifiedDemoReceipt;
        _loadedUser = null;
        if (_displayedUser != actor)
        {
            SelectedBooking = null;
            _items.Clear();
            LastConfirmation = "";
            selected = null;
        }
        Message = "Đang tải danh sách…";
        try
        {
            var items = await _deposits.GetDepositsAsync(IncludeUnavailable);
            if (actor != CurrentUserService.Instance.UserId)
                throw new InvalidOperationException("Phiên đăng nhập đã thay đổi. Hãy tải lại.");
            SelectedBooking = null;
            _items.Clear();
            foreach (var item in items) _items.Add(item);
            _displayedUser = actor;
            SelectedBooking = items.FirstOrDefault(i => i.BookingId == selected?.BookingId);
            if (SelectedBooking is { } current && selected is not null &&
                current.IsDemo == selected.IsDemo && current.DepositAmount == selected.DepositAmount &&
                current.DepositDeadline == selected.DepositDeadline && current.BookingStatus == selected.BookingStatus &&
                current.TransferContent == selected.TransferContent)
            { BankTransactionReference = reference; HasVerifiedDemoReceipt = verified; }
            _loadedUser = actor;
            Message = items.Count == 0 ? "Không có đơn chờ xác nhận. Bật ‘Hiện cả đơn hết hạn / VNPay cũ’ để xem các đơn không còn nhận cọc."
                : $"{items.Count(i => i.CanReceive)} đơn có thể nhận tiền / đối soát · {items.Count(i => !i.CanReceive)} đơn chỉ xem (hết hạn hoặc VNPay cũ).";
        }
        catch (Exception ex)
        {
            if (actor != CurrentUserService.Instance.UserId) { SelectedBooking = null; _items.Clear(); LastConfirmation = ""; }
            Message = "Không tải được danh sách: " + ex.GetBaseException().Message + " Hãy tải lại trước khi xác nhận.";
        }
        finally { IsLoading = false; }
    }
    public async Task ConfirmAsync()
    {
        if (!CanConfirmDeposit || SelectedBooking is not { } row) return;
        var reference = BankTransactionReference.Trim();
        var actor = CurrentUserService.Instance.UserId;
        var confirmation = row.IsDemo
            ? $"Bạn đã kiểm tra ngân hàng và thực nhận đúng 1.000đ cho đơn demo {row.BookingCode}?\nMã giao dịch: {BankTransactionReference.Trim()}\nTiền cọc {row.DepositAmount:N0}đ chỉ là mô phỏng. Đơn hết hạn/hủy chỉ được ghi nhận để đối soát, không xác nhận lại."
            : $"Bạn đã thực nhận {row.DepositAmount:N2} đ tiền mặt cho đơn {row.BookingCode}?";
        IsLoading = true;
        string result;
        try
        {
            // A modal dialog runs a nested dispatcher. Block reloads and repeated clicks before opening it.
            if (!_dialogs.Confirm(confirmation, "Xác nhận nhận cọc"))
            { Message = "Chưa xác nhận nhận tiền. Đơn và thông tin đối soát vẫn được giữ nguyên."; return; }
            if (actor != CurrentUserService.Instance.UserId || !ReferenceEquals(row, SelectedBooking))
                throw new InvalidOperationException("Phiên hoặc đơn đã thay đổi. Hãy tải lại trước khi xác nhận.");
            if (!row.CanReceive) throw new InvalidOperationException("Đơn đã hết hạn hoặc không hỗ trợ nhận cọc tại đây. Hãy tải lại.");
            if (!_requestKeys.TryGetValue(row.BookingId, out var key)) _requestKeys[row.BookingId] = key = Guid.NewGuid();
            if (row.IsDemo)
            {
                var receipt = await _demoPayments.ConfirmReceivedAsync(row.BookingId, DemoPaymentOptions.TransferAmount, reference, key);
                result = receipt.NeedsManualRefund ? "Đã ghi nhận thực nhận 1.000đ nhưng đơn đã hủy/hết hạn. Không xác nhận đơn; cần đối soát hoàn khoản tiền thực nhận."
                    : receipt.AlreadyRecorded ? "Khoản tiền demo đã được xác nhận trước đó. Không nhận tiền lần nữa."
                    : "Thanh toán demo thành công · Thực nhận 1.000đ · Đơn thuê đã xác nhận.";
            }
            else
            {
                var receipt = await _payments.ConfirmCashDeposit(row.BookingId, row.DepositAmount, key);
                result = receipt.NeedsManualRefund ? "Cọc đã được ghi nhận nhưng đơn đã hết hạn/hủy. Không giao xe; chuyển đối soát hoàn tiền."
                    : receipt.AlreadyRecorded ? "Giao dịch đã được ghi nhận trước đó; không thu cọc lần nữa." : "Đã ghi nhận cọc và xác nhận đơn thuê.";
            }
            LastConfirmation = $"{row.BookingCode} · {row.CustomerName}\n{result}" + (row.IsDemo ? $"\nMã giao dịch: {reference} · Cọc mô phỏng: {row.DepositAmount:N0}đ" : "");
        }
        catch (Exception ex) { Message = "Chưa xác nhận: " + ex.GetBaseException().Message; return; }
        finally { IsLoading = false; }
        await LoadAsync();
        Message = _loadedUser == actor ? result + " Đơn đã rời danh sách chờ; biên nhận được giữ bên dưới." : result + " " + Message;
    }
}
