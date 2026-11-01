using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CRMS.Services.Vehicles;
using CRMS.ViewModels;
using CRMS.Views.Admin;
using CRMS.Views.Staff;

internal static class RenderChecks
{
    private sealed class BindingErrorListener : TraceListener
    {
        public List<string> Errors { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message) &&
                (message.Contains("BindingExpression path error", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("Cannot find source", StringComparison.OrdinalIgnoreCase)))
            {
                Errors.Add(message.Trim());
            }
        }
    }

    public static void Run(string output)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            var listener = new BindingErrorListener();
            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);

            try
            {
                Directory.CreateDirectory(output);
                var app = new CRMS.App();
                app.InitializeComponent();

                var fakeService = new RenderSampleService();

                // 1. Admin VehiclePage
                var vehicleVm = new VehicleViewModel(fakeService);
                vehicleVm.LoadVehiclesAsync().GetAwaiter().GetResult();
                var adminPage = new VehiclePage { DataContext = vehicleVm };
                RenderElement(DetachRoot(adminPage, vehicleVm), 1280, 760, Path.Combine(output, "TV2-F06-VehiclePage.png"));

                // 2. Staff StaffVehiclePage (backward-compatibility check)
                var staffVm = new VehicleViewModel(fakeService);
                staffVm.LoadVehiclesAsync().GetAwaiter().GetResult();
                var staffPage = new StaffVehiclePage { DataContext = staffVm };
                RenderElement(DetachRoot(staffPage, staffVm), 1100, 720, Path.Combine(output, "TV2-F06-StaffVehiclePage.png"));

                // 3. Admin VehicleEditWindow
                var editVm = new VehicleEditViewModel(fakeService);
                editVm.InitializeForEditAsync(1).GetAwaiter().GetResult();
                var editWin = new VehicleEditWindow(editVm);
                RenderElement(DetachRoot(editWin, editVm), 1000, 735, Path.Combine(output, "TV2-F06-VehicleEditWindow.png"));

                // 4. Admin CategoryManagementWindow
                var catVm = new CategoryManagementViewModel(fakeService);
                catVm.LoadAsync().GetAwaiter().GetResult();
                catVm.SelectedCategory = catVm.Categories.FirstOrDefault();
                var catWin = new CategoryManagementWindow(catVm);
                RenderElement(DetachRoot(catWin, catVm), 900, 580, Path.Combine(output, "TV2-F06-CategoryManagementWindow.png"));

                if (listener.Errors.Count > 0)
                {
                    throw new InvalidOperationException(
                        "WPF DataBinding errors detected:\n" + string.Join("\n", listener.Errors));
                }

                Console.WriteLine("PASS: Rendered all 4 F06 WPF views with 0 DataBinding errors.");
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
            throw error;
    }

    private static FrameworkElement DetachRoot(Page page, object dataContext)
    {
        var content = (FrameworkElement)page.Content;
        page.Content = null;
        content.DataContext = dataContext;
        return content;
    }

    private static FrameworkElement DetachRoot(Window window, object dataContext)
    {
        var content = (FrameworkElement)window.Content;
        window.Content = null;
        content.DataContext = dataContext;
        return content;
    }

    private static void RenderElement(FrameworkElement element, int width, int height, string path)
    {
        var root = new Border
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(Color.FromRgb(244, 247, 251)),
            Child = element
        };
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private sealed class RenderSampleService : IVehicleService
    {
        private readonly List<CategoryListItem> _cats = new()
        {
            new CategoryListItem { CategoryId = 1, CategoryName = "Sedan", Description = "Xe 4-5 chỗ đô thị tiết kiệm nhiên liệu", IsActive = true, TotalVehicles = 8, ActiveVehicles = 7 },
            new CategoryListItem { CategoryId = 2, CategoryName = "SUV", Description = "Xe gầm cao 5-7 chỗ đa dụng", IsActive = true, TotalVehicles = 6, ActiveVehicles = 6 },
            new CategoryListItem { CategoryId = 3, CategoryName = "MPV", Description = "Xe gia đình 7 chỗ rộng rãi", IsActive = true, TotalVehicles = 4, ActiveVehicles = 4 },
            new CategoryListItem { CategoryId = 4, CategoryName = "Coupe", Description = "Dòng xe thể thao tạm ngưng", IsActive = false, TotalVehicles = 0, ActiveVehicles = 0 }
        };

        private readonly List<VehicleListItem> _vehicles = new()
        {
            new VehicleListItem
            {
                VehicleId = 1, CategoryId = 1, CategoryName = "Sedan", LicensePlate = "65A-12345",
                Brand = "Toyota", Model = "Vios G", ManufactureYear = 2024, SeatCount = 5,
                Transmission = "Automatic", FuelType = "Gasoline", DailyRate = 750000m,
                OdometerKm = 18200, OperationalStatus = "Active", ImageCount = 2
            },
            new VehicleListItem
            {
                VehicleId = 2, CategoryId = 2, CategoryName = "SUV", LicensePlate = "65A-67890",
                Brand = "Ford", Model = "Everest Titanium", ManufactureYear = 2024, SeatCount = 7,
                Transmission = "Automatic", FuelType = "Diesel", DailyRate = 1550000m,
                OdometerKm = 14000, OperationalStatus = "Active", ImageCount = 3
            },
            new VehicleListItem
            {
                VehicleId = 3, CategoryId = 3, CategoryName = "MPV", LicensePlate = "51K-889.99",
                Brand = "Kia", Model = "Carnival Signature", ManufactureYear = 2025, SeatCount = 7,
                Transmission = "Automatic", FuelType = "Diesel", DailyRate = 2100000m,
                OdometerKm = 6400, OperationalStatus = "Active", ImageCount = 2
            },
            new VehicleListItem
            {
                VehicleId = 4, CategoryId = 1, CategoryName = "Sedan", LicensePlate = "DEMO-CRMS-026",
                Brand = "Toyota", Model = "Camry Hybrid", ManufactureYear = 2024, SeatCount = 5,
                Transmission = "Automatic", FuelType = "Hybrid", DailyRate = 1750000m,
                OdometerKm = 11500, OperationalStatus = "Suspended", ImageCount = 1
            }
        };

        public Task<List<VehicleListItem>> GetVehiclesAsync(VehicleFilter? filter = null) =>
            Task.FromResult(_vehicles.ToList());

        public Task<VehicleDetailsDto?> GetVehicleDetailsAsync(long vehicleId) =>
            Task.FromResult<VehicleDetailsDto?>(new VehicleDetailsDto
            {
                VehicleId = 1, CategoryId = 1, CategoryName = "Sedan", LicensePlate = "65A-12345",
                Brand = "Toyota", Model = "Vios G", ManufactureYear = 2024, SeatCount = 5,
                Transmission = "Automatic", FuelType = "Gasoline", DailyRate = 750000m,
                OdometerKm = 18200, OperationalStatus = "Active",
                Description = "Xe sedan 5 chỗ tiết kiệm nhiên liệu, bảo dưỡng định kỳ chính hãng.",
                Images = new[]
                {
                    new VehicleImageDto(10, "https://placehold.co/960x540/png?text=Toyota+Vios+Front", true, 0),
                    new VehicleImageDto(11, "https://placehold.co/960x540/png?text=Toyota+Vios+Interior", false, 1)
                }
            });

        public Task<long> SaveVehicleAsync(VehicleUpsertRequest request) => Task.FromResult(1L);
        public Task SetVehicleStatusAsync(long vehicleId, string operationalStatus) => Task.CompletedTask;
        public Task<List<CategoryListItem>> GetCategoriesAsync(bool includeInactive = true) =>
            Task.FromResult(_cats.ToList());
        public Task<int> SaveCategoryAsync(CategoryUpsertRequest request) => Task.FromResult(1);
        public Task SetCategoryActiveAsync(int categoryId, bool isActive) => Task.CompletedTask;
    }
}
