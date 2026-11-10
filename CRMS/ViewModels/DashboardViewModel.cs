using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services;

namespace CRMS.ViewModels;

// Shared screen state only. Authorization and queries remain in DashboardService.
public abstract class DashboardViewModel<TSnapshot> : BaseViewModel where TSnapshot : class
{
    private readonly Func<DateTime, DateTime, Task<TSnapshot>> _load;
    private readonly IUserSession _session;
    private int _filterVersion;
    private const string FilterChangedMessage = "Khoảng ngày đã thay đổi. Nhấn Cập nhật để tải lại.";
    private string? _statusMessage;

    protected DashboardViewModel(Func<DateTime, DateTime, Task<TSnapshot>> load, IUserSession session)
    {
        _load = load;
        _session = session;
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync(), _ => !IsBusy);
    }

    protected abstract bool CanLoad(IUserSession session);
    protected abstract string InitialMessage { get; }
    protected abstract string AccessDeniedMessage { get; }
    protected abstract string LoadingMessage { get; }
    protected abstract string LoadedMessage { get; }
    protected abstract string ErrorPrefix { get; }

    private static DateTime TodayVN => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")).Date;

    private DateTime? _fromDate = TodayVN.AddDays(-29);
    public DateTime? FromDate
    {
        get => _fromDate;
        set { if (_fromDate == value) return; _fromDate = value; InvalidateFilter(); OnPropertyChanged(); }
    }

    private DateTime? _toDate = TodayVN;
    public DateTime? ToDate
    {
        get => _toDate;
        set { if (_toDate == value) return; _toDate = value; InvalidateFilter(); OnPropertyChanged(); }
    }

    private TSnapshot? _snapshot;
    public TSnapshot? Snapshot
    {
        get => _snapshot;
        private set { _snapshot = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasData)); }
    }
    public bool HasData => Snapshot != null;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }
    public string StatusMessage
    {
        get => _statusMessage ?? InitialMessage;
        private set { _statusMessage = value; OnPropertyChanged(); }
    }
    public ICommand RefreshCommand { get; }

    private void InvalidateFilter()
    {
        ++_filterVersion;
        Snapshot = null;
        StatusMessage = FilterChangedMessage;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        Snapshot = null;
        if (!CanLoad(_session) || !_session.UserId.HasValue)
        {
            StatusMessage = AccessDeniedMessage;
            return;
        }
        if (!FromDate.HasValue || !ToDate.HasValue)
        {
            StatusMessage = "Vui lòng chọn đầy đủ khoảng thời gian.";
            return;
        }
        if (FromDate.Value.Date > ToDate.Value.Date)
        {
            StatusMessage = "Ngày bắt đầu không được sau ngày kết thúc.";
            return;
        }

        DateTime fromDate = FromDate.Value.Date;
        DateTime toDate = ToDate.Value.Date;
        int filterVersion = _filterVersion;
        long userId = _session.UserId.Value;
        string role = _session.Role;
        IsBusy = true;
        try
        {
            StatusMessage = LoadingMessage;
            TSnapshot result = await _load(fromDate, toDate);
            // Preserve the existing user/role and filter-version checks.
            if (_session.UserId != userId || _session.Role != role)
            {
                StatusMessage = "Phiên đăng nhập đã thay đổi. Hãy tải lại.";
                return;
            }
            if (filterVersion != _filterVersion)
            {
                StatusMessage = FilterChangedMessage;
                return;
            }
            Snapshot = result;
            StatusMessage = LoadedMessage;
        }
        catch (Exception ex)
        {
            Snapshot = null;
            StatusMessage = filterVersion != _filterVersion
                ? FilterChangedMessage : ErrorPrefix + ex.GetBaseException().Message;
        }
        finally { IsBusy = false; }
    }
}
