using CRMS.Helpers;
using CRMS.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace CRMS.ViewModels
{
    public class CashDepositViewModel : BaseViewModel
    {
        private readonly CashDepositService _service = new();
        private readonly ObservableCollection<CashDepositItem> _items = new();

        public ICollectionView Bookings { get; }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                Bookings.Refresh();
            }
        }

        private CashDepositItem? _selectedBooking;
        public CashDepositItem? SelectedBooking
        {
            get => _selectedBooking;
            set
            {
                _selectedBooking = value;
                OnPropertyChanged();
            }
        }

        private string _message = "";
        public string Message
        {
            get => _message;
            private set
            {
                _message = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                _isLoading = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Sẽ bật khi hoàn thiện service xác nhận cọc.
        public bool CanConfirmDeposit => false;

        public ICommand LoadCommand { get; }
        public ICommand ConfirmDepositCommand { get; }

        public CashDepositViewModel()
        {
            Bookings = CollectionViewSource.GetDefaultView(_items);

            Bookings.Filter = item =>
            {
                if (item is not CashDepositItem row)
                    return false;

                string keyword = SearchText.Trim();

                return row.BookingCode.Contains(
                           keyword, StringComparison.OrdinalIgnoreCase)
                    || row.CustomerName.Contains(
                           keyword, StringComparison.OrdinalIgnoreCase);
            };

            LoadCommand = new RelayCommand(
                async _ => await LoadAsync(),
                _ => !IsLoading);

            ConfirmDepositCommand = new RelayCommand(
                _ => { },
                _ => CanConfirmDeposit);
        }

        public async Task LoadAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;
            SelectedBooking = null;
            _items.Clear();
            Message = "Đang tải danh sách từ database...";

            var userId = CurrentUserService.Instance.UserId;

            try
            {
                var items = await _service.GetPendingDepositsAsync();

                // Không hiển thị dữ liệu nếu đã đổi phiên đăng nhập.
                if (CurrentUserService.Instance.UserId != userId)
                {
                    Message = "Phiên đăng nhập đã thay đổi. Hãy tải lại.";
                    return;
                }

                foreach (var item in items)
                    _items.Add(item);

                Message = items.Count == 0
                    ? "Không có đơn chờ cọc tiền mặt còn hạn."
                    : $"Đã tải {items.Count} đơn chờ cọc tiền mặt.";
            }
            catch (Exception ex)
            {
                Message = "Không tải được danh sách: "
                    + ex.GetBaseException().Message;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}