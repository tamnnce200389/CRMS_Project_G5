using System.Windows;
using CRMS.Services;
using CRMS.Services.Vehicles;

namespace CRMS.Views.Guest;

public partial class VehiclePhotosWindow : Window
{
    private readonly long _vehicleId;

    public VehiclePhotosWindow(CarSearchItem vehicle)
    {
        InitializeComponent();
        _vehicleId = vehicle.VehicleId;
        VehicleTitle.Text = $"{vehicle.Brand} {vehicle.Model}";
        MaxWidth = SystemParameters.WorkArea.Width;
        MaxHeight = SystemParameters.WorkArea.Height;
        Loaded += LoadPhotos;
    }

    private async void LoadPhotos(object sender, RoutedEventArgs e)
    {
        Loaded -= LoadPhotos;
        try
        {
            var vehicle = await new VehicleService().GetVehicleDetailsAsync(_vehicleId);
            Photos.ItemsSource = vehicle?.Images;
            StatusText.Text = vehicle?.Images.Count > 0 ? "" : "Xe chưa có ảnh.";
        }
        catch (Exception)
        {
            StatusText.Text = "Chưa tải được ảnh. Hãy đóng và mở lại cửa sổ để thử lại.";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
