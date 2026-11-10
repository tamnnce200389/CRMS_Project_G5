namespace CRMS.Models;

public sealed record SelfDriveFacets(List<VehicleTypeFacet> Types, List<BrandFacet> Brands, List<string> ChildSeatCodes);
