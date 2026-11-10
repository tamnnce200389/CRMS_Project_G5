
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRMS.Data;
using CRMS.Services;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Repositories
{
    // Du lieu hien thi tren giao dien Admin
    public sealed class ReviewModerationItem
    {
        public long ReviewId { get; set; }

        public long BookingId { get; set; }

        public long CustomerId { get; set; }

        public byte Rating { get; set; }

        public string? Comment { get; set; }

        public bool IsVisible { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class ReviewModerationRepository
    {
        // Kiem tra quyen Admin
        private static void RequireAdmin()
        {
            var currentUser = CurrentUserService.Instance;

            if (!currentUser.IsLoggedIn ||
                !currentUser.IsAdmin ||
                !currentUser.UserId.HasValue ||
                currentUser.UserId.Value <= 0)
            {
                throw new UnauthorizedAccessException(
                    "Chỉ tài khoản Admin mới được kiểm duyệt đánh giá.");
            }
        }

        // F23 - Lay tat ca danh gia
        public async Task<List<ReviewModerationItem>>
            GetAllReviewsAsync()
        {
            RequireAdmin();

            using var db = CrmsContextFactory.Create();

            return await (
                from review in db.Reviews.AsNoTracking()
                join booking in db.Bookings.AsNoTracking()
                    on review.BookingId equals booking.BookingId
                orderby review.CreatedAt descending
                select new ReviewModerationItem
                {
                    ReviewId = review.ReviewId,
                    BookingId = review.BookingId,
                    CustomerId = booking.CustomerId,
                    Rating = review.Rating,
                    Comment = review.Comment,
                    IsVisible = review.IsVisible,
                    CreatedAt = review.CreatedAt
                }
            ).ToListAsync();
        }

        // F23 - Duyet danh gia
        public async Task ApproveReviewAsync(long reviewId)
        {
            await SetReviewVisibilityAsync(reviewId, true);
        }

        // F23 - An danh gia
        public async Task HideReviewAsync(long reviewId)
        {
            await SetReviewVisibilityAsync(reviewId, false);
        }

        // Xu ly chung cho Duyet va An
        private async Task SetReviewVisibilityAsync(
            long reviewId,
            bool isVisible)
        {
            RequireAdmin();

            if (reviewId <= 0)
            {
                throw new ArgumentException(
                    "Mã đánh giá không hợp lệ.");
            }

            using var db = CrmsContextFactory.Create();

            var review = await db.Reviews
                .SingleOrDefaultAsync(r =>
                    r.ReviewId == reviewId);

            if (review == null)
            {
                throw new InvalidOperationException(
                    "Không tìm thấy đánh giá cần kiểm duyệt.");
            }

            // Khong cap nhat neu trang thai da dung
            if (review.IsVisible == isVisible)
            {
                return;
            }

            review.IsVisible = isVisible;

            // Kiem tra lai phien Admin truoc khi ghi
            RequireAdmin();

            await db.SaveChangesAsync();
        }
    }
}
