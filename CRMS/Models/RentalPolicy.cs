using System;
using System.Collections.Generic;

namespace CRMS.Models;

public partial class RentalPolicy
{
    public byte PolicyId { get; set; }

    public decimal DepositPercent { get; set; }

    public decimal DriverDailyFee { get; set; }

    public int OnlineHoldMinutes { get; set; }

    public int CashHoldHours { get; set; }

    public DateTime UpdatedAt { get; set; }
}
