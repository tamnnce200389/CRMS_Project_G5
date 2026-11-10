using System;
using System.Collections.Generic;
using System.IO;

namespace CRMS.Services.Vehicles
{
    public class VehicleFilter
    {
        public string? Keyword { get; set; }
        public int? CategoryId { get; set; }
        public string? OperationalStatus { get; set; }
        public decimal? MinDailyRate { get; set; }
        public decimal? MaxDailyRate { get; set; }
        public bool OnlyActiveCategories { get; set; }

        public VehicleFilter()
        {
        }

        public VehicleFilter(
            string? keyword = null,
            int? categoryId = null,
            string? operationalStatus = null,
            decimal? minDailyRate = null,
            decimal? maxDailyRate = null,
            bool onlyActiveCategories = false)
        {
            Keyword = keyword;
            CategoryId = categoryId;
            OperationalStatus = operationalStatus;
            MinDailyRate = minDailyRate;
            MaxDailyRate = maxDailyRate;
            OnlyActiveCategories = onlyActiveCategories;
        }
    }

    public class VehicleImageDto
    {
        public long ImageId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public int SortOrder { get; set; }

        public VehicleImageDto()
        {
        }

        public VehicleImageDto(long imageId, string imageUrl, bool isPrimary, int sortOrder)
        {
            ImageId = imageId;
            ImageUrl = imageUrl;
            IsPrimary = isPrimary;
            SortOrder = sortOrder;
        }

        public string Caption
        {
            get
            {
                if (ImageUrl.StartsWith("pack://application:,,,/CRMS;component/Assets/Vehicles/", StringComparison.Ordinal))
                {
                    string fileName = Path.GetFileNameWithoutExtension(ImageUrl);
                    return fileName.EndsWith("-interior", StringComparison.Ordinal) ? "Nội thất" : "Ngoại thất";
                }
                return IsPrimary ? "Ảnh đại diện" : "Ảnh chi tiết";
            }
        }
    }

    public class VehicleListItem
    {
        public long VehicleId { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public short ManufactureYear { get; set; }
        public byte SeatCount { get; set; }
        public string Transmission { get; set; } = string.Empty;
        public string FuelType { get; set; } = string.Empty;
        public decimal DailyRate { get; set; }
        public int OdometerKm { get; set; }
        public string OperationalStatus { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? PrimaryImageUrl { get; set; }
        public int ImageCount { get; set; }

        public bool IsActive
        {
            get { return OperationalStatus == "Active"; }
        }

        public override string ToString()
        {
            return $"{LicensePlate} · {Brand} {Model}".Trim();
        }
    }

    public class VehicleDetailsDto
    {
        public long VehicleId { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public short ManufactureYear { get; set; }
        public byte SeatCount { get; set; }
        public string Transmission { get; set; } = string.Empty;
        public string FuelType { get; set; } = string.Empty;
        public decimal DailyRate { get; set; }
        public int OdometerKm { get; set; }
        public string OperationalStatus { get; set; } = string.Empty;
        public string? Description { get; set; }
        public IList<VehicleImageDto> Images { get; set; } = new List<VehicleImageDto>();
    }

    public class VehicleImageInput
    {
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public int SortOrder { get; set; }

        public VehicleImageInput()
        {
        }

        public VehicleImageInput(string imageUrl, bool isPrimary = false, int sortOrder = 0)
        {
            ImageUrl = imageUrl;
            IsPrimary = isPrimary;
            SortOrder = sortOrder;
        }
    }

    public class VehicleUpsertRequest
    {
        public long? VehicleId { get; set; }
        public int CategoryId { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public short ManufactureYear { get; set; }
        public byte SeatCount { get; set; }
        public string Transmission { get; set; } = string.Empty;
        public string FuelType { get; set; } = string.Empty;
        public decimal DailyRate { get; set; }
        public int OdometerKm { get; set; }
        public string OperationalStatus { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<VehicleImageInput>? Images { get; set; }

        public VehicleUpsertRequest()
        {
        }

        public VehicleUpsertRequest(
            long? vehicleId,
            int categoryId,
            string licensePlate,
            string brand,
            string model,
            short manufactureYear,
            byte seatCount,
            string transmission,
            string fuelType,
            decimal dailyRate,
            int odometerKm,
            string operationalStatus,
            string? description,
            List<VehicleImageInput>? images = null)
        {
            VehicleId = vehicleId;
            CategoryId = categoryId;
            LicensePlate = licensePlate;
            Brand = brand;
            Model = model;
            ManufactureYear = manufactureYear;
            SeatCount = seatCount;
            Transmission = transmission;
            FuelType = fuelType;
            DailyRate = dailyRate;
            OdometerKm = odometerKm;
            OperationalStatus = operationalStatus;
            Description = description;
            Images = images;
        }
    }

    public class CategoryListItem
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int TotalVehicles { get; set; }
        public int ActiveVehicles { get; set; }

        public override string ToString()
        {
            return CategoryName;
        }
    }

    public class CategoryUpsertRequest
    {
        public int? CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;

        public CategoryUpsertRequest()
        {
        }

        public CategoryUpsertRequest(int? categoryId, string categoryName, string? description, bool isActive = true)
        {
            CategoryId = categoryId;
            CategoryName = categoryName;
            Description = description;
            IsActive = isActive;
        }
    }
}
