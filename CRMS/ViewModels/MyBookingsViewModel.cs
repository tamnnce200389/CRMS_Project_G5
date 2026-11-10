using System.Collections.ObjectModel;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services;
using CRMS.Services.Payments;
namespace CRMS.ViewModels;

public sealed class MyBookingsViewModel : SessionViewModel
{
    private readonly IBookingQueryService _service;
    private SessionStamp? _loadedSession;
    private readonly Dictionary<long, string> _displayedStatuses = new();
    public ObservableCollection<TripItemViewModel> Bookings { get; } = new();
    private string _title = "Đang tải chuyến thuê…", _description = "Vui lòng đợi trong giây lát.";
    private bool _retry;
    public string StateTitle { get => _title; private set { _title = value; OnPropertyChanged(); } }
    public string StateDescription { get => _description; private set { _description = value; OnPropertyChanged(); } }
    public bool ShowState => Bookings.Count == 0;
    public bool ShowRetry { get => _retry; private set { _retry = value; OnPropertyChanged(); } }
    public ICommand LoadCommand { get; }
    public MyBookingsViewModel() : this(new BookingQueryService(), CurrentUserService.Instance) { }
    public MyBookingsViewModel(IBookingQueryService service, IUserSession session) : base(session) { _service = service; LoadCommand = new AsyncRelayCommand(_ => LoadAsync(), _ => !IsBusy); }
    protected override void ClearState() { _loadedSession = null; _displayedStatuses.Clear(); Bookings.Clear(); ShowRetry = false; StateTitle = "Đang tải chuyến thuê…"; StateDescription = "Vui lòng đợi trong giây lát."; OnPropertyChanged(nameof(ShowState)); }
    public void RefreshDeadlineLabels()
    {
        for (var i = 0; i < Bookings.Count; i++)
        {
            var row = Bookings[i];
            if (!_displayedStatuses.TryGetValue(row.Trip.BookingId, out var shown) || shown != row.EffectiveStatus)
            {
                _displayedStatuses[row.Trip.BookingId] = row.EffectiveStatus;
                Bookings[i] = row with { };
            }
        }
    }
    public void UpdatePaymentSession(DemoPaymentSession payment)
    {
        if (_loadedSession is not { } stamp || Session.Role != "Customer") return;
        try { stamp.Ensure(Session); } catch (InvalidOperationException) { return; }
        for (var i = 0; i < Bookings.Count; i++)
        {
            if (Bookings[i].Trip.BookingId != payment.BookingId || !Bookings[i].Trip.IsDemo) continue;
            Bookings[i] = new(Bookings[i].Trip with { Status = payment.BookingStatus, DepositDeadlineUtc = payment.Deadline });
            _displayedStatuses[payment.BookingId] = Bookings[i].EffectiveStatus;
            break;
        }
    }
    public Task LoadAsync() => RunAsync(async (stamp, token) =>
    {
        ClearState(); try
        {
            var rows = await _service.GetMyBookingsAsync(token); Ensure(stamp, token);
            foreach (var row in rows) { var item = new TripItemViewModel(row); Bookings.Add(item); _displayedStatuses[row.BookingId] = item.EffectiveStatus; }
            _loadedSession = stamp;
            StateTitle = "Bạn chưa có chuyến thuê nào"; StateDescription = "Chọn Tìm chuyến đi mới để khám phá chiếc xe phù hợp."; OnPropertyChanged(nameof(ShowState));
        }
        catch (OperationCanceledException) { throw; }
        catch { Ensure(stamp, token); StateTitle = "Chưa thể tải chuyến thuê"; StateDescription = "Vui lòng kiểm tra kết nối và thử lại."; ShowRetry = true; }
    });
}
