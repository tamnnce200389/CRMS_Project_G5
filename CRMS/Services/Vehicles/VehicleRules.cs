using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CRMS.Services.Vehicles
{
    public static class VehicleRules
    {
        private static readonly string[] AllowedTransmissions = { "Automatic", "Manual" };
        private static readonly string[] AllowedFuels = { "Gasoline", "Diesel", "Electric", "Hybrid" };
        private static readonly string[] AllowedStatuses = { "Active", "Suspended" };

        public static string NormalizeLicensePlate(string? raw)
        {
            string plate = (raw ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(plate))
            {
                throw new InvalidOperationException("Biển số xe không được để trống.");
            }

            if (plate.Length < 3 || plate.Length > 20)
            {
                throw new InvalidOperationException("Biển số xe phải từ 3 đến 20 ký tự.");
            }

            if (!IsAsciiLetterOrDigit(plate[0]) || !IsAsciiLetterOrDigit(plate[plate.Length - 1]))
            {
                throw new InvalidOperationException("Biển số xe chỉ được chứa chữ cái không dấu (A-Z), chữ số (0-9), dấu gạch ngang, dấu chấm hoặc khoảng trắng.");
            }

            for (int i = 0; i < plate.Length; i++)
            {
                char c = plate[i];
                bool validChar = IsAsciiLetterOrDigit(c) || c == '-' || c == '.' || c == ' ';
                if (!validChar)
                {
                    throw new InvalidOperationException("Biển số xe chỉ được chứa chữ cái không dấu (A-Z), chữ số (0-9), dấu gạch ngang, dấu chấm hoặc khoảng trắng.");
                }
            }

            return plate;
        }

        public static VehicleUpsertRequest ValidateVehicle(
            VehicleUpsertRequest request,
            int? currentOdometerKm = null)
        {
            if (request == null)
            {
                throw new InvalidOperationException("Dữ liệu xe không hợp lệ.");
            }

            if (request.CategoryId <= 0)
            {
                throw new InvalidOperationException("Vui lòng chọn loại xe hợp lệ.");
            }

            string plate = NormalizeLicensePlate(request.LicensePlate);

            string brand = (request.Brand ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(brand) || brand.Length > 80)
            {
                throw new InvalidOperationException("Hãng xe bắt buộc nhập và tối đa 80 ký tự.");
            }

            string model = (request.Model ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(model) || model.Length > 100)
            {
                throw new InvalidOperationException("Mẫu xe bắt buộc nhập và tối đa 100 ký tự.");
            }

            if (request.ManufactureYear < 1990 || request.ManufactureYear > 2100)
            {
                throw new InvalidOperationException("Năm sản xuất phải nằm trong khoảng 1990 đến 2100.");
            }

            if (request.SeatCount < 1 || request.SeatCount > 30)
            {
                throw new InvalidOperationException("Số chỗ ngồi phải từ 1 đến 30.");
            }

            string transmission = (request.Transmission ?? string.Empty).Trim();
            if (!AllowedTransmissions.Contains(transmission))
            {
                throw new InvalidOperationException("Hộp số chỉ chấp nhận Automatic (Số tự động) hoặc Manual (Số sàn).");
            }

            string fuel = (request.FuelType ?? string.Empty).Trim();
            if (!AllowedFuels.Contains(fuel))
            {
                throw new InvalidOperationException("Loại nhiên liệu không hợp lệ.");
            }

            if (request.DailyRate <= 0m)
            {
                throw new InvalidOperationException("Giá thuê theo ngày phải lớn hơn 0.");
            }

            if (decimal.Round(request.DailyRate, 2) != request.DailyRate)
            {
                throw new InvalidOperationException("Giá thuê theo ngày chỉ chấp nhận tối đa 2 chữ số thập phân.");
            }

            if (request.OdometerKm < 0)
            {
                throw new InvalidOperationException("Số công-tơ-mét (Odo) không được âm.");
            }

            if (currentOdometerKm.HasValue && request.OdometerKm < currentOdometerKm.Value)
            {
                throw new InvalidOperationException($"Số công-tơ-mét mới ({request.OdometerKm:N0} km) không được nhỏ hơn số hiện tại ({currentOdometerKm.Value:N0} km).");
            }

            string status = (request.OperationalStatus ?? string.Empty).Trim();
            if (!AllowedStatuses.Contains(status))
            {
                throw new InvalidOperationException("Trạng thái hoạt động chỉ chấp nhận Active hoặc Suspended.");
            }

            string? description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();
            if (description != null && description.Length > 2000)
            {
                throw new InvalidOperationException("Mô tả xe tối đa 2000 ký tự.");
            }

            List<VehicleImageInput> normalizedImages = NormalizeImages(request.Images);

            return new VehicleUpsertRequest(
                request.VehicleId,
                request.CategoryId,
                plate,
                brand,
                model,
                request.ManufactureYear,
                request.SeatCount,
                transmission,
                fuel,
                request.DailyRate,
                request.OdometerKm,
                status,
                description,
                normalizedImages);
        }

        public static List<VehicleImageInput> NormalizeImages(IEnumerable<VehicleImageInput>? inputs)
        {
            var result = new List<VehicleImageInput>();
            if (inputs == null)
            {
                return result;
            }

            List<VehicleImageInput> ordered = inputs
                .OrderBy(item => item.SortOrder)
                .ToList();

            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cleaned = new List<VehicleImageInput>();

            foreach (VehicleImageInput img in ordered)
            {
                string normalizedUrl = NormalizeImageUrl(img.ImageUrl);
                if (!seenUrls.Add(normalizedUrl))
                {
                    continue;
                }

                cleaned.Add(new VehicleImageInput(normalizedUrl, img.IsPrimary, 0));
            }

            if (cleaned.Count == 0)
            {
                return result;
            }

            int primaryIndex = -1;
            for (int i = 0; i < cleaned.Count; i++)
            {
                if (cleaned[i].IsPrimary)
                {
                    primaryIndex = i;
                    break;
                }
            }

            if (primaryIndex < 0)
            {
                primaryIndex = 0;
            }

            for (int i = 0; i < cleaned.Count; i++)
            {
                result.Add(new VehicleImageInput(
                    cleaned[i].ImageUrl,
                    i == primaryIndex,
                    i));
            }

            return result;
        }

        public static string NormalizeImageUrl(string? raw)
        {
            string text = (raw ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                throw new InvalidOperationException("Đường dẫn ảnh xe không được để trống.");
            }

            if (text.Length > 1000)
            {
                throw new InvalidOperationException("Đường dẫn ảnh xe tối đa 1000 ký tự.");
            }

            const string bundledPrefix = "pack://application:,,,/CRMS;component/Assets/Vehicles/";
            if (text.StartsWith(bundledPrefix, StringComparison.Ordinal))
            {
                string fileName = text.Substring(bundledPrefix.Length);
                if (!IsValidBundledVehicleAsset(fileName))
                {
                    throw new InvalidOperationException("Đường dẫn ảnh đi kèm ứng dụng không hợp lệ.");
                }

                return text;
            }

            if (Path.IsPathRooted(text) &&
                !text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(text))
                {
                    throw new InvalidOperationException($"Không tìm thấy file ảnh trên máy: {text}");
                }

                string fileUri = new Uri(Path.GetFullPath(text)).AbsoluteUri;
                if (fileUri.Length > 1000)
                {
                    throw new InvalidOperationException("Đường dẫn file ảnh quá dài (> 1000 ký tự).");
                }

                return fileUri;
            }

            if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) || uri == null)
            {
                throw new InvalidOperationException("Đường dẫn ảnh không hợp lệ (chỉ hỗ trợ URL http/https hoặc file ảnh tồn tại trên máy).");
            }

            if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(uri.Host))
                {
                    throw new InvalidOperationException("URL ảnh thiếu tên miền hợp lệ.");
                }

                return text;
            }

            if (uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(uri.LocalPath))
                {
                    throw new InvalidOperationException($"Không tìm thấy file ảnh cục bộ: {uri.LocalPath}");
                }

                return uri.AbsoluteUri;
            }

            throw new InvalidOperationException("Đường dẫn ảnh chỉ chấp nhận giao thức http, https hoặc file cục bộ.");
        }

        public static CategoryUpsertRequest ValidateCategory(CategoryUpsertRequest request)
        {
            if (request == null)
            {
                throw new InvalidOperationException("Dữ liệu loại xe không hợp lệ.");
            }

            string name = (request.CategoryName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 80)
            {
                throw new InvalidOperationException("Tên loại xe bắt buộc nhập và tối đa 80 ký tự.");
            }

            string? description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();
            if (description != null && description.Length > 500)
            {
                throw new InvalidOperationException("Mô tả loại xe tối đa 500 ký tự.");
            }

            return new CategoryUpsertRequest(
                request.CategoryId,
                name,
                description,
                request.IsActive);
        }

        public static bool TryParseVndAmount(string? raw, out decimal amount)
        {
            amount = 0m;
            string text = (raw ?? string.Empty).Trim().Replace(" ", string.Empty);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string sign = string.Empty;
            string body = text;
            if (body[0] == '+' || body[0] == '-')
            {
                sign = body[0] == '-' ? "-" : string.Empty;
                body = body.Substring(1);
            }

            if (string.IsNullOrEmpty(body))
            {
                return false;
            }

            bool hasDot = body.Contains('.');
            bool hasComma = body.Contains(',');
            string normalized;

            if (hasDot && hasComma)
            {
                int lastDot = body.LastIndexOf('.');
                int lastComma = body.LastIndexOf(',');

                if (lastComma > lastDot)
                {
                    // Định dạng Việt Nam có phần thập phân: 1.250.000,50
                    string[] parts = body.Split(',');
                    if (parts.Length != 2 || parts[1].Length < 1 || parts[1].Length > 2 || !IsAllDigits(parts[1]))
                    {
                        return false;
                    }

                    string[] groups = parts[0].Split('.');
                    bool validVnGroups = IsThousandsGrouped(groups) &&
                        (groups.Length >= 3 || (groups.Length == 2 && groups[1].EndsWith("00", StringComparison.Ordinal)));
                    if (!validVnGroups)
                    {
                        return false;
                    }

                    normalized = sign + parts[0].Replace(".", string.Empty) + "." + parts[1];
                }
                else
                {
                    // Định dạng quốc tế: 850,000.50
                    string[] parts = body.Split('.');
                    if (parts.Length != 2 || parts[1].Length < 1 || parts[1].Length > 2 || !IsAllDigits(parts[1]))
                    {
                        return false;
                    }

                    string[] groups = parts[0].Split(',');
                    if (!IsThousandsGrouped(groups))
                    {
                        return false;
                    }

                    normalized = sign + parts[0].Replace(",", string.Empty) + "." + parts[1];
                }
            }
            else if (hasDot)
            {
                string[] parts = body.Split('.');
                if (parts.Length >= 3 && IsThousandsGrouped(parts))
                {
                    normalized = sign + body.Replace(".", string.Empty);
                }
                else if (parts.Length == 2)
                {
                    if (parts[1].Length >= 1 && parts[1].Length <= 2 && IsAllDigits(parts[0]) && IsAllDigits(parts[1]))
                    {
                        normalized = sign + body;
                    }
                    else if (IsThousandsGrouped(parts) && parts[1].EndsWith("00", StringComparison.Ordinal))
                    {
                        normalized = sign + parts[0] + parts[1];
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            else if (hasComma)
            {
                string[] parts = body.Split(',');
                if (parts.Length == 2 && parts[1].Length >= 1 && parts[1].Length <= 2 && IsAllDigits(parts[0]) && IsAllDigits(parts[1]))
                {
                    normalized = sign + parts[0] + "." + parts[1];
                }
                else if (IsThousandsGrouped(parts))
                {
                    normalized = sign + body.Replace(",", string.Empty);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if (!IsAllDigits(body))
                {
                    return false;
                }

                normalized = sign + body;
            }

            return decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out amount);
        }

        private static bool IsAsciiLetterOrDigit(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
        }

        private static bool IsAllDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] < '0' || value[i] > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsThousandsGrouped(string[] groups)
        {
            if (groups.Length < 2)
            {
                return false;
            }

            string head = groups[0];
            if (head.Length < 1 || head.Length > 3 || head[0] == '0' || !IsAllDigits(head))
            {
                return false;
            }

            for (int i = 1; i < groups.Length; i++)
            {
                if (groups[i].Length != 3 || !IsAllDigits(groups[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidBundledVehicleAsset(string fileName)
        {
            if (!fileName.StartsWith("vehicle-", StringComparison.Ordinal))
            {
                return false;
            }

            bool validExtension = fileName.EndsWith(".jpg", StringComparison.Ordinal) ||
                                  fileName.EndsWith(".png", StringComparison.Ordinal);
            if (!validExtension)
            {
                return false;
            }

            string withoutExt = Path.GetFileNameWithoutExtension(fileName);
            string numberPart;
            if (withoutExt.EndsWith("-exterior", StringComparison.Ordinal))
            {
                numberPart = withoutExt.Substring("vehicle-".Length, withoutExt.Length - "vehicle-".Length - "-exterior".Length);
            }
            else if (withoutExt.EndsWith("-interior", StringComparison.Ordinal))
            {
                numberPart = withoutExt.Substring("vehicle-".Length, withoutExt.Length - "vehicle-".Length - "-interior".Length);
            }
            else
            {
                return false;
            }

            return numberPart.Length == 2 && IsAllDigits(numberPart);
        }
    }
}
