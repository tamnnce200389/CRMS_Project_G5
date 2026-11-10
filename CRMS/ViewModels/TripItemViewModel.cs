using CRMS.Dtos;
using CRMS.Services;
using CRMS.Services.Vehicles;

namespace CRMS.ViewModels;

// Immutable row presentation, replaced by MyBookingsViewModel when its data changes.
// Services return CustomerTrip data and never depend on this view model.
public sealed record TripItemViewModel(CustomerTrip Trip)
{
    public string EffectiveStatus => BookingStatusRules.EffectiveStatus(Trip.Status, Trip.DepositDeadlineUtc);
    public DateTime StartLocal => AvailabilityRules.ToVietnamLocal(Trip.StartAt);
    public DateTime EndLocal => AvailabilityRules.ToVietnamLocal(Trip.EndAt);
    public string BillingLabel => (Trip.IsDemo ? "DEMO · " : "") + (Trip.PriceUnit switch
    {
        "Hour" => $"Thuê theo giờ · {Trip.RentalUnits:0.##} giờ",
        "Month" => $"Thuê dài hạn · {Trip.RentalUnits:0} tháng",
        _ => Trip.RentalType == "WithDriver" ? "Thuê có tài xế" : "Thuê theo ngày"
    });
    public string StatusLabel => BookingStatusRules.Label(EffectiveStatus);
}
