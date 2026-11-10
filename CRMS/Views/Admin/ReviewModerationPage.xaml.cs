
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CRMS.Services;

namespace CRMS.Views.Admin
{
    public partial class ReviewModerationPage : Page
    {
        // F23 - Su dung Service thay cho Repository
        private readonly ReviewModerationService _service = new();

        private bool _isBusy;

        public ReviewModerationPage()
        {
            InitializeComponent();

            Loaded += ReviewModerationPage_Loaded;
        }

        // Tu dong tai danh sach khi mo trang
        private async void ReviewModerationPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await LoadReviewsAsync();
        }

        // F23 - Lay danh sach danh gia
        private async Task LoadReviewsAsync()
        {
            if (_isBusy)
                return;

            SetBusy(true);

            try
            {
                StatusTextBlock.Text =
                    "Đang tải danh sách đánh giá...";

                var reviews =
                    await _service.GetAllReviewsAsync();

                ReviewsDataGrid.ItemsSource = reviews;

                StatusTextBlock.Text =
                    reviews.Count == 0
                        ? "Chưa có đánh giá nào."
                        : $"Tìm thấy {reviews.Count} đánh giá.";
            }
            catch (Exception ex)
            {
                ReviewsDataGrid.ItemsSource = null;

                StatusTextBlock.Text =
                    "Không thể tải đánh giá: " +
                    ex.GetBaseException().Message;
            }
            finally
            {
                SetBusy(false);
            }
        }

        // F23 - Lam moi danh sach
        private async void Refresh_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadReviewsAsync();
        }

        // F23 - Duyet danh gia
        private async void Approve_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            if (sender is not Button button ||
                button.Tag is not ReviewModerationItem review)
            {
                return;
            }

            if (!CheckAdmin())
                return;

            if (review.IsVisible)
            {
                MessageBox.Show(
                    "Đánh giá này đã được duyệt.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            var confirm = MessageBox.Show(
                $"Bạn có muốn duyệt đánh giá #{review.ReviewId} không?",
                "Xác nhận duyệt đánh giá",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            await UpdateReviewAsync(
                review.ReviewId,
                true);
        }

        // F23 - An danh gia
        private async void Hide_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            if (sender is not Button button ||
                button.Tag is not ReviewModerationItem review)
            {
                return;
            }

            if (!CheckAdmin())
                return;

            if (!review.IsVisible)
            {
                MessageBox.Show(
                    "Đánh giá này đang ở trạng thái ẩn hoặc chờ duyệt.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            var confirm = MessageBox.Show(
                $"Bạn có muốn ẩn đánh giá #{review.ReviewId} không?",
                "Xác nhận ẩn đánh giá",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            await UpdateReviewAsync(
                review.ReviewId,
                false);
        }

        // Kiem tra quyen Admin
        private static bool CheckAdmin()
        {
            var currentUser = CurrentUserService.Instance;

            if (currentUser.IsLoggedIn &&
                currentUser.IsAdmin &&
                currentUser.UserId.HasValue &&
                currentUser.UserId.Value > 0)
            {
                return true;
            }

            MessageBox.Show(
                "Chỉ tài khoản Admin mới được kiểm duyệt đánh giá.",
                "Không có quyền truy cập",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return false;
        }

        // Cap nhat trang thai va tai lai danh sach
        private async Task UpdateReviewAsync(
            long reviewId,
            bool approve)
        {
            if (_isBusy)
                return;

            SetBusy(true);

            bool success = false;

            try
            {
                StatusTextBlock.Text =
                    "Đang cập nhật trạng thái đánh giá...";

                if (approve)
                {
                    await _service.ApproveReviewAsync(reviewId);
                }
                else
                {
                    await _service.HideReviewAsync(reviewId);
                }

                success = true;

                MessageBox.Show(
                    approve
                        ? "Duyệt đánh giá thành công!"
                        : "Ẩn đánh giá thành công!",
                    "F23 - Review Moderation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text =
                    "Không thể cập nhật đánh giá: " +
                    ex.GetBaseException().Message;

                MessageBox.Show(
                    ex.GetBaseException().Message,
                    "Không thể cập nhật đánh giá",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }

            if (success)
            {
                await LoadReviewsAsync();
            }
        }

        // Khoa thao tac trong khi xu ly
        private void SetBusy(bool busy)
        {
            _isBusy = busy;

            RefreshButton.IsEnabled = !busy;
            ReviewsDataGrid.IsEnabled = !busy;
        }
    }
}
