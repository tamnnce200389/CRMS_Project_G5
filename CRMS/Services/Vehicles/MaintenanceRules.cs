namespace CRMS.Services.Vehicles;

public static class MaintenanceRules
{
    public static IReadOnlyList<string> AllowedStatuses { get; } = new[]
    {
        "Scheduled",
        "InProgress",
        "Completed",
        "Cancelled"
    };

    public static string ValidateDescription(string? description)
    {
        var trimmed = (description ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new InvalidOperationException("Vui lòng nhập nội dung / hạng mục bảo trì.");

        if (trimmed.Length > 1500)
            throw new InvalidOperationException("Mô tả bảo trì không được vượt quá 1500 ký tự.");

        return trimmed;
    }

    public static decimal ValidateCost(decimal cost)
    {
        if (cost < 0m)
            throw new InvalidOperationException("Chi phí bảo trì không được nhỏ hơn 0.");

        if (decimal.Round(cost, 2) != cost)
            throw new InvalidOperationException("Chi phí bảo trì chỉ hỗ trợ tối đa 2 chữ số thập phân.");

        return cost;
    }

    public static string NormalizeStatus(string? status)
    {
        var trimmed = (status ?? string.Empty).Trim();
        var match = AllowedStatuses.FirstOrDefault(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            throw new InvalidOperationException($"Trạng thái bảo trì '{status}' không hợp lệ.");

        return match;
    }

    public static void ValidateTransition(string currentStatus, string nextStatus)
    {
        var current = NormalizeStatus(currentStatus);
        var next = NormalizeStatus(nextStatus);

        if (current is "Completed" or "Cancelled")
        {
            throw new InvalidOperationException("Phiếu bảo trì đã đóng (Đã hoàn thành / Đã hủy), không thể chỉnh sửa.");
        }

        if (current == "InProgress" && next == "Scheduled")
        {
            throw new InvalidOperationException("Không thể chuyển ngược phiếu Đang bảo trì về lại Đã lên lịch.");
        }
    }

    public static (MaintenanceScheduleRequest Normalized, DateTime StartUtc, DateTime EndUtc) ValidateSchedule(
        MaintenanceScheduleRequest request,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.VehicleId <= 0)
            throw new InvalidOperationException("Vui lòng chọn phương tiện cần bảo trì.");

        var description = ValidateDescription(request.Description);
        var cost = ValidateCost(request.Cost);

        var startUtc = TruncateToSeconds(AvailabilityRules.ToUtc(request.StartAtLocal));
        var endUtc = TruncateToSeconds(AvailabilityRules.ToUtc(request.EndAtLocal));
        var nowSec = TruncateToSeconds(nowUtc);

        if (endUtc <= startUtc)
            throw new InvalidOperationException("Thời gian kết thúc bảo trì phải sau thời gian bắt đầu.");

        if (!request.StartImmediately && startUtc <= nowSec)
        {
            throw new InvalidOperationException(
                "Lịch bảo trì đặt trước phải có thời gian bắt đầu ở tương lai (hoặc chọn Đưa vào bảo trì ngay).");
        }

        if (request.StartImmediately && endUtc <= nowSec)
        {
            throw new InvalidOperationException("Thời gian dự kiến hoàn tất bảo trì phải sau thời điểm hiện tại.");
        }

        var normalized = request with
        {
            Description = description,
            Cost = cost
        };

        return (normalized, startUtc, endUtc);
    }

    public static (MaintenanceUpdateRequest Normalized, DateTime StartUtc, DateTime EndUtc) ValidateUpdate(
        MaintenanceUpdateRequest request,
        string currentStatus,
        DateTime? nowUtc = null,
        DateTime? existingStartUtc = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MaintenanceId <= 0)
            throw new InvalidOperationException("Mã phiếu bảo trì không hợp lệ.");

        var nextStatus = NormalizeStatus(request.MaintenanceStatus);
        ValidateTransition(currentStatus, nextStatus);

        var description = ValidateDescription(request.Description);
        var cost = ValidateCost(request.Cost);

        var startUtc = TruncateToSeconds(AvailabilityRules.ToUtc(request.StartAtLocal));
        var endUtc = TruncateToSeconds(AvailabilityRules.ToUtc(request.EndAtLocal));

        if (endUtc <= startUtc)
            throw new InvalidOperationException("Thời gian kết thúc bảo trì phải sau thời gian bắt đầu.");

        if (nextStatus == "Scheduled" && nowUtc.HasValue)
        {
            var nowSec = TruncateToSeconds(nowUtc.Value);
            var startChanged = !existingStartUtc.HasValue || startUtc != TruncateToSeconds(existingStartUtc.Value);
            if (startChanged && startUtc <= nowSec)
            {
                throw new InvalidOperationException(
                    "Lịch bảo trì đặt trước phải có thời gian bắt đầu ở tương lai (hoặc chuyển sang Đang bảo trì).");
            }
        }

        var normalized = request with
        {
            Description = description,
            Cost = cost,
            MaintenanceStatus = nextStatus
        };

        return (normalized, startUtc, endUtc);
    }

    private static DateTime TruncateToSeconds(DateTime utc) =>
        new(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, DateTimeKind.Utc);
}
