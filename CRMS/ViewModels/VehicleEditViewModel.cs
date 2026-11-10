using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services.Vehicles;

namespace CRMS.ViewModels
{
    public class VehicleImageItemViewModel : BaseViewModel
    {
        private string _imageUrl = string.Empty;
        public string ImageUrl
        {
            get { return _imageUrl; }
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
            get { return _isPrimary; }
            set
            {
                _isPrimary = value;
                OnPropertyChanged();
            }
        }

        private int _sortOrder;
        public int SortOrder
        {
            get { return _sortOrder; }
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
                    int slashIdx = ImageUrl.LastIndexOf('/');
                    if (slashIdx >= 0 && slashIdx + 1 < ImageUrl.Length)
                    {
                        return ImageUrl.Substring(slashIdx + 1);
                    }
                    return ImageUrl;
                }

                if (Uri.TryCreate(ImageUrl, UriKind.Absolute, out Uri? uri) && uri != null)
                {
                    if (uri.IsFile)
                    {
                        return Path.GetFileName(uri.LocalPath);
                    }

                    string queryText = uri.Query;
                    if (!string.IsNullOrEmpty(queryText) && queryText.Contains("text=", StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = queryText.IndexOf("text=", StringComparison.OrdinalIgnoreCase);
                        return Uri.UnescapeDataString(queryText.Substring(idx + 5).Replace('+', ' '));
                    }

                    string file = Path.GetFileName(uri.AbsolutePath);
                    return string.IsNullOrWhiteSpace(file) ? ImageUrl : $"{uri.Host}/{file}";
                }

                return ImageUrl;
            }
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public class OptionItem
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;

        public OptionItem()
        {
        }

        public OptionItem(string value, string label)
        {
            Value = value;
            Label = label;
        }

        public override string ToString()
        {
            return Label;
        }
    }

    public class VehicleEditViewModel : BaseViewModel
    {
        private class CopiedImageBackup
        {
            public VehicleImageItemViewModel Item { get; set; }
            public string OriginalUri { get; set; }
            public string CopiedPath { get; set; }

            public CopiedImageBackup(VehicleImageItemViewModel item, string originalUri, string copiedPath)
            {
                Item = item;
                OriginalUri = originalUri;
                CopiedPath = copiedPath;
            }
        }

        public const long MaxImageFileBytes = 10L * 1024 * 1024;

        private static readonly string[] AllowedLocalImageExtensions =
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
        public bool IsEditMode
        {
            get { return EditingVehicleId.HasValue; }
        }

        public string WindowTitle
        {
            get { return IsEditMode ? $"Chỉnh sửa xe #{EditingVehicleId}" : "Thêm phương tiện mới"; }
        }

        public ObservableCollection<CategoryListItem> Categories { get; } = new ObservableCollection<CategoryListItem>();
        public ObservableCollection<VehicleImageItemViewModel> Images { get; } = new ObservableCollection<VehicleImageItemViewModel>();

        public List<OptionItem> TransmissionOptions { get; } = new List<OptionItem>
        {
            new OptionItem("Automatic", "Số tự động (Automatic)"),
            new OptionItem("Manual", "Số sàn (Manual)")
        };

        public List<OptionItem> FuelOptions { get; } = new List<OptionItem>
        {
            new OptionItem("Gasoline", "Xăng (Gasoline)"),
            new OptionItem("Diesel", "Dầu Diesel"),
            new OptionItem("Electric", "Điện (Electric)"),
            new OptionItem("Hybrid", "Hybrid")
        };

        public List<OptionItem> StatusOptions { get; } = new List<OptionItem>
        {
            new OptionItem("Active", "Hoạt động (Active)"),
            new OptionItem("Suspended", "Tạm ngưng (Suspended)")
        };

        private int _selectedCategoryId;
        public int SelectedCategoryId
        {
            get { return _selectedCategoryId; }
            set { _selectedCategoryId = value; OnPropertyChanged(); }
        }

        private string _licensePlate = string.Empty;
        public string LicensePlate
        {
            get { return _licensePlate; }
            set { _licensePlate = value; OnPropertyChanged(); }
        }

        private string _brand = string.Empty;
        public string Brand
        {
            get { return _brand; }
            set { _brand = value; OnPropertyChanged(); }
        }

        private string _model = string.Empty;
        public string Model
        {
            get { return _model; }
            set { _model = value; OnPropertyChanged(); }
        }

