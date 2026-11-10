
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CRMS.Repositories;
using CRMS.Services;

namespace CRMS.Views
{
    public partial class WriteReviewWindow : Window
    {
        private readonly ReviewRepository _repository = new();

        private bool _isBusy;

        public WriteReviewWindow()
        {
            InitializeComponent();
            Loaded += WriteReviewWindow_Loaded;
        }

        private async void WriteReviewWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await LoadBookingsAsync();
        }

        private async Task LoadBookingsAsync()
        {
            SetBusy(true);

            try
            {
                var bookingIds =
                    await _repository.GetReviewableBookingIdsAsync();

                BookingComboBox.ItemsSource = bookingIds;

                // ComboBox hien thi truc tiep BookingId.
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
                    StatusTextBlock.Text =
                        "Bạn không có đơn thuê nào đủ điều kiện đánh giá.";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text =
                    "Không thể tải đơn thuê: " + ex.Message;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void Submit_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            if (BookingComboBox.SelectedItem is not long bookingId)
            {
                MessageBox.Show(
                    "Vui lòng chọn đơn thuê.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (RatingComboBox.SelectedItem is not ComboBoxItem ratingItem ||
                !byte.TryParse(ratingItem.Tag?.ToString(), out byte rating))
            {
                MessageBox.Show(
                    "Vui lòng chọn số sao.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var customerId = CurrentUserService.Instance.UserId;

            if (!customerId.HasValue)
            {
                MessageBox.Show(
                    "Vui lòng đăng nhập.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            SetBusy(true);

            try
            {
                await _repository.AddReviewAsync(
                    customerId.Value,
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
                StatusTextBlock.Text = ex.Message;

                MessageBox.Show(
                    ex.Message,
                    "Không thể gửi đánh giá",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void Cancel_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

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
