namespace CRMS.Models;
public sealed record VehicleTypeFacet(int? CategoryId, byte? Seats, string CategoryName, int Count)
{
    public string Label => CategoryId == null ? "Tất cả loại xe" : $"{Seats} chỗ\n{CategoryName switch { "Hatchback" => "Mini (Hatchback)", "SUV" => "SUV gầm cao", "MPV" => "MPV gầm thấp", "Pickup" => "Bán tải", "Electric" => "Xe điện", "Luxury" => "Xe sang", _ => CategoryName }}";
    public string CountLabel => $"{Count} xe";
    public string BodyGeometry => CategoryName switch
    {
        "Pickup" => "M3,25 L3,18 L12,18 L17,9 L29,9 L33,18 L51,18 L51,25 M20,10 L20,18 L31,18 M3,25 L10,25 M19,25 L36,25 M45,25 L51,25 M10,25 A5,5 0 1 0 20,25 A5,5 0 1 0 10,25 M36,25 A5,5 0 1 0 46,25 A5,5 0 1 0 36,25",
        "SUV" or "MPV" or "Hatchback" => "M3,25 L3,16 L11,15 L17,7 L38,7 L47,16 L52,18 L52,25 L46,25 M20,8 L17,16 L43,16 L37,8 M29,8 L29,16 M3,25 L10,25 M20,25 L36,25 M10,25 A5,5 0 1 0 20,25 A5,5 0 1 0 10,25 M36,25 A5,5 0 1 0 46,25 A5,5 0 1 0 36,25",
        _ => "M2,25 L2,19 L12,16 L21,9 L35,9 L45,16 L53,19 L53,25 L46,25 M22,10 L16,17 L41,17 L34,10 M28,10 L28,17 M2,25 L10,25 M20,25 L36,25 M10,25 A5,5 0 1 0 20,25 A5,5 0 1 0 10,25 M36,25 A5,5 0 1 0 46,25 A5,5 0 1 0 36,25"
    };
}
public sealed record BrandFacet(string Name, int Count)
{
    public string CountLabel => $"({Count} xe)";
    public string Mark => Name switch { "Tất cả hãng xe" => "", "Mercedes-Benz" => "✧", "Mitsubishi" => "◆", "VinFast" => "V", "Toyota" => "T", "Hyundai" => "H", "Honda" => "H", "Suzuki" => "S", "Lexus" => "L", "Mazda" => "M", _ => Name.Length <= 4 ? Name.ToUpperInvariant() : Name[..2].ToUpperInvariant() };
}
public sealed record SelfDriveFacets(List<VehicleTypeFacet> Types, List<BrandFacet> Brands, List<string> ChildSeatCodes);
