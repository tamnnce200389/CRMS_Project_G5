using System.Globalization;
using System.Windows;
using System.Windows.Input;
using CRMS.Data;
using CRMS.Helpers;
using CRMS.Services;
using CRMS.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace CRMS.ViewModels;

public sealed class PaymentWorkspaceViewModel : BaseViewModel
{
    private readonly PaymentService _service = new();
    private readonly string _mode;
    private long? _loadedUser;
    private Guid _requestKey = Guid.NewGuid();
    public PaymentWorkspaceViewModel(string mode)
    {
        _mode = mode;
        CalculateBalanceCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(BookingLookup));
        RecordFinalPaymentCommand = new RelayCommand(async _ => await RecordAsync(false), _ => CanCollect);
        RecordManualRefundCommand = new RelayCommand(async _ => await RecordAsync(true), _ => CanRefund);
        CompleteBookingCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanComplete);
    }
    public string Title => _mode == "Balance" ? "Thu tiền còn lại" : _mode == "Refund" ? "Ghi nhận hoàn tiền" : "Tổng hợp thanh toán";
    public string Description => _mode == "Balance" ? "Đối soát phụ phí và tiền đã thu trước khi hoàn tất đơn. Thanh toán cuối bằng tiền mặt."
        : _mode == "Refund" ? "Chỉ ghi nhận sau khi đã duyệt và thực hiện hoàn tiền. Thao tác này không chuyển tiền qua ngân hàng."
        : "Tra cứu tổng tiền sau giảm giá, tiền đã thu, tiền hoàn và số tiền còn thiếu.";
    public Visibility BalanceVisibility => _mode == "Balance" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RefundVisibility => _mode == "Refund" ? Visibility.Visible : Visibility.Collapsed;
    public string[] RefundMethods { get; } = ["Tiền mặt", "Chuyển khoản"];
    private string _refundMethod = "Tiền mặt";
    public string RefundMethod { get => _refundMethod; set { _refundMethod = value; ResetRequest(); OnPropertyChanged(); } }
    private string _refundAmount = "";
    public string RefundAmount { get => _refundAmount; set { _refundAmount = value; ResetRequest(); OnPropertyChanged(); } }
    private string _approvalReference = "";
    public string ApprovalReference { get => _approvalReference; set { _approvalReference = value; ResetRequest(); OnPropertyChanged(); } }
    private string _bookingLookup = "";
    public string BookingLookup
    {
        get => _bookingLookup;
        set { _bookingLookup = value; Summary = null; ResetRequest(); OnPropertyChanged(); Refresh(); }
    }
    private PaymentSummary? _summary;
    public PaymentSummary? Summary
    {
        get => _summary;
        private set { _summary = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryVisibility)); OnPropertyChanged(nameof(StatusText)); Refresh(); }
    }
    public Visibility SummaryVisibility => Summary is null ? Visibility.Collapsed : Visibility.Visible;
    public string StatusText => Summary?.Status switch
    {
        "PendingDeposit" => "Chờ đặt cọc", "Confirmed" => "Đã xác nhận", "InUse" => "Đang thuê",
        "Returned" => "Đã trả xe", "Completed" => "Đã hoàn tất", "Cancelled" => "Đã hủy", "Expired" => "Hết hạn", _ => ""
    };
    private string _message = "Nhập mã đơn thuê để tra cứu.";
    public string Message { get => _message; private set { _message = value; OnPropertyChanged(); } }
    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanEdit)); Refresh(); } }
    public bool CanEdit => !IsBusy;
    private bool ValidSession => _loadedUser.HasValue && _loadedUser == CurrentUserService.Instance.UserId;
    public bool CanCollect => _mode == "Balance" && !IsBusy && ValidSession && Summary is { Status: "Returned", HasReturn: true, OutstandingBalance: > 0 };
    public bool CanComplete => _mode == "Balance" && !IsBusy && ValidSession && Summary?.CanComplete == true;
    public bool CanRefund => _mode == "Refund" && !IsBusy && ValidSession && Summary?.RefundableAmount > 0;
    public ICommand CalculateBalanceCommand { get; }
    public ICommand RecordFinalPaymentCommand { get; }
    public ICommand RecordManualRefundCommand { get; }
    public ICommand CompleteBookingCommand { get; }
    private void ResetRequest() => _requestKey = Guid.NewGuid();
    private void Refresh()
    {
        OnPropertyChanged(nameof(CanCollect)); OnPropertyChanged(nameof(CanComplete)); OnPropertyChanged(nameof(CanRefund));
        CommandManager.InvalidateRequerySuggested();
    }
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        var lookup = BookingLookup.Trim();
        var actor = CurrentUserService.Instance.UserId;
        Summary = null; _loadedUser = null; IsBusy = true;
        try
        {
            using var db = CrmsContextFactory.Create();
            var id = await db.Bookings.Where(b => b.BookingCode == lookup).Select(b => (long?)b.BookingId).SingleOrDefaultAsync();
            if (!id.HasValue) throw new InvalidOperationException("Không tìm thấy mã đơn thuê.");
            var summary = await _service.GetPaymentSummary(id.Value);
            if (actor != CurrentUserService.Instance.UserId || lookup != BookingLookup.Trim())
                throw new InvalidOperationException("Phiên hoặc mã đơn đã thay đổi. Hãy tra cứu lại.");
            _loadedUser = actor; Summary = summary;
            Message = summary.HasReturn ? "Đã đối soát cả phụ phí trả xe."
                : "Chưa có biên bản trả xe; số dư chưa bao gồm các phụ phí chưa phát sinh.";
        }
        catch (Exception ex) { Message = ex.GetBaseException().Message; }
        finally { IsBusy = false; }
    }
    private async Task RecordAsync(bool refund)
    {
        if ((refund ? !CanRefund : !CanCollect) || Summary is not { } summary) return;
        var amount = summary.OutstandingBalance;
        if (refund && (!decimal.TryParse(RefundAmount, NumberStyles.AllowDecimalPoint, CultureInfo.GetCultureInfo("vi-VN"), out amount) || amount <= 0))
        { Message = "Nhập tiền hoàn bằng chữ số, không dùng dấu phân cách hàng nghìn (ví dụ 500000)."; return; }
        var method = RefundMethod == "Chuyển khoản" ? "BankTransfer" : "Cash";
        if (refund)
        {
            try { PaymentRules.ValidateRefund(summary, amount, method, ApprovalReference); }
            catch (Exception ex) { Message = ex.Message; return; }
        }
        var prompt = refund ? $"Bạn đã duyệt và thực hoàn {amount:N2} đ cho đơn {summary.BookingCode}?"
            : $"Bạn đã thực nhận {amount:N2} đ tiền mặt cho đơn {summary.BookingCode}? Đơn sẽ được hoàn tất nếu đã đủ tiền.";
        if (MessageBox.Show(prompt, "Xác nhận giao dịch", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        if (!ValidSession) { Summary = null; Message = "Phiên đăng nhập đã thay đổi. Hãy tra cứu lại."; return; }
        IsBusy = true;
        try
        {
            var receipt = refund ? await _service.RecordManualRefund(summary.BookingId, amount, method, ApprovalReference, _requestKey)
                : await _service.RecordFinalPayment(summary.BookingId, amount, _requestKey);
            Summary = await _service.GetPaymentSummary(summary.BookingId);
            Message = receipt.AlreadyRecorded ? "Giao dịch đã được ghi nhận trước đó, không tạo bản ghi trùng."
                : refund ? "Đã ghi nhận hoàn tiền và cập nhật số dư." : "Đã thu đủ tiền và hoàn tất đơn thuê.";
        }
        catch (Exception ex) { Message = "Chưa hoàn tất thao tác: " + ex.GetBaseException().Message + " Có thể tra cứu lại để đối soát trước khi thử lại."; }
        finally { IsBusy = false; }
    }
    private async Task CompleteAsync()
    {
        if (!CanComplete || Summary is not { } summary) return;
        if (MessageBox.Show($"Hoàn tất đơn {summary.BookingCode} đã trả xe và đủ tiền?", "Hoàn tất đơn", MessageBoxButton.YesNo,
            MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        if (!ValidSession) return;
        IsBusy = true;
        try
        {
            await _service.CompletePaidBooking(summary.BookingId);
            Summary = await _service.GetPaymentSummary(summary.BookingId);
            Message = "Đã hoàn tất đơn thuê, không ghi thêm giao dịch thu tiền.";
        }
        catch (Exception ex) { Message = ex.GetBaseException().Message; }
        finally { IsBusy = false; }
    }
}
