using System.Collections.ObjectModel;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services;
using CRMS.Services.Vehicles;
namespace CRMS.ViewModels;

public sealed class VehiclePhotosViewModel : BaseViewModel
{
    private readonly IVehicleService _service;
    private readonly long _vehicleId;
    private int _version;
    public string VehicleTitle { get; }
    public ObservableCollection<VehicleImageDto> Photos { get; } = new();
    private string _status = "Đang tải ảnh xe…";
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }
    public ICommand LoadCommand { get; }
    public VehiclePhotosViewModel(CarSearchItem car, IVehicleService? service = null)
    {
        _service = service ?? new VehicleService(); _vehicleId = car.VehicleId; VehicleTitle = $"{car.Brand} {car.Model}";
        LoadCommand = new AsyncRelayCommand(async _ => { var version = ++_version; try { var details = await _service.GetVehicleDetailsAsync(_vehicleId); if (version != _version) return; Photos.Clear(); foreach (var photo in details?.Images ?? Array.Empty<VehicleImageDto>()) Photos.Add(photo); Status = Photos.Count > 0 ? "" : "Xe chưa có ảnh."; } catch { if (version == _version) Status = "Chưa tải được ảnh. Hãy đóng và mở lại cửa sổ để thử lại."; } });
    }
    public void Cancel() => ++_version;
}