        private string _manufactureYearText = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);
        public string ManufactureYearText
        {
            get { return _manufactureYearText; }
            set { _manufactureYearText = value; OnPropertyChanged(); }
        }

        private string _seatCountText = "5";
        public string SeatCountText
        {
            get { return _seatCountText; }
            set { _seatCountText = value; OnPropertyChanged(); }
        }

        private string _selectedTransmission = "Automatic";
        public string SelectedTransmission
        {
            get { return _selectedTransmission; }
            set { _selectedTransmission = value; OnPropertyChanged(); }
        }

        private string _selectedFuelType = "Gasoline";
        public string SelectedFuelType
        {
            get { return _selectedFuelType; }
            set { _selectedFuelType = value; OnPropertyChanged(); }
        }

        private string _dailyRateText = "800000";
        public string DailyRateText
        {
            get { return _dailyRateText; }
            set { _dailyRateText = value; OnPropertyChanged(); }
        }

        private string _odometerKmText = "0";
        public string OdometerKmText
        {
            get { return _odometerKmText; }
            set { _odometerKmText = value; OnPropertyChanged(); }
        }

        private string _selectedOperationalStatus = "Active";
        public string SelectedOperationalStatus
        {
            get { return _selectedOperationalStatus; }
            set { _selectedOperationalStatus = value; OnPropertyChanged(); }
        }

        private string _description = string.Empty;
        public string Description
        {
            get { return _description; }
            set { _description = value; OnPropertyChanged(); }
        }

        private string _addUrlInput = string.Empty;
        public string AddUrlInput
        {
            get { return _addUrlInput; }
            set { _addUrlInput = value; OnPropertyChanged(); }
        }

