using System.Collections.ObjectModel;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Models;
using CRMS.Services;
using CRMS.Services.Vehicles;
namespace CRMS.ViewModels;

public partial class HomeViewModel
{
    private SearchService _searchService = new();
    private Task? _cityLoad;
    public ObservableCollection<SearchCity> PickupCities { get; } = new();
    public ObservableCollection<SearchCity> DestinationCities { get; } = new();
    private SearchCity? _pickup, _destination;
    public SearchCity? DriverPickup { get => _pickup; set { _pickup = value; OnPropertyChanged(); } }
    public SearchCity? DriverDestination { get => _destination; set { _destination = value; OnPropertyChanged(); } }
    private string _route = "InnerCity";
    public string DriverRoute { get => _route; set { if (value is not ("InnerCity" or "InterCityRoundTrip" or "InterCityOneWay")) return; _route = value; OnPropertyChanged(); } }
    private string _cityName = "Cần Thơ";
    public string SelectedCityName => _cityName;
    public int? SelectedCityId { get; private set; }
    public List<string> SelectedNeeds { get; private set; } = new();
    public List<string> SelectedTrends { get; private set; } = new();
    public List<string> SelectedBudgets { get; private set; } = new();
    public DateTime? SelfStart { get; private set; }
    public DateTime? SelfEnd { get; private set; }
    public DateTime? DriverStart { get; private set; }
    public DateTime? DriverEnd { get; private set; }
    public bool HourlyOnly { get; private set; }
    public string SelfPeriodLabel => $"{SelfStart:HH:mm dd/MM} - {SelfEnd:HH:mm dd/MM}";
    public string DriverPeriodLabel => $"{DriverStart:HH:mm dd/MM} - {DriverEnd:HH:mm dd/MM}";
    private string _searchMessage = "";
    public string SearchMessage { get => _searchMessage; private set { _searchMessage = value; OnPropertyChanged(); } }
    public ICommand LoadCitiesCommand { get; private set; } = null!;
    public ICommand SearchCommand { get; private set; } = null!;
    public event Action<SearchNavigationRequest>? SearchRequested;
    public HomeViewModel(SearchService service) : this() { _searchService = service; }
    private void InitializeSearchState()
    {
        var next = DateTime.Now.AddHours(1); var start = new DateTime(next.Year, next.Month, next.Day, next.Hour, 0, 0);
        SelfStart = DriverStart = start; SelfEnd = start.AddDays(1); DriverEnd = start.AddHours(4);
        LoadCitiesCommand = new AsyncRelayCommand(async _ => { try { await LoadCitiesAsync(); SearchMessage = ""; } catch (Exception ex) { SearchMessage = "Không thể tải địa điểm: " + ex.Message; } });
        SearchCommand = new AsyncRelayCommand(async p =>
        {
            try
            {
                SearchMessage = ""; await LoadCitiesAsync(); var mode = p?.ToString() ?? SelectedMode;
                var context = BuildSearchContext(mode); DateTime? from = mode == "SelfDrive" ? SelfStart : mode == "WithDriver" ? DriverStart : null;
                DateTime? until = mode == "SelfDrive" ? SelfEnd : mode == "WithDriver" ? DriverEnd : null;
                if (mode != "LongTerm")
                {
                    if (!from.HasValue || !until.HasValue) throw new ArgumentException("Vui lòng chọn thời gian thuê.");
                    AvailabilityRules.ValidatePeriod(from.Value, until.Value, DateTime.UtcNow, true);
                }
                SearchRequested?.Invoke(new(mode, from, until, context));
            }
            catch (Exception ex) { SearchMessage = ex.Message; }
        });
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(SelectedMode)) SuggestSeason(); };
    }
    private void SuggestSeason() { var start = IsSelfDrive ? SelfStart : IsWithDriver ? DriverStart : null; if (start.HasValue) SelectedSeason = SeasonForMonth(start.Value.Month); }
    public void SetPeriod(bool driver, DateTime start, DateTime end, bool hourly = false)
    {
        AvailabilityRules.ValidatePeriod(start, end, DateTime.UtcNow, true);
        if (driver) { DriverStart = start; DriverEnd = end; OnPropertyChanged(nameof(DriverPeriodLabel)); }
        else { SelfStart = start; SelfEnd = end; HourlyOnly = hourly; OnPropertyChanged(nameof(SelfPeriodLabel)); }
        SuggestSeason();
    }
    public void SetLocation(string name, int? id, IEnumerable<string> needs, IEnumerable<string> trends, IEnumerable<string> budgets)
    {
        _cityName = name; SelectedCityId = id; SelectedNeeds = needs.ToList(); SelectedTrends = trends.ToList(); SelectedBudgets = budgets.ToList(); OnPropertyChanged(nameof(SelectedCityName));
    }
    public async Task LoadCitiesAsync()
    {
        var load = _cityLoad ??= LoadCoreAsync(); try { await load; } catch { if (ReferenceEquals(_cityLoad, load)) _cityLoad = null; throw; }
    }
    public async Task ReloadCitiesAsync() { _cityLoad = null; await LoadCitiesAsync(); }
    private async Task LoadCoreAsync()
    {
        var cities = await _searchService.GetCitiesAsync(); var pickup = DriverPickup?.CityId; var destination = DriverDestination?.CityId;
        var city = cities.FirstOrDefault(c => c.CityId == SelectedCityId) ?? cities.FirstOrDefault(c => c.CityName == _cityName);
        SelectedCityId = city?.CityId; _cityName = city?.CityName ?? "Tất cả địa điểm"; OnPropertyChanged(nameof(SelectedCityName));
        PickupCities.Clear(); PickupCities.Add(new() { CityId = 0, CityName = "Chọn điểm đón" }); DestinationCities.Clear(); DestinationCities.Add(new() { CityId = 0, CityName = "Chọn điểm đến" });
        foreach (var c in cities) { PickupCities.Add(c); DestinationCities.Add(c); }
        DriverPickup = PickupCities.FirstOrDefault(c => c.CityId == pickup) ?? city ?? PickupCities[0]; DriverDestination = DestinationCities.FirstOrDefault(c => c.CityId == destination) ?? DestinationCities[0];
    }
    public CarSearchContext BuildSearchContext(string mode)
    {
        if (mode is not ("SelfDrive" or "WithDriver" or "LongTerm")) throw new ArgumentException("Hình thức thuê không hợp lệ.");
        if (mode == "WithDriver")
        {
            if (DriverPickup?.CityId is not > 0) throw new ArgumentException("Vui lòng chọn điểm đón.");
            if (DriverRoute != "InnerCity" && DriverDestination?.CityId is not > 0) throw new ArgumentException("Vui lòng chọn điểm đến.");
            if (DriverRoute != "InnerCity" && DriverPickup.CityId == DriverDestination!.CityId) throw new ArgumentException("Điểm đến phải khác điểm đón.");
        }
        var longTerm = PickupCities.FirstOrDefault(c => c.CityName == "Cần Thơ" && c.CityId > 0);
        if (mode == "LongTerm" && longTerm == null) throw new ArgumentException("Địa điểm Cần Thơ chưa được cấu hình cho trang thuê dài hạn.");
        return new()
        {
            PickupCityId = mode == "WithDriver" ? DriverPickup?.CityId : mode == "LongTerm" ? longTerm?.CityId : SelectedCityId,
            DestinationCityId = mode == "WithDriver" ? (DriverRoute == "InnerCity" ? DriverPickup?.CityId : DriverDestination?.CityId) : null,
            RouteType = mode == "WithDriver" ? DriverRoute : "InnerCity",
            HourlyOnly = mode == "SelfDrive" && HourlyOnly,
            Fuel = mode == "SelfDrive" ? (SelectedTrends.Contains("Electric") ? "Electric" : SelectedTrends.Contains("Hybrid") ? "Hybrid" : null) : null,
            PurposeCodes = mode == "SelfDrive" ? SelectedNeeds.Where(n => n != "ChildSeat" && n != "LongDistance").ToArray() : [],
            ChildSeatOnly = mode == "SelfDrive" && SelectedNeeds.Contains("ChildSeat"),
            ExtendedTravelOnly = mode == "SelfDrive" && SelectedNeeds.Contains("LongDistance"),
            SportOnly = mode == "SelfDrive" && SelectedTrends.Contains("Sport"),
            BudgetCode = mode == "SelfDrive" ? SelectedBudgets.FirstOrDefault() : null
        };
    }
}
