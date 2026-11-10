
using System;

namespace CRMS.Services
{
    // Du lieu xe duoc chon tu F05
    public sealed record BookingSelection(
        long CustomerId,
        long VehicleId,
        string RentalMode,
        DateTime StartAt,
        DateTime EndAt,
        string PriceUnit = "Day",
        int? DurationMonths = null,
        decimal? ExpectedTotal = null,
        string DepositMethod = "Cash",
        bool IsDemo = false
    );

}
