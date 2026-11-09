using CRMS.Services.Vehicles;
namespace CRMS.Dtos;

public sealed record CustomerTrip(string BookingCode, string VehicleName, DateTime StartAt, DateTime EndAt, decimal TotalAmount, string Status, string RentalType, string PriceUnit, decimal? RentalUnits)
{
    public DateTime StartLocal => AvailabilityRules.ToVietnamLocal(StartAt);
    public DateTime EndLocal => AvailabilityRules.ToVietnamLocal(EndAt);
    public string BillingLabel => PriceUnit switch { "Hour" => $"Thuê theo giờ · {RentalUnits:0.##} giờ", "Month" => $"Thuê dài hạn · {RentalUnits:0} tháng", _ => RentalType == "WithDriver" ? "Thuê có tài xế" : "Thuê theo ngày" };
    public string StatusLabel => Status switch { "PendingDeposit" => "Chờ đặt cọc", "Confirmed" => "Đã xác nhận", "InUse" => "Đang thuê", "Returned" => "Đã trả xe", "Completed" => "Hoàn tất", "Cancelled" => "Đã hủy", "Expired" => "Hết hạn", _ => Status };
}
