using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services;
using CRMS.Services.Vehicles;
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
    public string TotalLabel => _selection.ExpectedTotal.HasValue ? $"{_selection.ExpectedTotal:N0} ₫" : "Xác nhận theo giá hiện tại";
    public string DepositLabel => _selection.ExpectedTotal.HasValue ? $"{Math.Round(_selection.ExpectedTotal.Value / 2m, 2):N0} ₫" : "50% tổng tiền";
    private bool _busy;
    public bool IsBusy { get => _busy; private set { _busy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanClose)); CommandManager.InvalidateRequerySuggested(); } }
    public bool CanClose => !IsBusy;
    private string _status = "Đặt cọc tiền mặt; đơn chờ nhân viên xác nhận đã nhận cọc.";
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }
    public BookingCreated? Result { get; private set; }
    public ICommand ConfirmCommand { get; }
    public event Action? Completed;
    public BookingCheckoutViewModel(BookingSelection selection, string vehicleName, CustomerBookingService? service = null)
    {
        _selection = selection; VehicleName = vehicleName; _service = service ?? new CustomerBookingService();
        ConfirmCommand = new AsyncRelayCommand(async _ => await ConfirmAsync(), _ => !IsBusy && Result == null);
    }
    public async Task ConfirmAsync()
    {
        if (IsBusy || Result != null) return;
        IsBusy = true;
        try
        {
            Status = "Đang kiểm tra lịch trống và tạo đơn…";
            Result = await _service.CreateAsync(_selection, _requestKey);
            Status = $"Đã tạo đơn {Result.BookingCode}. Cọc {Result.DepositAmount:N0} ₫ trước {AvailabilityRules.ToVietnamLocal(Result.DepositDeadline):HH:mm dd/MM/yyyy}.";
            Completed?.Invoke();
        }
        catch (SqlException ex) when (ex.Number is 51004 or 51005 or 51009) { Status = "Xe vừa có lịch thuê, bảo trì hoặc lịch khóa. Vui lòng tìm lại xe."; }
        catch (SqlException ex) when (ex.Number == 51014) { Status = "Giá thuê vừa thay đổi. Vui lòng tìm lại xe và xác nhận giá mới."; }
        catch (SqlException ex) when (ex.Number is 1205 or 51003) { Status = "Hệ thống đang bận. Bạn có thể thử xác nhận lại."; }
        catch (Exception ex) { Status = "Chưa thể tạo đơn: " + ex.Message; }
        finally { IsBusy = false; }
    }
}
