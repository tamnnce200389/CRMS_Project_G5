
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services;

namespace CRMS.ViewModels
{
    public class AdminDashboardViewModel : BaseViewModel
    {
        private readonly DashboardService _service = new();

        private static DateTime TodayVN
        {
            get
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(
                    "SE Asia Standard Time");

                return TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow, zone).Date;
            }
        }

        private DateTime? _fromDate = TodayVN.AddDays(-29);

        public DateTime? FromDate
        {
            get => _fromDate;
            set
            {
                if (_fromDate == value) return;
                _fromDate = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _toDate = TodayVN;

        public DateTime? ToDate
        {
            get => _toDate;
            set
            {
                if (_toDate == value) return;
                _toDate = value;
                OnPropertyChanged();
            }
        }

        private DashboardSnapshot? _snapshot;

        public DashboardSnapshot? Snapshot
        {
            get => _snapshot;
            private set
            {
                _snapshot = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasData));
            }
        }

        public bool HasData => Snapshot != null;

        private bool _isBusy;

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

        private string _statusMessage =
            "Chọn khoảng ngày để xem thống kê hệ thống.";

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshCommand { get; }

        public AdminDashboardViewModel()
        {
            RefreshCommand = new RelayCommand(
                async _ => await LoadAsync(),
                _ => !IsBusy);
        }

        public async Task LoadAsync()
        {
            if (IsBusy) return;

            // Khong giu du lieu cua phien truoc
            Snapshot = null;

            var session = CurrentUserService.Instance;

            if (!session.IsAdmin ||
                !session.UserId.HasValue)
            {
                StatusMessage =
                    "Chỉ Admin mới được xem Dashboard quản trị.";
                return;
            }

            if (!FromDate.HasValue || !ToDate.HasValue)
            {
                StatusMessage =
                    "Vui lòng chọn đầy đủ khoảng thời gian.";
                return;
            }

            if (FromDate.Value.Date > ToDate.Value.Date)
            {
                StatusMessage =
                    "Ngày bắt đầu không được sau ngày kết thúc.";
                return;
            }

            long adminId = session.UserId.Value;
            string role = session.Role;

            IsBusy = true;

            try
            {
                StatusMessage =
                    "Đang tải dữ liệu quản trị từ SQL Server...";

                // Admin: thong ke van hanh + tai chinh
                DashboardSnapshot result =
                    await _service.GetOverviewAsync(
                        FromDate.Value,
                        ToDate.Value);

                if (session.UserId != adminId ||
                    session.Role != role)
                {
                    StatusMessage =
                        "Phiên đăng nhập đã thay đổi. Hãy tải lại.";
                    return;
                }

                Snapshot = result;

                StatusMessage =
                    "Đã cập nhật Admin Dashboard thành công.";
            }
            catch (Exception ex)
            {
                Snapshot = null;
                StatusMessage =
                    "Lỗi tải Admin Dashboard: " +
                    ex.GetBaseException().Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
