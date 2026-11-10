
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Repositories;
using CRMS.Services;

namespace CRMS.ViewModels;

public sealed class MyReviewsViewModel : BaseViewModel
{
    private readonly ReviewRepository _repository = new();

    private bool _isBusy;
    private string _status = "Chưa tải danh sách đánh giá.";

    public ObservableCollection<MyReviewItem> Reviews { get; }
        = new();

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
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

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;

        try
        {
            long? customerId =
                CurrentUserService.Instance.UserId;

            if (!customerId.HasValue || customerId.Value <= 0)
            {
                Status = "Vui lòng đăng nhập để xem đánh giá.";
                Reviews.Clear();
                return;
            }

            Status = "Đang tải danh sách đánh giá...";

            var items = await _repository.GetMyReviewsAsync(
                customerId.Value
            );

            Reviews.Clear();

            foreach (var item in items)
                Reviews.Add(item);

            Status = Reviews.Count == 0
                ? "Bạn chưa có đánh giá nào."
                : $"Tìm thấy {Reviews.Count} đánh giá.";
        }
        catch (Exception ex)
        {
            Status = "Không thể tải đánh giá: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
