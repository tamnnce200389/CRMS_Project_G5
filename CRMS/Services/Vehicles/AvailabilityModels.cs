using System;
using System.Collections.Generic;

namespace CRMS.Services.Vehicles
{
    public enum AvailabilityConflictType
    {
        VehicleNotFound,
        VehicleSuspended,
        CategoryInactive,
        BookingInUse,
        BookingConfirmed,
        BookingPendingDeposit,
        Maintenance,
        CalendarBlock
    }

    public class AvailabilityConflict
    {
        public AvailabilityConflictType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime? StartAtLocal { get; set; }
        public DateTime? EndAtLocal { get; set; }
        public string? ReferenceCode { get; set; }

        public bool HasReferenceCode
        {
            get { return !string.IsNullOrWhiteSpace(ReferenceCode); }
        }

        public AvailabilityConflict()
        {
        }

        public AvailabilityConflict(
            AvailabilityConflictType type,
            string title,
            string reason,
            DateTime? startAtLocal = null,
            DateTime? endAtLocal = null,
            string? referenceCode = null)
        {
            Type = type;
            Title = title;
            Reason = reason;
            StartAtLocal = startAtLocal;
            EndAtLocal = endAtLocal;
            ReferenceCode = referenceCode;
        }
    }

    public class BookingConflictSnapshot
    {
        public string BookingCode { get; set; } = string.Empty;
        public string BookingStatus { get; set; } = string.Empty;
        public DateTime StartAtUtc { get; set; }
        public DateTime EndAtUtc { get; set; }
        public DateTime DepositDeadlineUtc { get; set; }

        public BookingConflictSnapshot()
        {
        }

        public BookingConflictSnapshot(
            string bookingCode,
            string bookingStatus,
            DateTime startAtUtc,
            DateTime endAtUtc,
            DateTime depositDeadlineUtc)
        {
            BookingCode = bookingCode;
            BookingStatus = bookingStatus;
            StartAtUtc = startAtUtc;
            EndAtUtc = endAtUtc;
            DepositDeadlineUtc = depositDeadlineUtc;
        }
    }

    public class MaintenanceConflictSnapshot
    {
        public long MaintenanceId { get; set; }
        public string MaintenanceStatus { get; set; } = string.Empty;
        public DateTime StartAtUtc { get; set; }
        public DateTime EndAtUtc { get; set; }
        public string Description { get; set; } = string.Empty;

        public MaintenanceConflictSnapshot()
        {
        }

        public MaintenanceConflictSnapshot(
            long maintenanceId,
            string maintenanceStatus,
            DateTime startAtUtc,
            DateTime endAtUtc,
            string description)
        {
            MaintenanceId = maintenanceId;
            MaintenanceStatus = maintenanceStatus;
            StartAtUtc = startAtUtc;
            EndAtUtc = endAtUtc;
            Description = description;
        }
    }

    public class VehicleAvailabilityResult
    {
        public long VehicleId { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public byte SeatCount { get; set; }
        public string Transmission { get; set; } = string.Empty;
        public string FuelType { get; set; } = string.Empty;
        public decimal DailyRate { get; set; }
        public string OperationalStatus { get; set; } = "Active";
        public string? PrimaryImageUrl { get; set; }
        public DateTime StartAtLocal { get; set; }
        public DateTime EndAtLocal { get; set; }
        public IList<AvailabilityConflict> Conflicts { get; set; } = new List<AvailabilityConflict>();

        public bool IsAvailable
        {
            get { return Conflicts.Count == 0; }
        }

        public bool HasConflicts
        {
            get { return Conflicts.Count > 0; }
        }

        public int ConflictCount
        {
            get { return Conflicts.Count; }
        }

        public string DisplayName
        {
            get { return $"{Brand} {Model}".Trim(); }
        }

        public string PeriodText
        {
            get { return $"{StartAtLocal:dd/MM/yyyy HH:mm} → {EndAtLocal:dd/MM/yyyy HH:mm}"; }
        }

        public string AvailabilityLabel
        {
            get { return IsAvailable ? "Khả dụng" : "Không khả dụng"; }
        }

        public string SummaryReason
        {
            get
            {
                if (IsAvailable)
                {
                    return "Sẵn sàng nhận lịch đặt trong khoảng giờ đã chọn.";
                }
                if (Conflicts.Count == 1)
                {
                    return Conflicts[0].Reason;
                }
                return $"{Conflicts[0].Reason} (+{Conflicts.Count - 1} xung đột khác)";
            }
        }

        public override string ToString()
        {
            return $"{LicensePlate} · {DisplayName} ({AvailabilityLabel})";
        }
    }
}
