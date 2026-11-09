using System.Collections.ObjectModel;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Models;

namespace CRMS.ViewModels;

public sealed class SelfDriveFilterDialogViewModel : BaseViewModel
{
    private readonly CarSearchViewModel _parent;
    public string Kind { get; }
    public string Title => Kind switch { "Price" => "Khoảng giá thuê", "Category" => "Loại xe", "Brand" => "Hãng xe", "Sort" => "Sắp xếp", "Transmission" => "Truyền động", "Delivery" => "Giao nhận tận nơi", "Pickup" => "Miễn phí đưa đón", "Route" => "Hành trình có tài xế", "Travel" => "Di chuyển mở rộng", _ => "Ghế trẻ em" };
    public bool IsPrice => Kind == "Price";
    public string PriceUnit => _parent.IsLongTerm ? "đ/tháng" : _parent.IsWithDriver ? "đ/chuyến" : _parent.HourlyOnly ? "đ/giờ" : "đ/ngày";
    public string PriceFrom { get; set; }
    public string PriceTo { get; set; }
    public bool IsChoice => Kind is "Sort" or "Transmission";
    public int ChoiceColumns => Kind == "Sort" ? 2 : 1;
    public ObservableCollection<SearchDialogChoice> Choices { get; } = new();
    private SearchDialogChoice? _selectedChoice;
    public SearchDialogChoice? SelectedChoice { get => _selectedChoice; set { _selectedChoice = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsReady)); CommandManager.InvalidateRequerySuggested(); } }
    public bool IsCategory => Kind == "Category";
    public bool IsBrand => Kind == "Brand";
    public bool IsChildSeat => Kind == "ChildSeat";
    public bool IsDelivery => Kind is "Delivery" or "Pickup";
    public bool IsFreePickup => Kind == "Pickup";
    public string PointPrompt => IsFreePickup ? "Chọn điểm đưa đón" : "Chọn điểm giao nhận";
    public bool IsRoute => Kind == "Route";
    public List<SearchCity> RouteCities => _parent.Cities.Where(c => c.IsActive && c.CityId > 0).ToList();
    public List<string> RouteOptions => _parent.RouteOptions;
    private SearchCity? _pickup;
    public SearchCity? PickupCity { get => _pickup; set { _pickup = value; if (!IsInterCity) RouteDestination = value; RouteChanged(); } }
    private SearchCity? _routeDestination;
    public SearchCity? RouteDestination { get => _routeDestination; set { _routeDestination = value; RouteChanged(); } }
    private string _route = "Nội thành";
    public string SelectedRoute { get => _route; set { _route = value; if (!IsInterCity) RouteDestination = PickupCity; RouteChanged(); OnPropertyChanged(nameof(IsInterCity)); } }
    public bool IsInterCity => SelectedRoute != "Nội thành";
    public DateTime? StartAt { get; private set; }
    public DateTime? EndAt { get; private set; }
    public string PeriodLabel => StartAt.HasValue && EndAt.HasValue ? $"{StartAt:HH:mm dd/MM/yyyy} – {EndAt:HH:mm dd/MM/yyyy}" : "Chọn thời gian chuyến đi";
    public void SetRentalPeriod(DateTime start, DateTime end) { StartAt = start; EndAt = end; OnPropertyChanged(nameof(PeriodLabel)); RouteChanged(); }
    private bool ValidRoute => PickupCity != null && RouteDestination != null && (!IsInterCity || PickupCity.CityId != RouteDestination.CityId)
        && StartAt > DateTime.Now && EndAt > StartAt;
    private void RouteChanged()
    {
        OnPropertyChanged(nameof(PickupCity)); OnPropertyChanged(nameof(RouteDestination)); OnPropertyChanged(nameof(SelectedRoute));
        OnPropertyChanged(nameof(IsReady)); CommandManager.InvalidateRequerySuggested();
    }
    public bool IsTravel => Kind == "Travel";
    public ObservableCollection<SearchCity> TravelDestinations { get; } = new();
    private SearchCity? _selectedDestination;
    public SearchCity? SelectedDestinationCity
    {
        get => _selectedDestination;
        set { _selectedDestination = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsReady)); CommandManager.InvalidateRequerySuggested(); }
    }
    public string DeliveryCityLabel => _parent.LocationSummary;
    public ObservableCollection<RentalLocation> DeliveryLocations { get; } = new();
    private RentalLocation? _deliveryLocation;
    public RentalLocation? SelectedDeliveryLocation
    {
        get => _deliveryLocation;
        set { _deliveryLocation = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsReady)); CommandManager.InvalidateRequerySuggested(); }
    }
    public ObservableCollection<VehicleTypeFacet> Types { get; } = new();
    public ObservableCollection<BrandFacet> Brands { get; } = new();
    public ObservableCollection<ChildSeatAgeChoice> Ages { get; } = new();
    public VehicleTypeFacet? SelectedType { get; set; }
    public BrandFacet? SelectedBrand { get; set; }
    private bool _anyChildSeat;
    public bool AnyChildSeat { get => _anyChildSeat; set { _anyChildSeat = value; OnPropertyChanged(); if (value) foreach (var age in Ages) age.IsSelected = false; } }
    private bool _busy;
    private bool _loaded;
    public bool IsReady => !_busy && _loaded && (!IsChoice || SelectedChoice != null) && (!IsRoute || ValidRoute) && (!IsDelivery || SelectedDeliveryLocation != null) && (!IsTravel || SelectedDestinationCity != null);
    public bool IsApplying => _busy && _loaded;
    public string Status { get; private set; } = "Đang tải lựa chọn…";
    public ICommand ApplyCommand { get; }
    public event Action? Applied;
    public SelfDriveFilterDialogViewModel(CarSearchViewModel parent, string kind)
    {
        _parent = parent; Kind = kind;
        PriceFrom = parent.MinPrice; PriceTo = parent.MaxPrice;
        _route = parent.SelectedRoute; _pickup = parent.SelectedCity?.CityId > 0 ? parent.SelectedCity : null;
        _routeDestination = IsInterCity ? parent.DestinationCity : _pickup;
        StartAt = parent.StartAtLocal; EndAt = parent.EndAtLocal;
        AnyChildSeat = parent.ChildSeatOnly && parent.ChildSeatAgeCodes.Length == 0;
        ApplyCommand = new AsyncRelayCommand(async _ => await ApplyAsync(), _ => IsReady);
    }
    public async Task LoadAsync()
    {
        _busy = true; OnPropertyChanged(nameof(IsReady)); CommandManager.InvalidateRequerySuggested();
        try
        {
            if (IsPrice) { Status = "Để trống mức giá nếu bạn không muốn giới hạn."; _loaded = true; return; }
            if (IsChoice)
            {
                Choices.Clear();
                if (Kind == "Sort")
                {
                    Choices.Add(new("Giá tăng dần", "Giá thấp nhất"));
                    Choices.Add(new("Giá giảm dần", "Giá cao nhất"));
                }
                else
                {
                    Choices.Add(new("Tất cả hộp số", "Tất cả"));
                    Choices.Add(new("Số tự động", "Số tự động"));
                    Choices.Add(new("Số sàn", "Số sàn"));
                }
                var current = Kind == "Sort" ? _parent.SelectedSort : _parent.SelectedTransmission;
                SelectedChoice = Choices.FirstOrDefault(c => c.Value == current) ?? Choices[0];
                Status = ""; _loaded = true; return;
            }
            if (IsRoute)
            {
                Status = "Nội thành: cùng thành phố. Liên tỉnh: chọn hai thành phố khác nhau. Giá được tính lại theo hành trình và thời gian bạn chọn.";
                _loaded = true; return;
            }
            if (IsTravel)
            {
                TravelDestinations.Clear();
                foreach (var city in _parent.Cities.Where(c => c.IsActive && c.CityId > 0 && c.CityId != _parent.SelectedCity?.CityId))
                    TravelDestinations.Add(city);
                SelectedDestinationCity = TravelDestinations.FirstOrDefault(c => c.CityId == _parent.DestinationCity?.CityId);
                Status = TravelDestinations.Count == 0 ? "Chưa có thành phố đích để lựa chọn."
                    : "Chọn thành phố bạn dự định đến. Chỉ hiển thị xe được phép di chuyển tới đó.";
                _loaded = true;
                return;
            }
            if (IsDelivery)
            {
                DeliveryLocations.Clear();
                foreach (var location in _parent.DeliveryLocations.Where(l => l.IsActive && l.Latitude.HasValue && l.Longitude.HasValue &&
                    (_parent.SelectedCity?.CityId is null or 0 || l.CityId == _parent.SelectedCity.CityId)))
                    DeliveryLocations.Add(location);
                SelectedDeliveryLocation = DeliveryLocations.FirstOrDefault(l => l.LocationId == _parent.SelectedDeliveryLocation?.LocationId);
                Status = DeliveryLocations.Count == 0 ? "Chưa có điểm giao nhận tại khu vực đang chọn."
                    : IsFreePickup ? "Chỉ hiển thị xe có phí đưa đón bằng 0 tại điểm bạn chọn, trong phạm vi phục vụ của xe."
                    : "Chọn nơi nhận và trả xe. Chỉ xe có thể giao nhận tại điểm này mới hiển thị.";
                _loaded = true;
                return;
            }
            var facets = await _parent.GetCurrentModeFacetsAsync();
            Types.Clear(); Types.Add(new(null, null, "Tất cả", facets.Types.Sum(t => t.Count)));
            foreach (var type in facets.Types) Types.Add(type);
            SelectedType = _parent.HasCategoryFilter ? Types.FirstOrDefault(t => t.CategoryId == _parent.SelectedCategory?.CategoryId &&
                (!byte.TryParse(_parent.SelectedSeats, out var selectedSeats) || t.Seats == selectedSeats))
                : byte.TryParse(_parent.SelectedSeats, out _) ? null : Types[0];
            Brands.Clear(); Brands.Add(new("Tất cả hãng xe", facets.Brands.Sum(b => b.Count)));
            foreach (var brand in facets.Brands) Brands.Add(brand);
            SelectedBrand = _parent.HasBrandFilter ? Brands.FirstOrDefault(b => b.Name == _parent.SelectedBrand) : Brands[0];
            Ages.Clear();
            foreach (var pair in new[] { ("0-12", "Từ 0–12 tuổi (Ghế đa năng)"), ("0-1", "Từ 0–1 tuổi"), ("1-4", "Từ 1–4 tuổi"), ("4-7", "Từ 4–7 tuổi"), ("7-12", "Từ 7–12 tuổi") })
            {
                var age = new ChildSeatAgeChoice("ChildSeat:" + pair.Item1, pair.Item2, _parent.ChildSeatAgeCodes.Contains("ChildSeat:" + pair.Item1), facets.ChildSeatCodes.Contains("ChildSeat:" + pair.Item1) || facets.ChildSeatCodes.Contains("ChildSeat:0-12"));
                age.PropertyChanged += (_, _) => { if (age.IsSelected) AnyChildSeat = false; };
                Ages.Add(age);
            }
            Status = IsChildSeat && facets.ChildSeatCodes.Count == 0
                ? "Chưa có dữ liệu độ tuổi ghế tại địa điểm này. Bạn vẫn có thể chọn xe có ghế trẻ em."
                : "Số lượng theo địa điểm và thời gian thuê đã chọn.";
            OnPropertyChanged(nameof(SelectedType)); OnPropertyChanged(nameof(SelectedBrand));
            _loaded = true;
        }
        catch (Exception ex) { Status = "Chưa tải được lựa chọn: " + ex.Message; }
        finally { _busy = false; OnPropertyChanged(nameof(IsReady)); OnPropertyChanged(nameof(Status)); CommandManager.InvalidateRequerySuggested(); }
    }
    public async Task ApplyAsync()
    {
        if (!IsReady) return;
        if (IsPrice)
        {
            try
            {
                var minimum = CarSearchViewModel.ParsePrice(PriceFrom);
                var maximum = CarSearchViewModel.ParsePrice(PriceTo);
                if (minimum > maximum) throw new ArgumentException("Giá tối đa phải bằng hoặc cao hơn giá tối thiểu.");
            }
            catch (ArgumentException ex)
            {
                Status = ex.Message;
                OnPropertyChanged(nameof(Status));
                return;
            }
            _parent.MinPrice = PriceFrom;
            _parent.MaxPrice = PriceTo;
        }
        if (IsChoice && SelectedChoice != null)
        {
            if (Kind == "Sort") _parent.SelectedSort = SelectedChoice.Value;
            else _parent.SelectedTransmission = SelectedChoice.Value;
        }
        if (IsRoute)
        {
            // Commit the local draft only after Apply. A cancelled dialog leaves the search intact.
            if (_parent.SelectedCity?.CityId != PickupCity?.CityId)
            {
                _parent.SelectedDeliveryLocation = null;
                _parent.DeliveryOnly = false;
                _parent.FreeDeliveryOnly = false;
            }
            _parent.SelectedCity = PickupCity;
            _parent.SelectedRoute = SelectedRoute;
            _parent.DestinationCity = RouteDestination;
            _parent.SetRentalPeriod(StartAt, EndAt);
            if (_parent.SelectedDeliveryLocation == null) _parent.FreeDeliveryOnly = false;
        }
        if (IsCategory && SelectedType != null)
        {
            _parent.SelectedCategory = _parent.Categories.FirstOrDefault(c => c.CategoryId == (SelectedType.CategoryId ?? 0));
            _parent.SelectedSeats = SelectedType.Seats?.ToString() ?? "Tất cả số chỗ";
        }
        if (IsBrand && SelectedBrand != null) _parent.SelectedBrand = SelectedBrand.Name;
        if (IsChildSeat)
        {
            _parent.ChildSeatAgeCodes = Ages.Where(a => a.IsSelected && a.IsAvailable).Select(a => a.Code).ToArray();
            _parent.ChildSeatOnly = AnyChildSeat || _parent.ChildSeatAgeCodes.Length > 0;
        }
        if (IsDelivery && SelectedDeliveryLocation != null)
        {
            _parent.SelectedDeliveryLocation = SelectedDeliveryLocation;
            if (IsFreePickup) _parent.FreeDeliveryOnly = true;
            else _parent.DeliveryOnly = true;
        }
        if (IsTravel && SelectedDestinationCity != null)
        {
            _parent.DestinationCity = SelectedDestinationCity;
            _parent.ExtendedTravelOnly = true;
        }
        _busy = true; OnPropertyChanged(nameof(IsReady)); CommandManager.InvalidateRequerySuggested();
        try { await _parent.SearchAsync(); if (!_parent.StatusMessage.StartsWith("Không thể")) Applied?.Invoke(); }
        finally { _busy = false; OnPropertyChanged(nameof(IsReady)); CommandManager.InvalidateRequerySuggested(); }
    }
}
public sealed record SearchDialogChoice(string Value, string Label);
public sealed class ChildSeatAgeChoice : BaseViewModel
{
    public string Code { get; }
    public string Name { get; }
    public bool IsAvailable { get; }
    private bool _selected;
    public bool IsSelected { get => _selected; set { _selected = value; OnPropertyChanged(); } }
    public ChildSeatAgeChoice(string code, string name, bool selected, bool available) { Code = code; Name = name; _selected = selected; IsAvailable = available; }
}
