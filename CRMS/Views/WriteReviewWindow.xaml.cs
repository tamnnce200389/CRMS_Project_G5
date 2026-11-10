
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CRMS.Services;

namespace CRMS.Views
{
    public partial class WriteReviewWindow : Window
    {
        // F22 - Su dung Service thay cho Repository
        private readonly ReviewService _service = new();

        private bool _isBusy;

        public WriteReviewWindow()
        {
            InitializeComponent();

            Loaded += WriteReviewWindow_Loaded;
        }

        // Tu dong tai don thue khi mo cua so
        private async void WriteReviewWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await LoadBookingsAsync();
        }

        // F22 - Lay danh sach don Completed chua danh gia
        private async Task LoadBookingsAsync()
        {
            if (_isBusy)
                return;

            SetBusy(true);

            try
            {
                var bookingIds =
                    await _service.GetReviewableBookingIdsAsync();

                BookingComboBox.ItemsSource = bookingIds;

                // Hien thi truc tiep BookingId
                BookingComboBox.DisplayMemberPath = "";
                BookingComboBox.SelectedValuePath = "";

                if (bookingIds.Count > 0)
                {
                    BookingComboBox.SelectedIndex = 0;

                    StatusTextBlock.Text =
                        $"Có {bookingIds.Count} đơn thuê có thể đánh giá.";
                }
                else
                {
                    BookingComboBox.SelectedIndex = -1;

                    StatusTextBlock.Text =
                        "Bạn không có đơn thuê nào đủ điều kiện đánh giá.";
                }
            }
            catch (Exception ex)
            {
                BookingComboBox.ItemsSource = null;

                StatusTextBlock.Text =
                    "Không thể tải đơn thuê: " +
                    ex.GetBaseException().Message;
            }
            finally
            {
                SetBusy(false);
            }
        }

        // F22 - Gui danh gia
        private async void Submit_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            // Kiem tra Booking
            if (BookingComboBox.SelectedItem is not long bookingId)
            {
                MessageBox.Show(
                    "Vui lòng chọn đơn thuê.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            // Kiem tra Rating
            if (RatingComboBox.SelectedItem is not ComboBoxItem ratingItem ||
                !byte.TryParse(
                    ratingItem.Tag?.ToString(),
                    out byte rating) ||
                rating < 1 ||
                rating > 5)
            {
                MessageBox.Show(
                    "Vui lòng chọn số sao từ 1 đến 5.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            // Lay CustomerId tu phien dang nhap
            var session = CurrentUserService.Instance;

            if (!session.IsLoggedIn ||
                !session.IsCustomer ||
                !session.UserId.HasValue ||
                session.UserId.Value <= 0)
            {
                MessageBox.Show(
                    "Vui lòng đăng nhập bằng tài khoản khách hàng.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            SetBusy(true);

            try
            {
                // Goi ReviewService de luu vao SQL Server
                await _service.AddReviewAsync(
                    session.UserId.Value,
                    bookingId,
                    rating,
                    CommentTextBox.Text);

                MessageBox.Show(
                    "Gửi đánh giá thành công! Đánh giá đang chờ duyệt.",
                    "Thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                string message =
                    ex.GetBaseException().Message;

                StatusTextBlock.Text = message;

                MessageBox.Show(
                    message,
                    "Không thể gửi đánh giá",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // Dong cua so
        private void Cancel_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            Close();
        }

        // Khoa giao dien khi dang xu ly
        private void SetBusy(bool busy)
        {
            _isBusy = busy;

            SubmitButton.IsEnabled = !busy;
            BookingComboBox.IsEnabled = !busy;
            RatingComboBox.IsEnabled = !busy;
            CommentTextBox.IsEnabled = !busy;
        }
    }
}
