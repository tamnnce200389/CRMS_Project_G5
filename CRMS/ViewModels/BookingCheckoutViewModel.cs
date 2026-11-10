using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services;
using CRMS.Services.Vehicles;
using CRMS.Services.Payments;
using Microsoft.Data.SqlClient;
namespace CRMS.ViewModels;

public sealed class BookingCheckoutViewModel : BaseViewModel
{
    private readonly BookingSelection _selection;
    private readonly CustomerBookingService _service;
    private readonly Guid _requestKey = Guid.NewGuid();
    public string VehicleName { get; }
    public string RentalLabel => _selection.PriceUnit switch { "Hour" => "Thuê theo giờ", "Month" => $"Gói {_selection.DurationMonths} tháng", _ => _selection.RentalMode == "WithDriver" ? "Thuê có tài xế" : "Thuê theo ngày" };
    public string PeriodLabel => $"{_selection.StartAt:HH:mm dd/MM/yyyy} → {_selection.EndAt:HH:mm dd/MM/yyyy}";
    public string TotalLabel => Result != null ? $"{Result.TotalAmount:N0} ₫" : _selection.ExpectedTotal.HasValue ? $"{_selection.ExpectedTotal:N0} ₫" : "Xác nhận theo giá hiện tại";
    public string DepositLabel => Result != null ? $"{Result.DepositAmount:N0} ₫" : _selection.ExpectedTotal.HasValue ? $"{Math.Round(_selection.ExpectedTotal.Value / 2m, 2):N0} ₫" : "50% tổng tiền";
    public bool DemoPaymentsEnabled { get; }
    public bool IsDemoSelection => DepositMethod == "Demo";
    public string DepositCaption => IsDemoSelection ? "Cọc mô phỏng · 50% tiền thuê" : "Tiền cọc 50%";
    private bool _busy;
    private bool _attempted;
    public bool IsBusy { get => _busy; private set { _busy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanClose)); OnPropertyChanged(nameof(CanChooseDepositMethod)); CommandManager.InvalidateRequerySuggested(); } }
    public bool CanClose => !IsBusy;
    public bool CanChooseDepositMethod => !IsBusy && !_attempted;
    private string _status = "Đặt cọc tiền mặt; đơn chờ nhân viên xác nhận đã nhận cọc.";
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }
    public BookingCreated? Result { get; private set; }
    private string _depositMethod = "Cash";
    public string DepositMethod
    {
        get => _depositMethod;
        set
        {
            if (!CanChooseDepositMethod || Result is not null || value is not ("Cash" or "Demo") || (value == "Demo" && !DemoPaymentsEnabled)) return;
            _depositMethod = value; OnPropertyChanged();
            OnPropertyChanged(nameof(IsDemoSelection)); OnPropertyChanged(nameof(DepositCaption));
            Status = value == "Demo" ? "Chế độ demo: chỉ chuyển khoản thử nghiệm 1.000đ. Tiền cọc hiển thị là mô phỏng; Staff xác nhận sau khi kiểm tra tiền về."
                : "Đặt cọc tiền mặt; đơn chờ nhân viên xác nhận đã nhận cọc.";
        }
    }
    public ICommand ConfirmCommand { get; }
    public event Action? Completed;
    public BookingCheckoutViewModel(BookingSelection selection, string vehicleName, CustomerBookingService? service = null, DemoPaymentOptions? demoOptions = null)
    {
        _selection = selection; VehicleName = vehicleName; _service = service ?? new CustomerBookingService();
        DemoPaymentsEnabled = (demoOptions ?? DemoPaymentOptions.Load()).Enabled;
        if (DemoPaymentsEnabled) DepositMethod = "Demo";
        ConfirmCommand = new AsyncRelayCommand(async _ => await ConfirmAsync(), _ => !IsBusy && Result == null);
    }
    public async Task ConfirmAsync()
    {
        if (IsBusy || Result != null) return;
        _attempted = true;
        IsBusy = true;
        try
        {
            Status = "Đang kiểm tra lịch trống và tạo đơn…";
            Result = await _service.CreateAsync(_selection with { DepositMethod = "Cash", IsDemo = IsDemoSelection }, _requestKey);
            OnPropertyChanged(nameof(TotalLabel)); OnPropertyChanged(nameof(DepositLabel));
            Status = $"Đã tạo đơn {Result.BookingCode}. Cọc {Result.DepositAmount:N0} ₫ trước {AvailabilityRules.ToVietnamLocal(Result.DepositDeadline):HH:mm dd/MM/yyyy}.";
            if (Result.IsDemo) Status = $"Đã tạo đơn demo {Result.BookingCode}. Cọc mô phỏng {Result.DepositAmount:N0}đ; chỉ chuyển khoản thử nghiệm 1.000đ và chờ Staff xác nhận.";
            Completed?.Invoke();
        }
        catch (SqlException ex) when (ex.Number is 51004 or 51005 or 51009) { Status = "Xe vừa có lịch thuê, bảo trì hoặc lịch khóa. Vui lòng tìm lại xe."; }
        catch (SqlException ex) when (ex.Number == 51014) { Status = "Giá thuê vừa thay đổi. Vui lòng tìm lại xe và xác nhận giá mới."; }
        catch (SqlException ex) when (ex.Number is 1205 or 51003) { Status = "Hệ thống đang bận. Bạn có thể thử xác nhận lại."; }
        catch (Exception ex) { Status = "Chưa thể tạo đơn: " + ex.Message; }
        finally { IsBusy = false; }
    }
}
