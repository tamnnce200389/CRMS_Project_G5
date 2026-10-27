
using CRMS.Data;
using CRMS.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRMS.Services
{
    public class VehicleAvailabilityService
    {
        // SQL Server cua CRMS luu ngay gio theo UTC.
        // Gio khach chon tren WPF la gio Viet Nam.
        private static readonly TimeZoneInfo VietnamTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById(
                "SE Asia Standard Time");

        // ========================================
        // 1. CHUYEN GIO VIET NAM SANG UTC
        // ========================================
        private static DateTime ToUtc(DateTime localTime)
        {
            DateTime vietnamTime = DateTime.SpecifyKind(
                localTime,
                DateTimeKind.Unspecified);

            return TimeZoneInfo.ConvertTimeToUtc(
                vietnamTime,
                VietnamTimeZone);
        }

        // ========================================
        // 2. KIEM TRA KHOANG THOI GIAN
        // ========================================
        private static (DateTime StartUtc, DateTime EndUtc)
            ValidatePeriod(
                DateTime startLocal,
                DateTime endLocal)
        {
            DateTime startUtc = ToUtc(startLocal);
            DateTime endUtc = ToUtc(endLocal);

            if (endUtc <= startUtc)
            {
                throw new ArgumentException(
                    "Thời gian trả xe phải sau thời gian nhận xe.");
            }

            if (startUtc <= DateTime.UtcNow)
            {
                throw new ArgumentException(
                    "Thời gian nhận xe phải ở tương lai.");
            }

            return (startUtc, endUtc);
        }

        // ========================================
        // 3. TRUY VAN XE CON TRONG
        // ========================================
        private static IQueryable<Vehicle> BuildAvailableQuery(
            CrmsContext context,
            DateTime startUtc,
            DateTime endUtc,
            DateTime nowUtc)
        {
            return context.Vehicles
                .AsNoTracking()

                // Chi hien thi xe hoat dong
                .Where(v =>
                    v.OperationalStatus == "Active")

                // Loai xe bi trung Booking
                .Where(v =>
                    !context.Bookings.Any(b =>
                        b.VehicleId == v.VehicleId &&
                        (
                            // Xe dang su dung:
                            // Van bi chiem dung den khi tra xe
                            (
                                b.BookingStatus == "InUse" &&
                                b.StartAt < endUtc
                            )

                            ||

                            // Booking da xac nhan
                            (
                                b.BookingStatus == "Confirmed" &&
                                b.StartAt < endUtc &&
                                b.EndAt > startUtc
                            )

                            ||

                            // Don cho coc chua het han
                            (
                                b.BookingStatus == "PendingDeposit" &&
                                b.DepositDeadline > nowUtc &&
                                b.StartAt < endUtc &&
                                b.EndAt > startUtc
                            )
                        )
                    ))

                // Loai xe trung lich bao tri
                .Where(v =>
                    !context.MaintenanceRecords.Any(m =>
                        m.VehicleId == v.VehicleId &&
                        m.StartAt < endUtc &&
                        m.EndAt > startUtc &&
                        (
                            m.MaintenanceStatus == "Scheduled" ||
                            m.MaintenanceStatus == "InProgress"
                        )
                    ));
        }

        // ========================================
        // 4. LAY DANH SACH ID XE CON TRONG
        // ========================================
        public async Task<HashSet<long>>
            GetAvailableVehicleIdsAsync(
                DateTime startLocal,
                DateTime endLocal)
        {
            var (startUtc, endUtc) =
                ValidatePeriod(startLocal, endLocal);

            using var context =
                CrmsContextFactory.Create();

            DateTime nowUtc = DateTime.UtcNow;

            List<long> vehicleIds =
                await BuildAvailableQuery(
                    context,
                    startUtc,
                    endUtc,
                    nowUtc)
                .Select(v => v.VehicleId)
                .ToListAsync();

            return vehicleIds.ToHashSet();
        }

        // ========================================
        // 5. KIEM TRA MOT XE CO CON TRONG KHONG
        // ========================================
        public async Task<bool> IsAvailableAsync(
            long vehicleId,
            DateTime startLocal,
            DateTime endLocal)
        {
            var (startUtc, endUtc) =
                ValidatePeriod(startLocal, endLocal);

            using var context =
                CrmsContextFactory.Create();

            DateTime nowUtc = DateTime.UtcNow;

            return await BuildAvailableQuery(
                    context,
                    startUtc,
                    endUtc,
                    nowUtc)
                .AnyAsync(v =>
                    v.VehicleId == vehicleId);
        }
    }
}
