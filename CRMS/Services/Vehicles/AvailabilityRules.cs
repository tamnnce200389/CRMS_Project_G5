using System;
using System.Collections.Generic;
using System.Linq;

namespace CRMS.Services.Vehicles
{
    public static class AvailabilityRules
    {
        private static readonly TimeZoneInfo VietnamTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        public static DateTime ToUtc(DateTime input)
        {
            if (input.Kind == DateTimeKind.Utc)
            {
                return input;
            }

            DateTime vietnamLocal = DateTime.SpecifyKind(input, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(vietnamLocal, VietnamTimeZone);
        }

        public static DateTime ToVietnamLocal(DateTime utcInput)
        {
            DateTime utc = utcInput.Kind == DateTimeKind.Utc
                ? utcInput
                : DateTime.SpecifyKind(utcInput, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone);
        }

        public static (DateTime StartUtc, DateTime EndUtc, DateTime StartLocal, DateTime EndLocal) ValidatePeriod(
            DateTime startInput,
            DateTime endInput,
            DateTime nowUtc,
            bool requireFutureStart)
        {
            DateTime startUtc = ToUtc(startInput);
            DateTime endUtc = ToUtc(endInput);

            if (endUtc <= startUtc)
            {
                throw new ArgumentException("Thời gian trả xe phải sau thời gian nhận xe.");
            }

            if (requireFutureStart && startUtc <= nowUtc)
            {
                throw new ArgumentException("Thời gian nhận xe phải ở tương lai.");
            }

            return (startUtc, endUtc, ToVietnamLocal(startUtc), ToVietnamLocal(endUtc));
        }

        /// <summary>
        /// Half-open interval intersection check for [existingStartUtc, existingEndUtc) and [queryStartUtc, queryEndUtc).
        /// Adjacent intervals such as [10:00, 12:00) and [12:00, 14:00) return false.
        /// </summary>
        public static bool OverlapsHalfOpen(
            DateTime existingStartUtc,
            DateTime existingEndUtc,
            DateTime queryStartUtc,
            DateTime queryEndUtc)
        {
            return existingStartUtc < queryEndUtc && existingEndUtc > queryStartUtc;
        }

        public static List<AvailabilityConflict> EvaluateConflicts(
            string operationalStatus,
            bool isCategoryActive,
            string categoryName,
            IEnumerable<BookingConflictSnapshot> bookings,
            IEnumerable<MaintenanceConflictSnapshot> maintenances,
            DateTime queryStartUtc,
            DateTime queryEndUtc,
            DateTime nowUtc,
            bool includeInternalReferences)
        {
            var conflicts = new List<AvailabilityConflict>();

            if (!string.Equals(operationalStatus, "Active", StringComparison.OrdinalIgnoreCase))
            {
                conflicts.Add(new AvailabilityConflict(
                    AvailabilityConflictType.VehicleSuspended,
                    "Xe tạm ngưng khai thác",
                    "Phương tiện đang ở trạng thái Tạm ngưng (Suspended), không nhận lịch đặt mới."));
            }

            if (!isCategoryActive)
            {
                conflicts.Add(new AvailabilityConflict(
                    AvailabilityConflictType.CategoryInactive,
                    "Loại xe ngừng hoạt động",
                    $"Danh mục loại xe '{categoryName}' đang tạm ngưng hoạt động."));
            }

            foreach (BookingConflictSnapshot b in bookings.OrderBy(x => x.StartAtUtc))
            {
                DateTime bStartLocal = ToVietnamLocal(b.StartAtUtc);
                DateTime bEndLocal = ToVietnamLocal(b.EndAtUtc);
                string? refCode = includeInternalReferences ? b.BookingCode : null;

                if (string.Equals(b.BookingStatus, "InUse", StringComparison.OrdinalIgnoreCase) &&
                    b.StartAtUtc < queryEndUtc)
                {
                    string reason = b.EndAtUtc <= queryStartUtc
                        ? $"Xe đang được khách sử dụng chưa hoàn trả (quá giờ trả dự kiến {bEndLocal:dd/MM/yyyy HH:mm})."
                        : $"Xe đang trong chuyến thuê ({bStartLocal:dd/MM/yyyy HH:mm} → {bEndLocal:dd/MM/yyyy HH:mm}).";

                    conflicts.Add(new AvailabilityConflict(
                        AvailabilityConflictType.BookingInUse,
                        "Đơn đang thuê (Chưa trả xe)",
                        reason,
                        bStartLocal,
                        bEndLocal,
                        refCode));
                }
                else if (string.Equals(b.BookingStatus, "Confirmed", StringComparison.OrdinalIgnoreCase) &&
                         OverlapsHalfOpen(b.StartAtUtc, b.EndAtUtc, queryStartUtc, queryEndUtc))
                {
                    conflicts.Add(new AvailabilityConflict(
                        AvailabilityConflictType.BookingConfirmed,
                        "Đơn thuê đã xác nhận cọc",
                        $"Trùng lịch đơn đã xác nhận ({bStartLocal:dd/MM/yyyy HH:mm} → {bEndLocal:dd/MM/yyyy HH:mm}).",
                        bStartLocal,
                        bEndLocal,
                        refCode));
                }
                else if (string.Equals(b.BookingStatus, "PendingDeposit", StringComparison.OrdinalIgnoreCase) &&
                         b.DepositDeadlineUtc > nowUtc &&
                         OverlapsHalfOpen(b.StartAtUtc, b.EndAtUtc, queryStartUtc, queryEndUtc))
                {
                    DateTime holdLocal = ToVietnamLocal(b.DepositDeadlineUtc);
                    conflicts.Add(new AvailabilityConflict(
                        AvailabilityConflictType.BookingPendingDeposit,
                        "Đang giữ chỗ chờ đặt cọc",
                        $"Trùng đơn giữ chỗ chờ thanh toán cọc đến {holdLocal:dd/MM/yyyy HH:mm} ({bStartLocal:dd/MM/yyyy HH:mm} → {bEndLocal:dd/MM/yyyy HH:mm}).",
                        bStartLocal,
                        bEndLocal,
                        refCode));
                }
            }

            foreach (MaintenanceConflictSnapshot m in maintenances.OrderBy(x => x.StartAtUtc))
            {
                bool isScheduled = string.Equals(m.MaintenanceStatus, "Scheduled", StringComparison.OrdinalIgnoreCase);
                bool isInProgress = string.Equals(m.MaintenanceStatus, "InProgress", StringComparison.OrdinalIgnoreCase);

                if ((isScheduled || isInProgress) &&
                    OverlapsHalfOpen(m.StartAtUtc, m.EndAtUtc, queryStartUtc, queryEndUtc))
                {
                    DateTime mStartLocal = ToVietnamLocal(m.StartAtUtc);
                    DateTime mEndLocal = ToVietnamLocal(m.EndAtUtc);
                    string statusVi = isInProgress ? "Đang thực hiện" : "Đã lên lịch";
                    string descPart = string.IsNullOrWhiteSpace(m.Description)
                        ? string.Empty
                        : $" · {m.Description.Trim()}";
                    string? refCode = includeInternalReferences ? $"BT-#{m.MaintenanceId}" : null;

                    conflicts.Add(new AvailabilityConflict(
                        AvailabilityConflictType.Maintenance,
                        $"Lịch bảo trì ({statusVi})",
                        $"Trùng lịch bảo trì {statusVi} ({mStartLocal:dd/MM/yyyy HH:mm} → {mEndLocal:dd/MM/yyyy HH:mm}{descPart}).",
                        mStartLocal,
                        mEndLocal,
                        refCode));
                }
            }

            return conflicts;
        }
    }
}
