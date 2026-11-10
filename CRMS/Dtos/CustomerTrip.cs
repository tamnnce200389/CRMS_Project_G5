namespace CRMS.Dtos;

public sealed record CustomerTrip(string BookingCode, string VehicleName, DateTime StartAt, DateTime EndAt, decimal TotalAmount, string Status, string RentalType = "SelfDrive", string PriceUnit = "Day", decimal? RentalUnits = null,
    long BookingId = 0, string DepositMethod = "Cash", bool IsDemo = false, DateTime? DepositDeadlineUtc = null);
