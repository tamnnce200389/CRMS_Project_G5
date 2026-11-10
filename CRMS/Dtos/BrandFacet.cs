namespace CRMS.Models;

public sealed record BrandFacet(string Name, int Count)
{
    public string CountLabel => $"({Count} xe)";
    public string Mark => Name switch { "Tất cả hãng xe" => "", "Mercedes-Benz" => "✧", "Mitsubishi" => "◆", "VinFast" => "V", "Toyota" => "T", "Hyundai" => "H", "Honda" => "H", "Suzuki" => "S", "Lexus" => "L", "Mazda" => "M", _ => Name.Length <= 4 ? Name.ToUpperInvariant() : Name[..2].ToUpperInvariant() };
}
