using CRMS.Services.Vehicles;
using CRMS.Services;
namespace CRMS.Dtos;

public sealed record CustomerTrip(string BookingCode, string VehicleName, DateTime StartAt, DateTime EndAt, decimal TotalAmount, string Status, string RentalType = "SelfDrive", string PriceUnit = "Day", decimal? RentalUnits = null,
    long BookingId = 0, string DepositMethod = "Cash", bool IsDemo = false, DateTime? DepositDeadlineUtc = null)
{
    public string EffectiveStatus => BookingStatusRules.EffectiveStatus(Status, DepositDeadlineUtc);
    public string DemoPaymentAction => EffectiveStatus == "PendingDeposit" ? "Thanh toán QR demo"
        : EffectiveStatus is "Expired" or "Cancelled" ? "Xem trạng thái demo" : "Xem biên nhận demo";
    public DateTime StartLocal => AvailabilityRules.ToVietnamLocal(StartAt);
    public DateTime EndLocal => AvailabilityRules.ToVietnamLocal(EndAt);
    public string BillingLabel => (IsDemo ? "DEMO · " : "") + (PriceUnit switch { "Hour" => $"Thuê theo giờ · {RentalUnits:0.##} giờ", "Month" => $"Thuê dài hạn · {RentalUnits:0} tháng", _ => RentalType == "WithDriver" ? "Thuê có tài xế" : "Thuê theo ngày" });
    public string StatusLabel => BookingStatusRules.Label(EffectiveStatus);
}
