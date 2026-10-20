using System;
using System.Collections.Generic;

namespace CRMS.Models;

public partial class VehicleImage
{
    public long ImageId { get; set; }

    public long VehicleId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public bool IsPrimary { get; set; }

    public int SortOrder { get; set; }

    public virtual Vehicle Vehicle { get; set; } = null!;
}
