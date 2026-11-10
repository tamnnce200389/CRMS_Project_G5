using System;
using System.Linq;

namespace CRMS.Services.Vehicles
{
    public static class MaintenanceRules
    {
        public static readonly string[] AllowedStatuses =
        {
            "Scheduled",
            "InProgress",
            "Completed",
            "Cancelled"
        };

        public static string ValidateDescription(string? description)
        {
            string trimmed = (description ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                throw new InvalidOperationException("Vui lòng nhập nội dung / hạng mục bảo trì.");
            }

            if (trimmed.Length > 1500)
            {
                throw new InvalidOperationException("Mô tả bảo trì không được vượt quá 1500 ký tự.");
            }

            return trimmed;
        }

        public static decimal ValidateCost(decimal cost)
        {
            if (cost < 0m)
            {
                throw new InvalidOperationException("Chi phí bảo trì không được nhỏ hơn 0.");
            }

            if (decimal.Round(cost, 2) != cost)
            {
                throw new InvalidOperationException("Chi phí bảo trì chỉ hỗ trợ tối đa 2 chữ số thập phân.");
            }

            return cost;
        }

        public static string NormalizeStatus(string? status)
        {
            string trimmed = (status ?? string.Empty).Trim();
            string? match = AllowedStatuses.FirstOrDefault(s =>
                string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                throw new InvalidOperationException($"Trạng thái bảo trì '{status}' không hợp lệ.");
            }

            return match;
        }

        public static void ValidateTransition(string currentStatus, string nextStatus)
        {
            string current = NormalizeStatus(currentStatus);
            string next = NormalizeStatus(nextStatus);

            if (current == "Completed" || current == "Cancelled")
            {
                throw new InvalidOperationException("Phiếu bảo trì đã đóng (Đã hoàn thành / Đã hủy), không thể chỉnh sửa.");
            }

            if (current == "InProgress" && next == "Scheduled")
            {
                throw new InvalidOperationException("Không thể chuyển ngược phiếu Đang bảo trì về lại Đã lên lịch.");
            }
        }

        public static MaintenanceScheduleRequest ValidateSchedule(
            MaintenanceScheduleRequest request,
            DateTime nowUtc,
            out DateTime startUtc,
            out DateTime endUtc)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.VehicleId <= 0)
            {
                throw new InvalidOperationException("Vui lòng chọn phương tiện cần bảo trì.");
            }

            string description = ValidateDescription(request.Description);
            decimal cost = ValidateCost(request.Cost);

            startUtc = TruncateToSeconds(AvailabilityRules.ToUtc(request.StartAtLocal));
            endUtc = TruncateToSeconds(AvailabilityRules.ToUtc(request.EndAtLocal));
            DateTime nowSec = TruncateToSeconds(nowUtc);

            if (endUtc <= startUtc)
            {
                throw new InvalidOperationException("Thời gian kết thúc bảo trì phải sau thời gian bắt đầu.");
            }

            if (!request.StartImmediately && startUtc <= nowSec)
            {
                throw new InvalidOperationException(
                    "Lịch bảo trì đặt trước phải có thời gian bắt đầu ở tương lai (hoặc chọn Đưa vào bảo trì ngay).");
            }

            if (request.StartImmediately && endUtc <= nowSec)
            {
                throw new InvalidOperationException("Thời gian dự kiến hoàn tất bảo trì phải sau thời điểm hiện tại.");
            }

            return new MaintenanceScheduleRequest(
                request.VehicleId,
                request.StartAtLocal,
                request.EndAtLocal,
                description,
                cost,
                request.StartImmediately);
        }

        public static MaintenanceUpdateRequest ValidateUpdate(
            MaintenanceUpdateRequest request,
            string currentStatus,
            DateTime? nowUtc,
            DateTime? existingStartUtc,
            out DateTime startUtc,
            out DateTime endUtc)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.MaintenanceId <= 0)
            {
                throw new InvalidOperationException("Mã phiếu bảo trì không hợp lệ.");
            }

            string nextStatus = NormalizeStatus(request.MaintenanceStatus);
            ValidateTransition(currentStatus, nextStatus);

            string description = ValidateDescription(request.Description);
            decimal cost = ValidateCost(request.Cost);

            startUtc = TruncateToSeconds(AvailabilityRules.ToUtc(request.StartAtLocal));
            endUtc = TruncateToSeconds(AvailabilityRules.ToUtc(request.EndAtLocal));

            if (endUtc <= startUtc)
            {
                throw new InvalidOperationException("Thời gian kết thúc bảo trì phải sau thời gian bắt đầu.");
            }

            if (nextStatus == "Scheduled" && nowUtc.HasValue)
            {
                DateTime nowSec = TruncateToSeconds(nowUtc.Value);
                bool startChanged = !existingStartUtc.HasValue || startUtc != TruncateToSeconds(existingStartUtc.Value);
                if (startChanged && startUtc <= nowSec)
                {
                    throw new InvalidOperationException(
                        "Lịch bảo trì đặt trước phải có thời gian bắt đầu ở tương lai (hoặc chuyển sang Đang bảo trì).");
                }
            }

            return new MaintenanceUpdateRequest(
                request.MaintenanceId,
                request.StartAtLocal,
                request.EndAtLocal,
                description,
                cost,
                nextStatus);
        }

        private static DateTime TruncateToSeconds(DateTime utc)
        {
            return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, DateTimeKind.Utc);
        }
    }
}
