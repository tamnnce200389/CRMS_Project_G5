using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using CRMS.Helpers;
using CRMS.Services;
using CRMS.Services.Payments;
using CRMS.Services.Vehicles;

namespace CRMS.ViewModels;

public sealed class DemoPaymentViewModel : BaseViewModel, IDisposable
{
    private readonly IDemoPaymentService _service;
    private readonly long _bookingId;
    private readonly long? _userId;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly bool _enabled;
    private bool _busy;
    private bool _fresh;
    private DemoPaymentSession? _session;
    private string? _publishedStatus;
    private string _message = "Đang tải thông tin thanh toán demo…";
    public DemoPaymentViewModel(long bookingId, IDemoPaymentService? service = null, bool? enabled = null)
    {
        _bookingId = bookingId; _userId = CurrentUserService.Instance.UserId;
        _service = service ?? ApplicationServices.CreateDemoPaymentService();
        _enabled = enabled ?? DemoPaymentOptions.Load().Enabled;
        RefreshCommand = new AsyncRelayCommand(_ => CheckStatusAsync(), _ => CanCheckStatus);
    }
    public bool IsBusy { get => _busy; private set { _busy = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
    public string BookingCode => _session?.BookingCode ?? "";
    public string TotalLabel => Money(_session?.RentalTotal ?? 0);
    public string SimulatedDepositLabel => Money(_session?.SimulatedDeposit ?? 0);
    public string ActualReceivedLabel => Money(_session?.ActualReceived ?? 0);
    public string TransferAmountLabel => "Chuyển khoản thử nghiệm: 1.000đ";
    public string TransferContent => _session?.TransferContent ?? "";
    public string AccountNumber => _session?.AccountNumber ?? "";
    public string ReceiverLabel => _session == null ? "" : $"{_session.BankName} · {_session.AccountName}";
    public string DeadlineLabel => _session == null ? "" : $"Hạn xác nhận: {AvailabilityRules.ToVietnamLocal(_session.Deadline):HH:mm dd/MM/yyyy}";
    public bool CanShowQr => _fresh && _enabled && _session?.CanTransfer == true && _userId == CurrentUserService.Instance.UserId;
    public bool IsSuccessful => _session?.IsSuccessful == true && _session.BookingStatus is not ("Cancelled" or "Expired");
    public string StatusTitle => IsSuccessful ? "Thanh toán demo thành công"
        : _session?.PaymentStatus == "Success" ? "Đã nhận tiền demo · cần đối soát"
        : _session?.EffectiveBookingStatus == "Expired" ? "Hết hạn đặt cọc"
        : _session != null && !_session.CanTransfer ? "Đơn không còn nhận chuyển khoản"
        : "Chờ Staff xác nhận tiền demo";
    public string BookingStatusLabel => "Đơn thuê: " + BookingStatusRules.Label(_session?.EffectiveBookingStatus ?? "PendingDeposit");
    public string Message { get => _message; private set { _message = value; OnPropertyChanged(); } }
    private ImageSource? _qrImage;
    public ImageSource? QrImage { get => _qrImage; private set { _qrImage = value; OnPropertyChanged(); } }
    public ICommand RefreshCommand { get; }
    public event Action<DemoPaymentSession>? SessionUpdated;
    public event Action<PaymentStatusNotice>? StatusCheckCompleted;
    private bool _isChecking;
    public bool IsChecking
    {
        get => _isChecking;
        private set
        {
            _isChecking = value;
            OnPropertyChanged(); OnPropertyChanged(nameof(CanCheckStatus)); OnPropertyChanged(nameof(CheckStatusButtonText));
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool CanCheckStatus => !IsChecking && !_lifetime.IsCancellationRequested;
    public string CheckStatusButtonText => IsChecking ? "Đang kiểm tra…" : "Kiểm tra trạng thái";
    public async Task CheckStatusAsync()
    {
        if (!CanCheckStatus) return;
        IsChecking = true;
        try
        {
            await RefreshAsync(waitForCurrent: true);
            if (!_lifetime.IsCancellationRequested)
                StatusCheckCompleted?.Invoke(new(StatusCheckResult,
                    LastRefreshSucceeded && IsSuccessful ? "Đặt cọc demo thành công" : "Kết quả kiểm tra thanh toán",
                    !LastRefreshSucceeded));
        }
        finally { IsChecking = false; }
    }
    public bool LastRefreshSucceeded { get; private set; }
    public string StatusCheckResult => !LastRefreshSucceeded ? Message
        : IsSuccessful ? $"Đặt cọc demo thành công.\nĐơn {BookingCode} đã xác nhận.\nThực nhận: {ActualReceivedLabel} · Cọc mô phỏng: {SimulatedDepositLabel}."
        : _session?.PaymentStatus == "Success" ? "Đã nhận tiền demo nhưng đơn đã hủy/hết hạn. Cần liên hệ Staff để đối soát, không chuyển thêm tiền."
        : _session?.EffectiveBookingStatus == "Expired" ? "Đơn đã hết hạn đặt cọc. Không chuyển thêm tiền; hãy tạo đơn mới hoặc liên hệ Staff nếu đã chuyển."
        : _session?.EffectiveBookingStatus == "Cancelled" ? "Đơn đã hủy. Không chuyển thêm tiền."
        : "Chưa có xác nhận nhận tiền từ Staff cho đơn này. Nếu đã chuyển, nhờ Staff kiểm tra đúng mã đơn và hoàn tất xác nhận.";
    private string _lastCheckedLabel = "";
    public string LastCheckedLabel { get => _lastCheckedLabel; private set { _lastCheckedLabel = value; OnPropertyChanged(); } }

    public async Task RefreshAsync(bool waitForCurrent = false)
    {
        if (_lifetime.IsCancellationRequested || (IsChecking && !waitForCurrent)) return;
        try
        {
            if (waitForCurrent) await _refreshLock.WaitAsync(_lifetime.Token);
            else if (!await _refreshLock.WaitAsync(0, _lifetime.Token)) return;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
        IsBusy = true;
        LastRefreshSucceeded = false;
        try
        {
            if (!_userId.HasValue || !CurrentUserService.Instance.IsCustomer || CurrentUserService.Instance.UserId != _userId)
                throw new InvalidOperationException("Phiên đăng nhập đã thay đổi. Vui lòng đóng cửa sổ và đăng nhập lại.");
            var session = await _service.GetSessionAsync(_bookingId, _lifetime.Token);
            if (_lifetime.IsCancellationRequested) return;
            if (CurrentUserService.Instance.UserId != _userId) throw new InvalidOperationException("Phiên đăng nhập đã thay đổi.");
            var changed = _session != session || _publishedStatus != session.EffectiveBookingStatus;
            _session = session;
            _fresh = true;
            QrImage = CanShowQr ? VietQrCode.CreateImage(VietQrCode.CreatePayload(session.BankBin, session.AccountNumber, session.TransferContent)) : null;
            Message = IsSuccessful ? "Staff đã kiểm tra và xác nhận khoản thực nhận 1.000đ. Cọc mô phỏng được xác nhận; không ghi nhận đã thu thật số tiền cọc này."
                : session.PaymentStatus == "Success" ? "Khoản thực nhận đã được ghi sổ nhưng đơn đã hủy/hết hạn. Liên hệ Staff để đối soát hoàn khoản tiền thực nhận."
                : !_enabled ? "Chế độ thanh toán demo đang tắt. Không tiếp tục chuyển tiền."
                : !session.CanTransfer ? "Không chuyển thêm tiền cho đơn này. Nếu đã chuyển, liên hệ Staff để đối soát."
                : "Chỉ chuyển 1.000đ với đúng nội dung bên dưới. Staff kiểm tra tiền về rồi xác nhận; cửa sổ này tự cập nhật trạng thái.";
            NotifySession();
            if (changed) { _publishedStatus = session.EffectiveBookingStatus; SessionUpdated?.Invoke(session); }
            LastRefreshSucceeded = true;
            LastCheckedLabel = $"Đã kiểm tra lúc {AvailabilityRules.ToVietnamLocal(DateTime.UtcNow):HH:mm:ss} · {StatusTitle}";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _fresh = false;
            QrImage = null;
            if (CurrentUserService.Instance.UserId != _userId) _session = null;
            Message = "Chưa thể cập nhật: " + ex.GetBaseException().Message;
            LastCheckedLabel = "Chưa thể kiểm tra trạng thái. Vui lòng thử lại.";
            NotifySession();
        }
        finally { IsBusy = false; _refreshLock.Release(); }
    }
    private void NotifySession()
    {
        foreach (var property in new[] { nameof(BookingCode), nameof(TotalLabel), nameof(SimulatedDepositLabel), nameof(ActualReceivedLabel),
            nameof(TransferContent), nameof(AccountNumber), nameof(ReceiverLabel), nameof(DeadlineLabel), nameof(CanShowQr), nameof(IsSuccessful), nameof(StatusTitle), nameof(BookingStatusLabel) })
            OnPropertyChanged(property);
    }
    private static string Money(decimal amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";
    public void Dispose()
    {
        _lifetime.Cancel(); QrImage = null;
        OnPropertyChanged(nameof(CanCheckStatus)); CommandManager.InvalidateRequerySuggested();
    }
}
