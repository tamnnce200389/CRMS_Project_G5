using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services.Vehicles;

namespace CRMS.ViewModels;

public sealed class VehicleImageItemViewModel : BaseViewModel
{
    private string _imageUrl = "";
    public string ImageUrl
    {
        get => _imageUrl;
        set
        {
            _imageUrl = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    private bool _isPrimary;
    public bool IsPrimary
    {
        get => _isPrimary;
        set
        {
            _isPrimary = value;
            OnPropertyChanged();
        }
    }

    private int _sortOrder;
    public int SortOrder
    {
        get => _sortOrder;
        set
        {
            _sortOrder = value;
            OnPropertyChanged();
        }
    }

    public string DisplayName
    {
        get
        {
            if (ImageUrl.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
            {
                var slashIdx = ImageUrl.LastIndexOf('/');
                return slashIdx >= 0 && slashIdx + 1 < ImageUrl.Length
                    ? ImageUrl[(slashIdx + 1)..]
                    : ImageUrl;
            }

            if (Uri.TryCreate(ImageUrl, UriKind.Absolute, out var uri))
            {
                if (uri.IsFile)
                    return Path.GetFileName(uri.LocalPath);
                var queryText = uri.Query;
                if (!string.IsNullOrEmpty(queryText) && queryText.Contains("text=", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = queryText.IndexOf("text=", StringComparison.OrdinalIgnoreCase);
                    return Uri.UnescapeDataString(queryText[(idx + 5)..].Replace('+', ' '));
                }
                var file = Path.GetFileName(uri.AbsolutePath);
                return string.IsNullOrWhiteSpace(file) ? ImageUrl : $"{uri.Host}/{file}";
            }
            return ImageUrl;
        }
    }

    public override string ToString() => DisplayName;
}

public sealed record OptionItem(string Value, string Label)
{
    public override string ToString() => Label;
}

public sealed class VehicleEditViewModel : BaseViewModel
{
    public const long MaxImageFileBytes = 10L * 1024 * 1024;

    private static readonly HashSet<string> AllowedLocalImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".bmp"
    };

    private readonly IVehicleService _service;
    private int? _originalOdometerKm;

    public long? EditingVehicleId { get; private set; }
    public bool IsEditMode => EditingVehicleId.HasValue;
    public string WindowTitle => IsEditMode ? $"Chỉnh sửa xe #{EditingVehicleId}" : "Thêm phương tiện mới";

    public ObservableCollection<CategoryListItem> Categories { get; } = new();
    public ObservableCollection<VehicleImageItemViewModel> Images { get; } = new();

    public IReadOnlyList<OptionItem> TransmissionOptions { get; } = new[]
    {
        new OptionItem("Automatic", "Số tự động (Automatic)"),
        new OptionItem("Manual", "Số sàn (Manual)")
    };

    public IReadOnlyList<OptionItem> FuelOptions { get; } = new[]
    {
        new OptionItem("Gasoline", "Xăng (Gasoline)"),
        new OptionItem("Diesel", "Dầu Diesel"),
        new OptionItem("Electric", "Điện (Electric)"),
        new OptionItem("Hybrid", "Hybrid")
    };

    public IReadOnlyList<OptionItem> StatusOptions { get; } = new[]
    {
        new OptionItem("Active", "Hoạt động (Active)"),
        new OptionItem("Suspended", "Tạm ngưng (Suspended)")
    };

    private int _selectedCategoryId;
    public int SelectedCategoryId
    {
        get => _selectedCategoryId;
        set { _selectedCategoryId = value; OnPropertyChanged(); }
    }

    private string _licensePlate = "";
    public string LicensePlate
    {
        get => _licensePlate;
        set { _licensePlate = value; OnPropertyChanged(); }
    }

    private string _brand = "";
    public string Brand
    {
        get => _brand;
        set { _brand = value; OnPropertyChanged(); }
    }

    private string _model = "";
    public string Model
    {
        get => _model;
        set { _model = value; OnPropertyChanged(); }
    }

    private string _manufactureYearText = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);
    public string ManufactureYearText
    {
        get => _manufactureYearText;
        set { _manufactureYearText = value; OnPropertyChanged(); }
    }

    private string _seatCountText = "5";
    public string SeatCountText
    {
        get => _seatCountText;
        set { _seatCountText = value; OnPropertyChanged(); }
    }

    private string _selectedTransmission = "Automatic";
    public string SelectedTransmission
    {
        get => _selectedTransmission;
        set { _selectedTransmission = value; OnPropertyChanged(); }
    }

    private string _selectedFuelType = "Gasoline";
    public string SelectedFuelType
    {
        get => _selectedFuelType;
        set { _selectedFuelType = value; OnPropertyChanged(); }
    }

    private string _dailyRateText = "800000";
    public string DailyRateText
    {
        get => _dailyRateText;
        set { _dailyRateText = value; OnPropertyChanged(); }
    }

    private string _odometerKmText = "0";
    public string OdometerKmText
    {
        get => _odometerKmText;
        set { _odometerKmText = value; OnPropertyChanged(); }
    }

    private string _selectedOperationalStatus = "Active";
    public string SelectedOperationalStatus
    {
        get => _selectedOperationalStatus;
        set { _selectedOperationalStatus = value; OnPropertyChanged(); }
    }

    private string _description = "";
    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    private string _addUrlInput = "";
    public string AddUrlInput
    {
        get => _addUrlInput;
        set { _addUrlInput = value; OnPropertyChanged(); }
    }

    private VehicleImageItemViewModel? _selectedImage;
    public VehicleImageItemViewModel? SelectedImage
    {
        get => _selectedImage;
        set
        {
            _selectedImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewImageUrl));
        }
    }