        private VehicleImageItemViewModel? _selectedImage;
        public VehicleImageItemViewModel? SelectedImage
        {
            get { return _selectedImage; }
            set
            {
                _selectedImage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewImageUrl));
            }
        }

        public string? PreviewImageUrl
        {
            get
            {
                if (SelectedImage != null)
                {
                    return SelectedImage.ImageUrl;
                }

                VehicleImageItemViewModel? primary = Images.FirstOrDefault(i => i.IsPrimary);
                if (primary != null)
                {
                    return primary.ImageUrl;
                }

                VehicleImageItemViewModel? first = Images.FirstOrDefault();
                return first != null ? first.ImageUrl : null;
            }
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get { return _errorMessage; }
            private set
            {
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError
        {
            get { return !string.IsNullOrWhiteSpace(ErrorMessage); }
        }

        private bool _isSaving;
        public bool IsSaving
        {
            get { return _isSaving; }
            private set
            {
                _isSaving = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public Func<string[]?>? PickLocalImageFiles { get; set; }
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
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            _service = service;

            BrowseLocalImagesCommand = new RelayCommand(_ => BrowseLocalImages(), _ => !IsSaving);
            AddImageUrlCommand = new RelayCommand(_ => AddImageFromInput(), _ => !IsSaving);
            SetPrimaryImageCommand = new RelayCommand(
                p => SetPrimaryImage((p as VehicleImageItemViewModel) ?? SelectedImage),
                p => !IsSaving && ((p is VehicleImageItemViewModel) || SelectedImage != null));
            MoveImageUpCommand = new RelayCommand(
                p => MoveImage((p as VehicleImageItemViewModel) ?? SelectedImage, -1),
                p => !IsSaving && CanMoveImage((p as VehicleImageItemViewModel) ?? SelectedImage, -1));
            MoveImageDownCommand = new RelayCommand(
                p => MoveImage((p as VehicleImageItemViewModel) ?? SelectedImage, 1),
                p => !IsSaving && CanMoveImage((p as VehicleImageItemViewModel) ?? SelectedImage, 1));
            RemoveImageCommand = new RelayCommand(
                p => RemoveImage((p as VehicleImageItemViewModel) ?? SelectedImage),
                p => !IsSaving && ((p is VehicleImageItemViewModel) || SelectedImage != null));
            SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync(), _ => !IsSaving);
            CancelCommand = new RelayCommand(
                _ =>
                {
                    if (RequestClose != null)
                    {
                        RequestClose(false);
                    }
                },
                _ => !IsSaving);
        }

        public async Task InitializeForCreateAsync()
        {
            EditingVehicleId = null;
            _originalOdometerKm = null;
            OnPropertyChanged(nameof(IsEditMode));
            OnPropertyChanged(nameof(WindowTitle));

            List<CategoryListItem> allCats = await _service.GetCategoriesAsync(false);
            Categories.Clear();
            foreach (CategoryListItem c in allCats)
            {
                Categories.Add(c);
            }

            CategoryListItem? firstCat = Categories.FirstOrDefault();
            SelectedCategoryId = firstCat != null ? firstCat.CategoryId : 0;
            ErrorMessage = Categories.Count == 0
                ? "Chưa có loại xe đang hoạt động. Vui lòng tạo hoặc bật ít nhất 1 loại xe trước."
                : string.Empty;
        }

        public async Task InitializeForEditAsync(long vehicleId)
        {
            VehicleDetailsDto? details = await _service.GetVehicleDetailsAsync(vehicleId);
            if (details == null)
            {
                throw new InvalidOperationException("Không tìm thấy xe cần chỉnh sửa.");
            }

            EditingVehicleId = details.VehicleId;
            _originalOdometerKm = details.OdometerKm;
            OnPropertyChanged(nameof(IsEditMode));
            OnPropertyChanged(nameof(WindowTitle));

            List<CategoryListItem> allCats = await _service.GetCategoriesAsync(true);
            Categories.Clear();
            foreach (CategoryListItem c in allCats.Where(c => c.IsActive || c.CategoryId == details.CategoryId))
            {
                Categories.Add(c);
            }

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
            Description = details.Description ?? string.Empty;

            Images.Clear();
            foreach (VehicleImageDto img in details.Images.OrderBy(i => i.SortOrder))
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
            ErrorMessage = string.Empty;
        }

        public static string GetUploadDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CRMS",
                "VehicleUploads");
        }

        public void ImportLocalImageFiles(IEnumerable<string> sourceFiles)
        {
            try
            {
                ErrorMessage = string.Empty;

                foreach (string sourceFile in sourceFiles)
                {
                    if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
                    {
                        throw new InvalidOperationException($"Không tìm thấy tệp ảnh: {sourceFile}");
                    }

                    string ext = Path.GetExtension(sourceFile);
                    bool supported = AllowedLocalImageExtensions.Any(e =>
                        string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
                    if (!supported)
                    {
                        throw new InvalidOperationException("Chỉ hỗ trợ tệp ảnh định dạng JPG, JPEG, PNG, WEBP hoặc BMP.");
                    }

                    var info = new FileInfo(sourceFile);
                    if (info.Length <= 0)
                    {
                        throw new InvalidOperationException($"Tệp ảnh rỗng hoặc không hợp lệ: {info.Name}");
                    }

                    if (info.Length > MaxImageFileBytes)
                    {
                        throw new InvalidOperationException($"Tệp ảnh '{info.Name}' vượt quá dung lượng tối đa 10 MB.");
                    }

                    string uri = VehicleRules.NormalizeImageUrl(sourceFile);
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
            if (PickLocalImageFiles == null)
            {
                return;
            }

            string[]? selectedFiles = PickLocalImageFiles();
            if (selectedFiles == null || selectedFiles.Length == 0)
            {
                return;
            }

            ImportLocalImageFiles(selectedFiles);
        }

        private void AddImageFromInput()
        {
            try
            {
                ErrorMessage = string.Empty;
                string uri = VehicleRules.NormalizeImageUrl(AddUrlInput);
                AppendImageUri(uri);
                AddUrlInput = string.Empty;
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private void AppendImageUri(string normalizedUri)
        {
            if (Images.Any(i => i.ImageUrl.Equals(normalizedUri, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Ảnh này đã có trong danh sách của xe.");
            }

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
            if (target == null)
            {
                return;
            }

            foreach (VehicleImageItemViewModel img in Images)
            {
                img.IsPrimary = ReferenceEquals(img, target);
            }

            SelectedImage = target;
            OnPropertyChanged(nameof(PreviewImageUrl));
        }

        private bool CanMoveImage(VehicleImageItemViewModel? item, int delta)
        {
            if (item == null)
            {
                return false;
            }

            int idx = Images.IndexOf(item);
            return idx >= 0 && idx + delta >= 0 && idx + delta < Images.Count;
        }

        private void MoveImage(VehicleImageItemViewModel? item, int delta)
        {
            if (item == null || !CanMoveImage(item, delta))
            {
                return;
            }

            int idx = Images.IndexOf(item);
            Images.Move(idx, idx + delta);
            RenumberImages();
            SelectedImage = item;
            CommandManager.InvalidateRequerySuggested();
        }

        private void RemoveImage(VehicleImageItemViewModel? item)
        {
            if (item == null)
            {
                return;
            }

            bool wasPrimary = item.IsPrimary;
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
            {
                Images[i].SortOrder = i;
            }
        }

        public async Task SaveAsync()
        {
            if (IsSaving)
            {
                return;
            }

            var copiedFiles = new List<CopiedImageBackup>();

            try
            {
                ErrorMessage = string.Empty;

                short year;
                if (!short.TryParse(ManufactureYearText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out year))
                {
                    throw new InvalidOperationException("Năm sản xuất phải là số nguyên hợp lệ (1990 - 2100).");
                }

                byte seats;
                if (!byte.TryParse(SeatCountText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seats))
                {
                    throw new InvalidOperationException("Số chỗ ngồi phải là số nguyên từ 1 đến 30.");
                }

                decimal dailyRate;
                if (!VehicleRules.TryParseVndAmount(DailyRateText, out dailyRate))
                {
                    throw new InvalidOperationException("Giá thuê theo ngày không hợp lệ.");
                }

                string normalizedOdoText = OdometerKmText.Trim().Replace(",", string.Empty).Replace(".", string.Empty);
                int odo;
                if (!int.TryParse(normalizedOdoText, NumberStyles.Integer, CultureInfo.InvariantCulture, out odo))
                {
                    throw new InvalidOperationException("Số công-tơ-mét (Odo) phải là số nguyên không âm.");
                }

                var request = new VehicleUpsertRequest(
                    EditingVehicleId,
                    SelectedCategoryId,
                    LicensePlate,
                    Brand,
                    Model,
                    year,
                    seats,
                    SelectedTransmission,
                    SelectedFuelType,
                    dailyRate,
                    odo,
                    SelectedOperationalStatus,
                    Description,
                    BuildImageInputs());

                VehicleRules.ValidateVehicle(request, _originalOdometerKm);

                IsSaving = true;
                PersistStagedLocalImages(copiedFiles);
                request.Images = BuildImageInputs();

                await _service.SaveVehicleAsync(request);
                if (RequestClose != null)
                {
                    RequestClose(true);
                }
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

        private List<VehicleImageInput> BuildImageInputs()
        {
            var list = new List<VehicleImageInput>(Images.Count);
            for (int i = 0; i < Images.Count; i++)
            {
                list.Add(new VehicleImageInput(Images[i].ImageUrl, Images[i].IsPrimary, i));
            }
            return list;
        }

        private void PersistStagedLocalImages(List<CopiedImageBackup> copiedFiles)
        {
            string uploadDir = Path.GetFullPath(GetUploadDirectory());
            string uploadPrefix = uploadDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? uploadDir
                : uploadDir + Path.DirectorySeparatorChar;

            foreach (VehicleImageItemViewModel item in Images)
            {
                Uri? uri;
                if (!Uri.TryCreate(item.ImageUrl, UriKind.Absolute, out uri) || uri == null || !uri.IsFile)
                {
                    continue;
                }

                string localPath = Path.GetFullPath(uri.LocalPath);
                if (localPath.StartsWith(uploadPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Directory.CreateDirectory(uploadDir);
                string ext = Path.GetExtension(localPath);
                string safeName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
                string destination = Path.Combine(uploadDir, safeName);
                File.Copy(localPath, destination, true);

                string originalUri = item.ImageUrl;
                item.ImageUrl = new Uri(destination).AbsoluteUri;
                copiedFiles.Add(new CopiedImageBackup(item, originalUri, destination));
            }

            OnPropertyChanged(nameof(PreviewImageUrl));
        }

        private void RollbackCopiedImages(List<CopiedImageBackup> copiedFiles)
        {
            foreach (CopiedImageBackup backup in copiedFiles)
            {
                backup.Item.ImageUrl = backup.OriginalUri;
                try
                {
                    if (File.Exists(backup.CopiedPath))
                    {
                        File.Delete(backup.CopiedPath);
                    }
                }
                catch
                {
                    // Ignore best-effort temp cleanup failure
                }
            }

            if (copiedFiles.Count > 0)
            {
                OnPropertyChanged(nameof(PreviewImageUrl));
            }
        }
    }
}
