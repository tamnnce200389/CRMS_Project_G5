
using System;
using System.Collections.ObjectModel;


using System.Threading.Tasks;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services;


namespace CRMS.ViewModels
{
    public class RevenueReportViewModel : BaseViewModel
    {
        private readonly ReportService _service = new();
        private readonly IFileExportService? _files;

        // ======================================
        // NGAY HIEN TAI TAI VIET NAM
        // ======================================

        private static DateTime TodayVN
        {
            get
            {
                var zone =
                    TimeZoneInfo.FindSystemTimeZoneById(
                        "SE Asia Standard Time");

                return TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    zone).Date;
            }
        }

        // ======================================
        // DATE FILTER
        // ======================================

        private DateTime? _fromDate =
            TodayVN.AddDays(-29);

        public DateTime? FromDate
        {
            get => _fromDate;

            set
            {
                if (_fromDate == value)
                    return;

                _fromDate = value;
                OnPropertyChanged();

                InvalidateReport();
            }
        }

        private DateTime? _toDate = TodayVN;

        public DateTime? ToDate
        {
            get => _toDate;

            set
            {
                if (_toDate == value)
                    return;

                _toDate = value;
                OnPropertyChanged();

                InvalidateReport();
            }
        }

        // ======================================
        // REPORT DATA
        // ======================================

        private RevenueReportResult? _report;

        public RevenueReportResult? Report
        {
            get => _report;

            private set
            {
                _report = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(HasData));
                OnPropertyChanged(nameof(HasNoTransactions));

                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ObservableCollection<RevenueReportRow> Rows
        { get; } = new();

        public bool HasData => Report != null;

        public bool HasNoTransactions =>
            Report != null && Rows.Count == 0;

        // ======================================
        // LOADING
        // ======================================

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

        // ======================================
        // STATUS MESSAGE
        // ======================================

        private string _statusMessage =
            "Chọn khoảng ngày để xem báo cáo doanh thu.";

        public string StatusMessage
        {
            get => _statusMessage;

            private set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        // ======================================
        // COMMANDS
        // ======================================

        public ICommand FilterByDateCommand { get; }

        public ICommand ExportCsvCommand { get; }

        public RevenueReportViewModel() : this(ApplicationServices.FileExport) { }
        public RevenueReportViewModel(IFileExportService? files)
        {
            _files = files;
            FilterByDateCommand = new AsyncRelayCommand(
                async _ => await LoadAsync(),
                _ => !IsBusy);

            ExportCsvCommand = new AsyncRelayCommand(
                async _ => await ExportCsvAsync(),
                _ => !IsBusy &&
                     HasData &&
                     CurrentUserService.Instance.IsAdmin);
        }

        // ======================================
        // CLEAR STALE REPORT
        // ======================================

        private void InvalidateReport()
        {
            Report = null;
            Rows.Clear();

            StatusMessage =
                "Bộ lọc đã thay đổi. Nhấn Xem báo cáo để cập nhật.";
        }

        // ======================================
        // LOAD REVENUE REPORT
        // ======================================

        public async Task LoadAsync()
        {
            if (IsBusy)
                return;

            Report = null;
            Rows.Clear();

            var session = CurrentUserService.Instance;

            if (!session.IsAdmin ||
                !session.UserId.HasValue)
            {
                StatusMessage =
                    "Chỉ Admin mới được xem báo cáo doanh thu.";
                return;
            }

            if (!FromDate.HasValue || !ToDate.HasValue)
            {
                StatusMessage =
                    "Vui lòng chọn đầy đủ khoảng thời gian.";
                return;
            }

            DateTime fromDate = FromDate.Value.Date;
            DateTime toDate = ToDate.Value.Date;

            if (fromDate > toDate)
            {
                StatusMessage =
                    "Ngày bắt đầu không được sau ngày kết thúc.";
                return;
            }

            long adminId = session.UserId.Value;

            IsBusy = true;

            try
            {
                StatusMessage =
                    "Đang lấy dữ liệu báo cáo từ SQL Server...";

                RevenueReportResult result =
                    await _service.GetRevenueReportAsync(
                        fromDate,
                        toDate);

                // Kiem tra phien dang nhap va bo loc
                // khong thay doi trong luc truy van
                if (session.UserId != adminId ||
                    !session.IsAdmin ||
                    FromDate?.Date != fromDate ||
                    ToDate?.Date != toDate)
                {
                    StatusMessage =
                        "Phiên đăng nhập hoặc bộ lọc đã thay đổi. " +
                        "Vui lòng tải lại.";
                    return;
                }

                foreach (var item in result.Rows)
                {
                    Rows.Add(item);
                }

                Report = result;

                OnPropertyChanged(nameof(HasNoTransactions));

                if (Rows.Count == 0)
                {
                    StatusMessage =
                        "Không có giao dịch thành công " +
                        "trong khoảng ngày đã chọn.";
                }
                else
                {
                    StatusMessage =
                        $"Đã tìm thấy {result.TotalBookings} đơn thuê " +
                        $"với {result.TotalTransactions} giao dịch.";
                }
            }
            catch (Exception ex)
            {
                Report = null;
                Rows.Clear();

                StatusMessage =
                    "Lỗi tải báo cáo: " +
                    ex.GetBaseException().Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ======================================
        // EXPORT CSV
        // ======================================

        private async Task ExportCsvAsync()
        {
            if (IsBusy || Report == null)
                return;

            var session = CurrentUserService.Instance;

            if (!session.IsAdmin ||
                !session.UserId.HasValue)
            {
                StatusMessage =
                    "Chỉ Admin được xuất báo cáo doanh thu.";
                return;
            }

            long adminId = session.UserId.Value;

            RevenueReportResult report = Report;

            // Tao ten file mac dinh
            string fileName =
                $"Revenue_{report.FromDateVN:yyyyMMdd}_" +
                $"{report.ToDateVN:yyyyMMdd}.csv";

            var path = _files?.ChooseCsvPath(fileName);
            if (path == null) return;

            // Khong xuat neu phien da thay doi
            if (session.UserId != adminId ||
                !session.IsAdmin ||
                Report != report)
            {
                StatusMessage =
                    "Phiên đăng nhập hoặc báo cáo đã thay đổi. " +
                    "Vui lòng tải lại.";
                return;
            }

            IsBusy = true;

            try
            {
                // Lay noi dung CSV tu ReportService
                string csv = _service.BuildCsv(report);

                // UTF-8 BOM giup Excel doc tieng Viet
                await _files!.WriteCsvAsync(path, csv);

                StatusMessage =
                    "Đã xuất báo cáo CSV thành công: " +
                    path;
            }
            catch (Exception ex)
            {
                StatusMessage =
                    "Không thể xuất CSV: " +
                    ex.GetBaseException().Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
