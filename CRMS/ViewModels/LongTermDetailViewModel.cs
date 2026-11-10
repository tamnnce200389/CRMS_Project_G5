using System.Globalization;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Models;
using CRMS.Services;
using CRMS.Services.Vehicles;
namespace CRMS.ViewModels;

public sealed class LongTermDetailViewModel : BaseViewModel
{
    private readonly ILongTermRateService _service;
    private readonly IUserSession _session;
    private readonly TimeSpan _pickupTime;
    private readonly Dictionary<int, VehicleLongTermRate> _rates = new();
    private readonly int _initialMonths;
    private bool _loading;
    private int _version;
    public CarSearchItem Car { get; }
    private DateTime? _pickupDate;
    public DateTime? PickupDate { get => _pickupDate; set { _pickupDate = value; OnPropertyChanged(); Refresh(); } }
    public DateTime MinimumDate => DateTime.Today;
    public int SelectedMonths { get; private set; }
    private VehicleLongTermRate? Rate => _rates.GetValueOrDefault(SelectedMonths);
    public string YearLabel => Car.ManufactureYear.ToString();
    private string PlanDiscount(int months) => _rates.TryGetValue(months, out var rate) ? rate.DiscountPercent > 0 ? $"-{rate.DiscountPercent:0.##}%" : "Giá gốc" : "Chưa có giá";
    public string Month1Label => PlanDiscount(1);
    public string Month3Label => PlanDiscount(3);
    public string Month6Label => PlanDiscount(6);
    public string Month9Label => PlanDiscount(9);
    public string Month12Label => PlanDiscount(12);
    private static string Money(decimal value) => value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " ₫";
    public string BaseMonthlyLabel => Rate is { } r ? Money(r.BaseMonthlyRate) + "/tháng" : "";
    public string MonthlyLabel => Rate is { } r ? Money(r.MonthlyRate) + "/tháng" : "";
    public string DiscountLabel => Rate is { } r ? r.DiscountPercent > 0 ? $"Ưu đãi {r.DiscountPercent:0}% cho gói {SelectedMonths} tháng" : "Giá tiêu chuẩn" : "";
    public string TotalLabel => Rate is { } r ? Money(r.MonthlyRate * SelectedMonths) : "";
    public string ReturnDateLabel => PickupDate.HasValue && SelectedMonths > 0 ? PickupDate.Value.AddMonths(SelectedMonths).ToString("dd/MM/yyyy") : "";
    private string _status = "Đang tải các gói thuê…";
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }
    public ICommand LoadCommand { get; }
    public ICommand SelectPlanCommand { get; }
    public ICommand ChooseCommand { get; }
    public event Action<BookingSelection>? CheckoutRequested;
    public LongTermDetailViewModel(CarSearchItem car, DateTime? start, int months, ILongTermRateService? service = null, IUserSession? session = null)
    {
        Car = car; _service = service ?? new LongTermRateService(); _session = session ?? CurrentUserService.Instance; _pickupTime = start?.TimeOfDay ?? TimeSpan.FromHours(9); _pickupDate = start?.Date ?? DateTime.Today.AddDays(7); _initialMonths = months;
        LoadCommand = new AsyncRelayCommand(_ => LoadAsync());
        SelectPlanCommand = new RelayCommand(p => { if (int.TryParse(p?.ToString(), out var n) && _rates.ContainsKey(n)) { SelectedMonths = n; Refresh(); } }, p => !_loading && int.TryParse(p?.ToString(), out var n) && _rates.ContainsKey(n));
        ChooseCommand = new RelayCommand(_ =>
        {
            try
            {
                if (_session.Role != "Customer" || _session.UserId is not > 0) throw new InvalidOperationException("Vui lòng đăng nhập tài khoản Customer để đặt xe.");
                if (Rate == null || !PickupDate.HasValue) throw new InvalidOperationException("Vui lòng chọn gói và ngày nhận xe.");
                var from = PickupDate.Value.Date.Add(_pickupTime); var until = from.AddMonths(SelectedMonths); AvailabilityRules.ValidatePeriod(from, until, DateTime.UtcNow, true);
                CheckoutRequested?.Invoke(new(_session.UserId.Value, Car.VehicleId, "LongTerm", from, until, "Month", SelectedMonths, Rate.MonthlyRate * SelectedMonths));
            }
            catch (Exception ex) { Status = ex.Message; }
        }, _ => !_loading && Rate != null);
    }
    private void Refresh() { foreach (var name in new[] { nameof(SelectedMonths), nameof(BaseMonthlyLabel), nameof(MonthlyLabel), nameof(DiscountLabel), nameof(TotalLabel), nameof(ReturnDateLabel), nameof(Month1Label), nameof(Month3Label), nameof(Month6Label), nameof(Month9Label), nameof(Month12Label) }) OnPropertyChanged(name); CommandManager.InvalidateRequerySuggested(); }
    public void Cancel() => ++_version;
    public async Task LoadAsync()
    {
        var version = ++_version; _loading = true; CommandManager.InvalidateRequerySuggested();
        try { var rates = await _service.GetRatesByVehicleAsync(Car.VehicleId); if (version != _version) return; _rates.Clear(); foreach (var rate in rates) _rates[rate.DurationMonths] = rate; if (!_rates.ContainsKey(_initialMonths)) { Status = "Gói thuê đã chọn không còn khả dụng. Vui lòng tìm lại."; return; } SelectedMonths = _initialMonths; Status = "Giá được lấy từ bảng giá thuê dài hạn."; Refresh(); }
        catch (Exception ex) { if (version == _version) Status = "Không thể tải bảng giá: " + ex.Message; }
        finally { if (version == _version) { _loading = false; CommandManager.InvalidateRequerySuggested(); } }
    }
}
