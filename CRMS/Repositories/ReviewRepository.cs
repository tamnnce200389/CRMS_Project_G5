
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRMS.Data;
using CRMS.Models;
using CRMS.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Repositories;

public sealed class MyReviewItem
{
    public long ReviewId { get; set; }
    public long BookingId { get; set; }
    public byte Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsVisible { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReviewRepository
{
    // Lay CustomerId tu phien dang nhap.
    // Khong tin tuong CustomerId do giao dien tu nhap.
    private static long RequireCustomerId()
    {
        var session = CurrentUserService.Instance;

        if (!session.IsLoggedIn ||
            !session.IsCustomer ||
            !session.UserId.HasValue ||
            session.UserId.Value <= 0)
        {
            throw new UnauthorizedAccessException(
                "Bạn cần đăng nhập bằng tài khoản khách hàng.");
        }

        return session.UserId.Value;
    }

    // F22 - Danh sach danh gia cua toi
    public async Task<List<MyReviewItem>> GetMyReviewsAsync(
        long customerId)
    {
        long currentCustomerId = RequireCustomerId();

        if (customerId != currentCustomerId)
        {
            throw new UnauthorizedAccessException(
                "Bạn không có quyền xem đánh giá của khách hàng khác.");
        }

        using var db = CrmsContextFactory.Create();

        return await (
            from r in db.Reviews.AsNoTracking()
            join b in db.Bookings.AsNoTracking()
                on r.BookingId equals b.BookingId
            where b.CustomerId == currentCustomerId
            orderby r.CreatedAt descending
            select new MyReviewItem
            {
                ReviewId = r.ReviewId,
                BookingId = r.BookingId,
                Rating = r.Rating,
                Comment = r.Comment,
                IsVisible = r.IsVisible,
                CreatedAt = r.CreatedAt
            }
        ).ToListAsync();
    }

    // F22 - Lay cac don Completed chua danh gia
    public async Task<List<long>> GetReviewableBookingIdsAsync()
    {
        long customerId = RequireCustomerId();

        using var db = CrmsContextFactory.Create();

        return await db.Bookings
            .AsNoTracking()
            .Where(b =>
                b.CustomerId == customerId &&
                b.BookingStatus == "Completed" &&
                !db.Reviews.Any(r =>
                    r.BookingId == b.BookingId))
            .OrderByDescending(b => b.BookingId)
            .Select(b => b.BookingId)
            .ToListAsync();
    }

    // F22 - Gui danh gia
    public async Task AddReviewAsync(
        long customerId,
        long bookingId,
        byte rating,
        string? comment)
    {
        long currentCustomerId = RequireCustomerId();

        if (customerId != currentCustomerId)
        {
            throw new UnauthorizedAccessException(
                "Bạn không có quyền đánh giá thay khách hàng khác.");
        }

        if (bookingId <= 0)
        {
            throw new ArgumentException(
                "Mã đơn thuê không hợp lệ.");
        }

        if (rating < 1 || rating > 5)
        {
            throw new ArgumentException(
                "Số sao phải từ 1 đến 5.");
        }

        string? normalizedComment =
            string.IsNullOrWhiteSpace(comment)
                ? null
                : comment.Trim();

        if (normalizedComment != null &&
            normalizedComment.Length > 1000)
        {
            throw new ArgumentException(
                "Nội dung đánh giá không được vượt quá 1000 ký tự.");
        }

        using var db = CrmsContextFactory.Create();

        // Chi cho phep danh gia don cua chinh khach hang.
        var booking = await db.Bookings
            .AsNoTracking()
            .SingleOrDefaultAsync(b =>
                b.BookingId == bookingId &&
                b.CustomerId == currentCustomerId);

        if (booking == null)
        {
            throw new InvalidOperationException(
                "Không tìm thấy đơn thuê thuộc tài khoản của bạn.");
        }

        if (booking.BookingStatus != "Completed")
        {
            throw new InvalidOperationException(
                "Chỉ được đánh giá đơn thuê đã hoàn thành.");
        }

        bool alreadyReviewed = await db.Reviews
            .AnyAsync(r => r.BookingId == bookingId);

        if (alreadyReviewed)
        {
            throw new InvalidOperationException(
                "Đơn thuê này đã được đánh giá.");
        }

        var review = new Review
        {
            BookingId = bookingId,
            Rating = rating,
            Comment = normalizedComment,
            IsVisible = false,
            CreatedAt = DateTime.UtcNow
        };

        db.Reviews.Add(review);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sqlEx &&
                  (sqlEx.Number == 2601 ||
                   sqlEx.Number == 2627))
        {
            // UNIQUE INDEX UX_Reviews_Booking
            // ngan 2 yeu cau cung danh gia mot don.
            throw new InvalidOperationException(
                "Đơn thuê này đã được đánh giá.",
                ex);
        }
    }
}
