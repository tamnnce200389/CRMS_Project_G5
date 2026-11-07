using System.ComponentModel.DataAnnotations.Schema;
namespace CRMS.Models;
public partial class Booking
{
    public string PriceUnit { get; set; } = "Day";
    public decimal? RentalUnits { get; set; }
    public decimal? UnitRate { get; set; }
    [NotMapped] public string BillingLabel => PriceUnit switch { "Hour" => "Theo giờ", "Month" => "Theo tháng", _ => "Theo ngày" };
}
