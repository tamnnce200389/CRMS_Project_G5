using System.Globalization;
using System.Windows.Input;
using CRMS.Helpers;
namespace CRMS.ViewModels;

public sealed class RentalTimeViewModel : BaseViewModel
{
    private readonly bool _withDriver;
    private int _tab;
    private DateTime? _pickup, _return, _hourDate;
    private string? _pickupTime, _returnTime, _hourTime;
    private int _duration = 4;
    public string[] Times { get; } = Enumerable.Range(0, 48).Select(i => TimeSpan.FromMinutes(i * 30).ToString(@"hh\:mm")).ToArray();
    public DateTime MinimumDate => DateTime.Today;
    public int SelectedTab { get => _tab; set { _tab = _withDriver ? 1 : value; Changed(); } }
    public DateTime? PickupDate { get => _pickup; set { _pickup = value; Changed(); } }
    public DateTime? ReturnDate { get => _return; set { _return = value; Changed(); } }
    public DateTime? HourDate { get => _hourDate; set { _hourDate = value; Changed(); } }
    public string? PickupTime { get => _pickupTime; set { _pickupTime = value; Changed(); } }
    public string? ReturnTime { get => _returnTime; set { _returnTime = value; Changed(); } }
    public string? HourTime { get => _hourTime; set { _hourTime = value; Changed(); } }
    public int DurationHours { get => _duration; set { _duration = value; Changed(); } }
    public string RentalUnit => _withDriver || SelectedTab == 1 ? "Hour" : "Day";
    public string Summary => TryBuildRange(out var start, out var end, out var error)
        ? $"{start:HH:mm dd/MM/yyyy} - {end:HH:mm dd/MM/yyyy}\nThời gian thuê: {(end - start).TotalHours:0.#} giờ" : error;
    public DateTime StartAt { get; private set; }
    public DateTime EndAt { get; private set; }
    public ICommand ConfirmCommand { get; }
    public event Action? Confirmed;
    public RentalTimeViewModel(bool withDriver = false, bool preferHourly = false)
    {
        _withDriver = withDriver; _tab = withDriver || preferHourly ? 1 : 0;
        var next = DateTime.Now.AddHours(1); var start = new DateTime(next.Year, next.Month, next.Day, next.Hour, 0, 0);
        _pickup = _hourDate = start.Date; _return = start.Date.AddDays(1); _pickupTime = _returnTime = _hourTime = start.ToString("HH:mm");
        ConfirmCommand = new RelayCommand(parameter => { if (TryBuildRange(out var startAt, out var endAt, out _)) { StartAt = startAt; EndAt = endAt; Confirmed?.Invoke(); } OnPropertyChanged(nameof(Summary)); });
    }
    private void Changed([System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    { OnPropertyChanged(property); OnPropertyChanged(nameof(Summary)); OnPropertyChanged(nameof(RentalUnit)); }
    public bool TryBuildRange(out DateTime start, out DateTime end, out string error)
    {
        start = end = default; error = ""; var day = RentalUnit == "Day";
        var date = day ? PickupDate : HourDate; var time = day ? PickupTime : HourTime;
        if (!date.HasValue || !TimeSpan.TryParseExact(time, @"hh\:mm", CultureInfo.InvariantCulture, out var pickup))
        { error = "Hãy chọn ngày và giờ nhận xe."; return false; }
        start = date.Value.Date.Add(pickup);
        if (day)
        {
            if (!ReturnDate.HasValue || !TimeSpan.TryParseExact(ReturnTime, @"hh\:mm", CultureInfo.InvariantCulture, out var dropoff))
            { error = "Hãy chọn ngày và giờ trả xe."; return false; }
            end = ReturnDate.Value.Date.Add(dropoff);
        }
        else
        {
            if (DurationHours is not (1 or 2 or 4 or 6 or 8 or 12 or 24 or 48 or 72)) { error = "Hãy chọn thời gian thuê."; return false; }
            end = start.AddHours(DurationHours);
        }
        if (start <= DateTime.Now) { error = "Thời gian nhận xe phải ở tương lai."; return false; }
        if (end <= start) { error = "Thời gian trả phải sau thời gian nhận."; return false; }
        if (day && end - start > TimeSpan.FromHours(48)) { error = "Thuê ngày chỉ được tối đa 48 giờ."; return false; }
        return true;
    }
}
