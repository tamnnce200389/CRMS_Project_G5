
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRMS.Models
{
    public partial class VehicleLongTermRate
    {
        // Khoa ngoai den Vehicles
        public long VehicleId { get; set; }

        // SQL INT -> C# int
        public int DurationMonths { get; set; }

        // Gia thang goc
        public decimal BaseMonthlyRate { get; set; }

        // Phan tram giam gia
        public decimal DiscountPercent { get; set; }

        // Gia thang sau giam tu SQL
        public decimal MonthlyRate { get; set; }

        // Trang thai goi thue
        public bool IsActive { get; set; }

        // Thoi gian cap nhat
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public virtual Vehicle Vehicle { get; set; } = null!;

        // Tong tien hop dong
        [NotMapped]
        public decimal TotalPrice =>
            MonthlyRate * DurationMonths;
    }
}
