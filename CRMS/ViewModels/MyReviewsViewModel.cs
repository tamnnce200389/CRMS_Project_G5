
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services;

namespace CRMS.ViewModels
{
    public sealed class MyReviewsViewModel : BaseViewModel
    {
        // F22 - Su dung Service thay cho Repository
        private readonly ReviewService _service = new();

        private bool _isBusy;

        private string _status =
            "Chưa tải danh sách đánh giá.";

        // Danh sach danh gia cua khach hang
        public ObservableCollection<MyReviewItem> Reviews { get; }
            = new();

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value)
                    return;

                _isBusy = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string Status
        {
            get => _status;
            private set
            {
                if (_status == value)
                    return;

                _status = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshCommand { get; }

        public MyReviewsViewModel()
        {
            RefreshCommand = new RelayCommand(
                async _ => await LoadAsync(),
                _ => !IsBusy
            );
        }

        // F22 - Tai danh sach danh gia cua Customer
        public async Task LoadAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                var session = CurrentUserService.Instance;

                if (!session.IsLoggedIn ||
                    !session.IsCustomer ||
                    !session.UserId.HasValue ||
                    session.UserId.Value <= 0)
                {
                    Reviews.Clear();

                    Status =
                        "Vui lòng đăng nhập bằng tài khoản khách hàng.";

                    return;
                }

                Status = "Đang tải danh sách đánh giá...";

                // Goi ReviewService thay cho ReviewRepository
                var items = await _service.GetMyReviewsAsync(
                    session.UserId.Value
                );

                Reviews.Clear();

                foreach (var item in items)
                {
                    Reviews.Add(item);
                }

                Status = Reviews.Count == 0
                    ? "Bạn chưa có đánh giá nào."
                    : $"Tìm thấy {Reviews.Count} đánh giá.";
            }
            catch (Exception ex)
            {
                Reviews.Clear();

                Status =
                    "Không thể tải đánh giá: " +
                    ex.GetBaseException().Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
