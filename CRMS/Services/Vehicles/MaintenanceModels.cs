using System;

namespace CRMS.Services.Vehicles
{
    public class MaintenanceListItem
    {
        public long MaintenanceId { get; set; }
        public long VehicleId { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public long CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime StartAtLocal { get; set; }
        public DateTime EndAtLocal { get; set; }
        public string MaintenanceStatus { get; set; } = "Scheduled";
        public string Description { get; set; } = string.Empty;
        public decimal Cost { get; set; }

        public string ReferenceCode
        {
            get { return $"BT-#{MaintenanceId}"; }
        }

        public string VehicleDisplayName
        {
            get { return $"{Brand} {Model}".Trim(); }
        }

        public string PeriodText
        {
            get { return $"{StartAtLocal:dd/MM/yyyy HH:mm} → {EndAtLocal:dd/MM/yyyy HH:mm}"; }
        }

        public bool IsScheduled
        {
            get { return string.Equals(MaintenanceStatus, "Scheduled", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsInProgress
        {
            get { return string.Equals(MaintenanceStatus, "InProgress", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsCompleted
        {
            get { return string.Equals(MaintenanceStatus, "Completed", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsCancelled
        {
            get { return string.Equals(MaintenanceStatus, "Cancelled", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsEditable
        {
            get { return IsScheduled || IsInProgress; }
        }

        public bool CanStart
        {
            get { return IsScheduled; }
        }

        public bool CanComplete
        {
            get { return IsScheduled || IsInProgress; }
        }

        public bool CanCancel
        {
            get { return IsScheduled || IsInProgress; }
        }

        public override string ToString()
        {
            return $"{ReferenceCode} · {LicensePlate}";
        }
    }

    public class MaintenanceFilter
    {
        public string? Keyword { get; set; }
        public long? VehicleId { get; set; }
        public string? MaintenanceStatus { get; set; }
        public int MaxRecords { get; set; } = 500;

        public MaintenanceFilter()
        {
        }

        public MaintenanceFilter(
            string? keyword = null,
            long? vehicleId = null,
            string? maintenanceStatus = null,
            int maxRecords = 500)
        {
            Keyword = keyword;
            VehicleId = vehicleId;
            MaintenanceStatus = maintenanceStatus;
            MaxRecords = maxRecords;
        }
    }

    public class MaintenanceScheduleRequest
    {
        public long VehicleId { get; set; }
        public DateTime StartAtLocal { get; set; }
        public DateTime EndAtLocal { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public bool StartImmediately { get; set; }

        public MaintenanceScheduleRequest()
        {
        }

        public MaintenanceScheduleRequest(
            long vehicleId,
            DateTime startAtLocal,
            DateTime endAtLocal,
            string description,
            decimal cost = 0m,
            bool startImmediately = false)
        {
            VehicleId = vehicleId;
            StartAtLocal = startAtLocal;
            EndAtLocal = endAtLocal;
            Description = description;
            Cost = cost;
            StartImmediately = startImmediately;
        }
    }

    public class MaintenanceUpdateRequest
    {
        public long MaintenanceId { get; set; }
        public DateTime StartAtLocal { get; set; }
        public DateTime EndAtLocal { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public string MaintenanceStatus { get; set; } = "Scheduled";

        public MaintenanceUpdateRequest()
        {
        }

        public MaintenanceUpdateRequest(
            long maintenanceId,
            DateTime startAtLocal,
            DateTime endAtLocal,
            string description,
            decimal cost,
            string maintenanceStatus)
        {
            MaintenanceId = maintenanceId;
            StartAtLocal = startAtLocal;
            EndAtLocal = endAtLocal;
            Description = description;
            Cost = cost;
            MaintenanceStatus = maintenanceStatus;
        }
    }
}
