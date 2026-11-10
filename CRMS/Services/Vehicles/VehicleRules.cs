using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace CRMS.Services.Vehicles;

public static class VehicleRules
{
    private static readonly Regex PlateRegex = new(
        @"^[A-Z0-9][A-Z0-9\-\.\s]{1,18}[A-Z0-9]$",
        RegexOptions.Compiled);

    private static readonly Regex VnDotThousandsRegex = new(
        @"^[+-]?\d{1,3}(\.\d{3})+(,\d{1,2})?$",
        RegexOptions.Compiled);

    private static readonly Regex VnDecimalCommaRegex = new(
        @"^[+-]?\d+,\d{1,2}$",
        RegexOptions.Compiled);

    private static readonly Regex UsCommaThousandsRegex = new(
        @"^[+-]?\d{1,3}(,\d{3})+(\.\d+)?$",
        RegexOptions.Compiled);

    private static readonly HashSet<string> AllowedTransmissions = new(StringComparer.Ordinal)
    {
        "Automatic",
        "Manual"
    };

    private static readonly HashSet<string> AllowedFuels = new(StringComparer.Ordinal)
    {
        "Gasoline",
        "Diesel",
        "Electric",
        "Hybrid"
    };

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        "Active",
        "Suspended"
    };

    public static string NormalizeLicensePlate(string? raw)
    {
        var plate = (raw ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(plate))
            throw new InvalidOperationException("Biển số xe không được để trống.");
        if (plate.Length < 3 || plate.Length > 20)
            throw new InvalidOperationException("Biển số xe phải từ 3 đến 20 ký tự.");
        if (!PlateRegex.IsMatch(plate))
            throw new InvalidOperationException("Biển số xe chỉ được chứa chữ cái không dấu (A-Z), chữ số (0-9), dấu gạch ngang, dấu chấm hoặc khoảng trắng.");
        return plate;
    }

    public static VehicleUpsertRequest ValidateVehicle(
        VehicleUpsertRequest request,
        int? currentOdometerKm = null)
    {
        if (request is null)
            throw new InvalidOperationException("Dữ liệu xe không hợp lệ.");

        if (request.CategoryId <= 0)
            throw new InvalidOperationException("Vui lòng chọn loại xe hợp lệ.");

        var plate = NormalizeLicensePlate(request.LicensePlate);

        var brand = (request.Brand ?? "").Trim();
        if (string.IsNullOrEmpty(brand) || brand.Length > 80)
            throw new InvalidOperationException("Hãng xe bắt buộc nhập và tối đa 80 ký tự.");

        var model = (request.Model ?? "").Trim();
        if (string.IsNullOrEmpty(model) || model.Length > 100)
            throw new InvalidOperationException("Mẫu xe bắt buộc nhập và tối đa 100 ký tự.");

        if (request.ManufactureYear < 1990 || request.ManufactureYear > 2100)
            throw new InvalidOperationException("Năm sản xuất phải nằm trong khoảng 1990 đến 2100.");

        if (request.SeatCount < 1 || request.SeatCount > 30)
            throw new InvalidOperationException("Số chỗ ngồi phải từ 1 đến 30.");

        var transmission = (request.Transmission ?? "").Trim();
        if (!AllowedTransmissions.Contains(transmission))
            throw new InvalidOperationException("Hộp số chỉ chấp nhận Automatic (Số tự động) hoặc Manual (Số sàn).");

        var fuel = (request.FuelType ?? "").Trim();
        if (!AllowedFuels.Contains(fuel))
            throw new InvalidOperationException("Loại nhiên liệu không hợp lệ.");

        if (request.DailyRate <= 0m)
            throw new InvalidOperationException("Giá thuê theo ngày phải lớn hơn 0.");

        if (decimal.Round(request.DailyRate, 2) != request.DailyRate)
            throw new InvalidOperationException("Giá thuê theo ngày chỉ chấp nhận tối đa 2 chữ số thập phân.");

        if (request.OdometerKm < 0)
            throw new InvalidOperationException("Số công-tơ-mét (Odo) không được âm.");

        if (currentOdometerKm.HasValue && request.OdometerKm < currentOdometerKm.Value)
            throw new InvalidOperationException($"Số công-tơ-mét mới ({request.OdometerKm:N0} km) không được nhỏ hơn số hiện tại ({currentOdometerKm.Value:N0} km).");

        var status = (request.OperationalStatus ?? "").Trim();
        if (!AllowedStatuses.Contains(status))
            throw new InvalidOperationException("Trạng thái hoạt động chỉ chấp nhận Active hoặc Suspended.");

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        if (description != null && description.Length > 2000)
            throw new InvalidOperationException("Mô tả xe tối đa 2000 ký tự.");

        var normalizedImages = NormalizeImages(request.Images);

        return new VehicleUpsertRequest(
            VehicleId: request.VehicleId,
            CategoryId: request.CategoryId,
            LicensePlate: plate,
            Brand: brand,
            Model: model,
            ManufactureYear: request.ManufactureYear,
            SeatCount: request.SeatCount,
            Transmission: transmission,
            FuelType: fuel,
            DailyRate: request.DailyRate,
            OdometerKm: request.OdometerKm,
            OperationalStatus: status,
            Description: description,
            Images: normalizedImages);
    }

    public static IReadOnlyList<VehicleImageInput> NormalizeImages(IEnumerable<VehicleImageInput>? inputs)
    {
        if (inputs is null)
            return Array.Empty<VehicleImageInput>();

        var ordered = inputs
            .Select((item, idx) => (item, idx))
            .OrderBy(t => t.item.SortOrder)
            .ThenBy(t => t.idx)
            .Select(t => t.item)
            .ToList();

        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cleaned = new List<(string Url, bool RequestedPrimary)>();

        foreach (var img in ordered)
        {
            var normalizedUrl = NormalizeImageUrl(img.ImageUrl);
            if (!seenUrls.Add(normalizedUrl))
                continue;

            cleaned.Add((normalizedUrl, img.IsPrimary));
        }

        if (cleaned.Count == 0)
            return Array.Empty<VehicleImageInput>();

        int primaryIndex = cleaned.FindIndex(x => x.RequestedPrimary);
        if (primaryIndex < 0)
            primaryIndex = 0;

        var result = new List<VehicleImageInput>(cleaned.Count);
        for (int i = 0; i < cleaned.Count; i++)
        {
            result.Add(new VehicleImageInput(
                ImageUrl: cleaned[i].Url,
                IsPrimary: i == primaryIndex,
                SortOrder: i));
        }

        return result;
    }

    public static string NormalizeImageUrl(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (string.IsNullOrEmpty(text))
            throw new InvalidOperationException("Đường dẫn ảnh xe không được để trống.");
        if (text.Length > 1000)
            throw new InvalidOperationException("Đường dẫn ảnh xe tối đa 1000 ký tự.");

        const string bundledPrefix = "pack://application:,,,/CRMS;component/Assets/Vehicles/";
        if (text.StartsWith(bundledPrefix, StringComparison.Ordinal))
        {
            var name = text[bundledPrefix.Length..];
            if (!Regex.IsMatch(name, @"^vehicle-\d{2}-(exterior|interior)\.(jpg|png)$"))
                throw new InvalidOperationException("Đường dẫn ảnh đi kèm ứng dụng không hợp lệ.");

            using var resources = typeof(VehicleRules).Assembly.GetManifestResourceStream("CRMS.g.resources");
            if (resources is not null)
            {
                using var reader = new System.Resources.ResourceReader(resources);
                var entries = reader.GetEnumerator();
                while (entries.MoveNext())
                    if (string.Equals(entries.Key as string, "assets/vehicles/" + name, StringComparison.Ordinal))
                        return text;
            }
            throw new InvalidOperationException("Ảnh chưa được đóng gói trong ứng dụng.");
        }

        if (Path.IsPathRooted(text) && !text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(text))
                throw new InvalidOperationException($"Không tìm thấy file ảnh trên máy: {text}");

            var fileUri = new Uri(Path.GetFullPath(text)).AbsoluteUri;
            if (fileUri.Length > 1000)
                throw new InvalidOperationException("Đường dẫn file ảnh quá dài (> 1000 ký tự).");
            return fileUri;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Đường dẫn ảnh không hợp lệ (chỉ hỗ trợ URL http/https hoặc file ảnh tồn tại trên máy).");

        if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(uri.Host))
                throw new InvalidOperationException("URL ảnh thiếu tên miền hợp lệ.");
            return text;
        }

        if (uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(uri.LocalPath))
                throw new InvalidOperationException($"Không tìm thấy file ảnh cục bộ: {uri.LocalPath}");
            return uri.AbsoluteUri;
        }

        throw new InvalidOperationException("Đường dẫn ảnh chỉ chấp nhận giao thức http, https hoặc file cục bộ.");
    }

    public static CategoryUpsertRequest ValidateCategory(CategoryUpsertRequest request)
    {
        if (request is null)
            throw new InvalidOperationException("Dữ liệu loại xe không hợp lệ.");

        var name = (request.CategoryName ?? "").Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 80)
            throw new InvalidOperationException("Tên loại xe bắt buộc nhập và tối đa 80 ký tự.");

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        if (description != null && description.Length > 500)
            throw new InvalidOperationException("Mô tả loại xe tối đa 500 ký tự.");

        return new CategoryUpsertRequest(
            CategoryId: request.CategoryId,
            CategoryName: name,
            Description: description,
            IsActive: request.IsActive);
    }

    public static bool TryParseVndAmount(string? raw, out decimal amount)
    {
        amount = 0m;
        var text = (raw ?? string.Empty).Trim().Replace(" ", string.Empty);
        if (string.IsNullOrEmpty(text))
            return false;

        string normalized;
        if (VnDotThousandsRegex.IsMatch(text))
        {
            normalized = text.Replace(".", string.Empty).Replace(',', '.');
        }
        else if (VnDecimalCommaRegex.IsMatch(text))
        {
            normalized = text.Replace(',', '.');
        }
        else if (UsCommaThousandsRegex.IsMatch(text))
        {
            normalized = text.Replace(",", string.Empty);
        }
        else
        {
            normalized = text;
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out amount);
    }
}