    public string? PreviewImageUrl =>
        SelectedImage?.ImageUrl ?? Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? Images.FirstOrDefault()?.ImageUrl;

    private string _errorMessage = "";
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            _isSaving = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public Func<IReadOnlyList<string>?>? PickLocalImageFiles { get; set; }
    public event Action<bool>? RequestClose;

    public ICommand BrowseLocalImagesCommand { get; }
    public ICommand AddImageUrlCommand { get; }
    public ICommand SetPrimaryImageCommand { get; }
    public ICommand MoveImageUpCommand { get; }
    public ICommand MoveImageDownCommand { get; }
    public ICommand RemoveImageCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public VehicleEditViewModel()
        : this(new VehicleService())
    {
    }

    public VehicleEditViewModel(IVehicleService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));

        BrowseLocalImagesCommand = new RelayCommand(_ => BrowseLocalImages(), _ => !IsSaving);
        AddImageUrlCommand = new RelayCommand(_ => AddImageFromInput(), _ => !IsSaving);
        SetPrimaryImageCommand = new RelayCommand(
            p => SetPrimaryImage(p as VehicleImageItemViewModel ?? SelectedImage),
            p => !IsSaving && (p is VehicleImageItemViewModel || SelectedImage != null));
        MoveImageUpCommand = new RelayCommand(
            p => MoveImage(p as VehicleImageItemViewModel ?? SelectedImage, -1),
            p => !IsSaving && CanMoveImage(p as VehicleImageItemViewModel ?? SelectedImage, -1));
        MoveImageDownCommand = new RelayCommand(
            p => MoveImage(p as VehicleImageItemViewModel ?? SelectedImage, 1),
            p => !IsSaving && CanMoveImage(p as VehicleImageItemViewModel ?? SelectedImage, 1));
        RemoveImageCommand = new RelayCommand(
            p => RemoveImage(p as VehicleImageItemViewModel ?? SelectedImage),
            p => !IsSaving && (p is VehicleImageItemViewModel || SelectedImage != null));
        SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync(), _ => !IsSaving);
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false), _ => !IsSaving);
    }

    public async Task InitializeForCreateAsync()
    {
        EditingVehicleId = null;
        _originalOdometerKm = null;
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(WindowTitle));

        var allCats = await _service.GetCategoriesAsync(includeInactive: false);
        Categories.Clear();
        foreach (var c in allCats)
            Categories.Add(c);

        SelectedCategoryId = Categories.FirstOrDefault()?.CategoryId ?? 0;
        ErrorMessage = Categories.Count == 0
            ? "Chưa có loại xe đang hoạt động. Vui lòng tạo hoặc bật ít nhất 1 loại xe trước."
            : "";
    }

    public async Task InitializeForEditAsync(long vehicleId)
    {
        var details = await _service.GetVehicleDetailsAsync(vehicleId)
            ?? throw new InvalidOperationException("Không tìm thấy xe cần chỉnh sửa.");

        EditingVehicleId = details.VehicleId;
        _originalOdometerKm = details.OdometerKm;
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(WindowTitle));

        var allCats = await _service.GetCategoriesAsync(includeInactive: true);
        Categories.Clear();
        foreach (var c in allCats.Where(c => c.IsActive || c.CategoryId == details.CategoryId))
            Categories.Add(c);

        SelectedCategoryId = details.CategoryId;
        LicensePlate = details.LicensePlate;
        Brand = details.Brand;
        Model = details.Model;
        ManufactureYearText = details.ManufactureYear.ToString(CultureInfo.InvariantCulture);
        SeatCountText = details.SeatCount.ToString(CultureInfo.InvariantCulture);
        SelectedTransmission = details.Transmission;
        SelectedFuelType = details.FuelType;
        DailyRateText = details.DailyRate.ToString("0.##", CultureInfo.InvariantCulture);
        OdometerKmText = details.OdometerKm.ToString(CultureInfo.InvariantCulture);
        SelectedOperationalStatus = details.OperationalStatus;
        Description = details.Description ?? "";

        Images.Clear();
        foreach (var img in details.Images.OrderBy(i => i.SortOrder))
        {
            Images.Add(new VehicleImageItemViewModel
            {
                ImageUrl = img.ImageUrl,
                IsPrimary = img.IsPrimary,
                SortOrder = img.SortOrder
            });
        }
        RenumberImages();
        SelectedImage = Images.FirstOrDefault(i => i.IsPrimary) ?? Images.FirstOrDefault();
        ErrorMessage = "";
    }

    public static string GetUploadDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CRMS", "VehicleUploads");

    public void ImportLocalImageFiles(IEnumerable<string> sourceFiles)
    {
        try
        {
            ErrorMessage = "";

            foreach (var sourceFile in sourceFiles)
            {
                if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
                    throw new InvalidOperationException($"Không tìm thấy tệp ảnh: {sourceFile}");

                var ext = Path.GetExtension(sourceFile);
                if (!AllowedLocalImageExtensions.Contains(ext))
                    throw new InvalidOperationException("Chỉ hỗ trợ tệp ảnh định dạng JPG, JPEG, PNG, WEBP hoặc BMP.");

                var info = new FileInfo(sourceFile);
                if (info.Length <= 0)
                    throw new InvalidOperationException($"Tệp ảnh rỗng hoặc không hợp lệ: {info.Name}");

                if (info.Length > MaxImageFileBytes)
                    throw new InvalidOperationException($"Tệp ảnh '{info.Name}' vượt quá dung lượng tối đa 10 MB.");

                var uri = VehicleRules.NormalizeImageUrl(sourceFile);
                AppendImageUri(uri);
            }
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (IOException ex)
        {
            ErrorMessage = $"Không thể đọc tệp ảnh: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            ErrorMessage = $"Không có quyền truy cập tệp ảnh: {ex.Message}";
        }
    }

    private void BrowseLocalImages()
    {
        var selectedFiles = PickLocalImageFiles?.Invoke();
        if (selectedFiles is null || selectedFiles.Count == 0)
            return;

        ImportLocalImageFiles(selectedFiles);
    }

    private void AddImageFromInput()
    {
        try
        {
            ErrorMessage = "";
            var uri = VehicleRules.NormalizeImageUrl(AddUrlInput);
            AppendImageUri(uri);
            AddUrlInput = "";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void AppendImageUri(string normalizedUri)
    {
        if (Images.Any(i => i.ImageUrl.Equals(normalizedUri, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Ảnh này đã có trong danh sách của xe.");

        var item = new VehicleImageItemViewModel
        {
            ImageUrl = normalizedUri,
            IsPrimary = Images.Count == 0,
            SortOrder = Images.Count
        };
        Images.Add(item);
        RenumberImages();
        SelectedImage = item;
        OnPropertyChanged(nameof(PreviewImageUrl));
    }

    private void SetPrimaryImage(VehicleImageItemViewModel? target)
    {
        if (target is null)
            return;

        foreach (var img in Images)
            img.IsPrimary = ReferenceEquals(img, target);

        SelectedImage = target;
        OnPropertyChanged(nameof(PreviewImageUrl));
    }

    private bool CanMoveImage(VehicleImageItemViewModel? item, int delta)
    {
        if (item is null)
            return false;
        var idx = Images.IndexOf(item);
        return idx >= 0 && idx + delta >= 0 && idx + delta < Images.Count;
    }

    private void MoveImage(VehicleImageItemViewModel? item, int delta)
    {
        if (item is null || !CanMoveImage(item, delta))
            return;

        var idx = Images.IndexOf(item);
        Images.Move(idx, idx + delta);
        RenumberImages();
        SelectedImage = item;
        CommandManager.InvalidateRequerySuggested();
    }

    private void RemoveImage(VehicleImageItemViewModel? item)
    {
        if (item is null)
            return;

        var wasPrimary = item.IsPrimary;
        Images.Remove(item);
        if (wasPrimary && Images.Count > 0 && !Images.Any(i => i.IsPrimary))
        {
            Images[0].IsPrimary = true;
        }
        RenumberImages();
        SelectedImage = Images.FirstOrDefault(i => i.IsPrimary) ?? Images.FirstOrDefault();
        OnPropertyChanged(nameof(PreviewImageUrl));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RenumberImages()
    {
        for (int i = 0; i < Images.Count; i++)
            Images[i].SortOrder = i;
    }

    public async Task SaveAsync()
    {
        if (IsSaving)
            return;

        var copiedFiles = new List<(VehicleImageItemViewModel Item, string OriginalUri, string CopiedPath)>();

        try
        {
            ErrorMessage = "";

            if (!short.TryParse(ManufactureYearText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
                throw new InvalidOperationException("Năm sản xuất phải là số nguyên hợp lệ (1990 - 2100).");

            if (!byte.TryParse(SeatCountText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seats))
                throw new InvalidOperationException("Số chỗ ngồi phải là số nguyên từ 1 đến 30.");

            if (!VehicleRules.TryParseVndAmount(DailyRateText, out var dailyRate))
                throw new InvalidOperationException("Giá thuê theo ngày không hợp lệ.");

            var normalizedOdoText = OdometerKmText.Trim().Replace(",", "").Replace(".", "");
            if (!int.TryParse(normalizedOdoText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var odo))
                throw new InvalidOperationException("Số công-tơ-mét (Odo) phải là số nguyên không âm.");

            var request = new VehicleUpsertRequest(
                VehicleId: EditingVehicleId,
                CategoryId: SelectedCategoryId,
                LicensePlate: LicensePlate,
                Brand: Brand,
                Model: Model,
                ManufactureYear: year,
                SeatCount: seats,
                Transmission: SelectedTransmission,
                FuelType: SelectedFuelType,
                DailyRate: dailyRate,
                OdometerKm: odo,
                OperationalStatus: SelectedOperationalStatus,
                Description: Description,
                Images: Images.Select((img, idx) => new VehicleImageInput(img.ImageUrl, img.IsPrimary, idx)).ToList());

            VehicleRules.ValidateVehicle(request, _originalOdometerKm);

            IsSaving = true;
            PersistStagedLocalImages(copiedFiles);
            request = request with
            {
                Images = Images.Select((img, idx) => new VehicleImageInput(img.ImageUrl, img.IsPrimary, idx)).ToList()
            };

            await _service.SaveVehicleAsync(request);
            RequestClose?.Invoke(true);
        }
        catch (InvalidOperationException ex)
        {
            RollbackCopiedImages(copiedFiles);
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            RollbackCopiedImages(copiedFiles);
            ErrorMessage = "Không thể lưu thông tin xe. Vui lòng kiểm tra kết nối cơ sở dữ liệu và thử lại.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsSaving = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void PersistStagedLocalImages(List<(VehicleImageItemViewModel Item, string OriginalUri, string CopiedPath)> copiedFiles)
    {
        var uploadDir = Path.GetFullPath(GetUploadDirectory());
        var uploadPrefix = uploadDir.EndsWith(Path.DirectorySeparatorChar)
            ? uploadDir
            : uploadDir + Path.DirectorySeparatorChar;

        foreach (var item in Images)
        {
            if (!Uri.TryCreate(item.ImageUrl, UriKind.Absolute, out var uri) || !uri.IsFile)
                continue;

            var localPath = Path.GetFullPath(uri.LocalPath);
            if (localPath.StartsWith(uploadPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            Directory.CreateDirectory(uploadDir);
            var ext = Path.GetExtension(localPath);
            var safeName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
            var destination = Path.Combine(uploadDir, safeName);
            File.Copy(localPath, destination, overwrite: true);

            var originalUri = item.ImageUrl;
            item.ImageUrl = new Uri(destination).AbsoluteUri;
            copiedFiles.Add((item, originalUri, destination));
        }

        OnPropertyChanged(nameof(PreviewImageUrl));
    }

    private void RollbackCopiedImages(List<(VehicleImageItemViewModel Item, string OriginalUri, string CopiedPath)> copiedFiles)
    {
        foreach (var (item, originalUri, copiedPath) in copiedFiles)
        {
            item.ImageUrl = originalUri;
            try
            {
                if (File.Exists(copiedPath))
                    File.Delete(copiedPath);
            }
            catch
            {
                // Ignore best-effort temp cleanup failure
            }
        }

        if (copiedFiles.Count > 0)
            OnPropertyChanged(nameof(PreviewImageUrl));
    }
}
