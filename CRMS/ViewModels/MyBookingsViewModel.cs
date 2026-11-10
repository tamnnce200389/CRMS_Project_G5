using System.Collections.ObjectModel;
using System.Windows.Input;
using CRMS.Dtos;
using CRMS.Helpers;
using CRMS.Services;
namespace CRMS.ViewModels;

public sealed class MyBookingsViewModel : SessionViewModel
{
    private readonly IBookingQueryService _service;
    public ObservableCollection<CustomerTrip> Bookings { get; } = new();
    private string _title = "Đang tải chuyến thuê…", _description = "Vui lòng đợi trong giây lát.";
    private bool _retry;
    public string StateTitle { get => _title; private set { _title = value; OnPropertyChanged(); } }
    public string StateDescription { get => _description; private set { _description = value; OnPropertyChanged(); } }
    public bool ShowState => Bookings.Count == 0;
    public bool ShowRetry { get => _retry; private set { _retry = value; OnPropertyChanged(); } }
    public ICommand LoadCommand { get; }
    public MyBookingsViewModel() : this(new BookingQueryService(), CurrentUserService.Instance) { }
    public MyBookingsViewModel(IBookingQueryService service, IUserSession session) : base(session) { _service = service; LoadCommand = new AsyncRelayCommand(_ => LoadAsync(), _ => !IsBusy); }
    protected override void ClearState() { Bookings.Clear(); ShowRetry = false; StateTitle = "Đang tải chuyến thuê…"; StateDescription = "Vui lòng đợi trong giây lát."; OnPropertyChanged(nameof(ShowState)); }
    public Task LoadAsync() => RunAsync(async (stamp, token) =>
    {
        ClearState(); try
        {
            var rows = await _service.GetMyBookingsAsync(token); Ensure(stamp, token); foreach (var row in rows) Bookings.Add(row);
            StateTitle = "Bạn chưa có chuyến thuê nào"; StateDescription = "Chọn Tìm chuyến đi mới để khám phá chiếc xe phù hợp."; OnPropertyChanged(nameof(ShowState));
        }
        catch (OperationCanceledException) { throw; }
        catch { Ensure(stamp, token); StateTitle = "Chưa thể tải chuyến thuê"; StateDescription = "Vui lòng kiểm tra kết nối và thử lại."; ShowRetry = true; }
    });
}
