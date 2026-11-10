using System.Collections.ObjectModel;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Models;
using CRMS.Services;
namespace CRMS.ViewModels;

public sealed class FilterChoiceViewModel : BaseViewModel
{
    public string Code { get; }
    public string Label { get; }
    private bool _selected;
    public bool IsSelected { get => _selected; set { if (_selected == value) return; _selected = value; OnPropertyChanged(); Changed?.Invoke(this); } }
    public event Action<FilterChoiceViewModel>? Changed;
    public FilterChoiceViewModel(string code, string label, bool selected) { Code = code; Label = label; _selected = selected; }
}
public sealed class LocationFilterViewModel : BaseViewModel
{
    private readonly SearchService _service;
    private readonly int? _initialCity;
    public ObservableCollection<SearchCity> Cities { get; } = new();
    public ObservableCollection<FilterChoiceViewModel> Needs { get; } = new();
    public ObservableCollection<FilterChoiceViewModel> Trends { get; } = new();
    public ObservableCollection<FilterChoiceViewModel> Budgets { get; } = new();
    private SearchCity? _city;
    public SearchCity? SelectedCity { get => _city; set { _city = value; OnPropertyChanged(); } }
    private bool _ready;
    public bool IsReady { get => _ready; private set { _ready = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
    private string _status = "Chọn địa điểm nhận xe.";
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }
    public ICommand LoadCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand ApplyCommand { get; }
    public event Action? Applied;
    public LocationFilterViewModel(int? city, IEnumerable<string>? needs, IEnumerable<string>? trends, IEnumerable<string>? budgets, SearchService? service = null, string? currentCityName = null)
    {
        _service = service ?? new(); _initialCity = city;
        foreach (var (code, label) in new[] { ("ChildSeat", "Ghế trẻ em"), ("LongDistance", "Di chuyển mở rộng"), ("NewDriver", "Lái mới"), ("Business", "Công việc, đi lại"), ("Family", "Gia đình"), ("Camping", "Cắm trại, chở đồ"), ("Friends", "Nhóm bạn"), ("Event", "Tiếp khách, dự tiệc") }) Needs.Add(new(code, label, needs?.Contains(code) == true));
        foreach (var (code, label) in new[] { ("Electric", "Xe điện"), ("Hybrid", "Xe hybrid"), ("Sport", "Xe thể thao") }) Trends.Add(new(code, label, trends?.Contains(code) == true));
        foreach (var (code, label) in new[] { ("LowCost", "Giá rẻ"), ("Economy", "Tiết kiệm") }) Budgets.Add(new(code, label, budgets?.Contains(code) == true));
        foreach (var choice in Trends) choice.Changed += c => { if (c.IsSelected && c.Code != "Sport") foreach (var other in Trends.Where(x => x != c && x.Code != "Sport")) other.IsSelected = false; };
        foreach (var choice in Budgets) choice.Changed += c => { if (c.IsSelected) foreach (var other in Budgets.Where(x => x != c)) other.IsSelected = false; };
        LoadCommand = new AsyncRelayCommand(async _ => { try { IsReady = false; var rows = await _service.GetCitiesAsync(); Cities.Clear(); Cities.Add(new() { CityId = 0, CityName = "Tất cả địa điểm" }); foreach (var row in rows) Cities.Add(row); SelectedCity = Cities.FirstOrDefault(c => c.CityId == city) ?? Cities.FirstOrDefault(c => c.CityName == currentCityName) ?? Cities[0]; Status = rows.Count == 0 ? "Chưa có địa điểm được cấu hình." : "Các điều kiện sẽ được áp dụng khi tìm xe."; IsReady = true; } catch (Exception ex) { Status = "Không thể tải địa điểm: " + ex.Message; } });
        ResetCommand = new RelayCommand(_ => { SelectedCity = Cities.FirstOrDefault(c => c.CityId == _initialCity) ?? Cities.FirstOrDefault(c => c.CityName == currentCityName) ?? Cities.FirstOrDefault(); foreach (var c in Needs.Concat(Trends).Concat(Budgets)) c.IsSelected = false; });
        ApplyCommand = new RelayCommand(_ => Applied?.Invoke(), _ => IsReady);
    }
}
