using System.Collections.ObjectModel;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services.Vehicles;

namespace CRMS.ViewModels;

public sealed class CategoryManagementViewModel : BaseViewModel
{
    private readonly IVehicleService _service;

    public ObservableCollection<CategoryListItem> Categories { get; } = new();

    public bool HasChanges { get; private set; }

    private CategoryListItem? _selectedCategory;
    public CategoryListItem? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged();
            ErrorMessage = "";

            if (value != null)
            {
                EditingCategoryId = value.CategoryId;
                CategoryName = value.CategoryName;
                Description = value.Description ?? "";
                IsActive = value.IsActive;
            }
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private int? _editingCategoryId;
    public int? EditingCategoryId
    {
        get => _editingCategoryId;
        private set
        {
            _editingCategoryId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormTitle));
        }
    }

    public string FormTitle => EditingCategoryId.HasValue
        ? $"Chỉnh sửa loại xe #{EditingCategoryId}"
        : "Thêm loại xe mới";

    private string _categoryName = "";
    public string CategoryName
    {
        get => _categoryName;
        set
        {
            _categoryName = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _description = "";
    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    private bool _isActive = true;
    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            _isBusy = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _statusMessage = "Chọn một loại xe để sửa hoặc nhập mới ở khung bên phải.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set { _statusMessage = value; OnPropertyChanged(); }
    }

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

    public int TotalCategories => Categories.Count;
    public int ActiveCategories => Categories.Count(c => c.IsActive);

    public ICommand RefreshCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ToggleActiveCommand { get; }

    public CategoryManagementViewModel()
        : this(new VehicleService())
    {
    }

    public CategoryManagementViewModel(IVehicleService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));

        RefreshCommand = new AsyncRelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
        NewCommand = new RelayCommand(_ => ResetForm(), _ => !IsBusy);
        SaveCommand = new AsyncRelayCommand(
            async _ => await SaveAsync(),
            _ => !IsBusy && !string.IsNullOrWhiteSpace(CategoryName));
        ToggleActiveCommand = new AsyncRelayCommand(
            async _ => await ToggleSelectedAsync(),
            _ => !IsBusy && SelectedCategory != null);
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            ErrorMessage = "";
            var selectedId = EditingCategoryId;
            var items = await _service.GetCategoriesAsync(includeInactive: true);

            Categories.Clear();
            foreach (var item in items)
                Categories.Add(item);

            OnPropertyChanged(nameof(TotalCategories));
            OnPropertyChanged(nameof(ActiveCategories));

            if (selectedId.HasValue)
                SelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == selectedId.Value);

            StatusMessage = $"Hiện có {TotalCategories} loại xe ({ActiveCategories} đang hoạt động).";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Không thể tải danh sách loại xe.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public void ResetForm()
    {
        _selectedCategory = null;
        OnPropertyChanged(nameof(SelectedCategory));
        EditingCategoryId = null;
        CategoryName = "";
        Description = "";
        IsActive = true;
        ErrorMessage = "";
        StatusMessage = "Đang ở chế độ thêm loại xe mới.";
    }

    public async Task SaveAsync()
    {
        if (IsBusy)
            return;

        try
        {
            ErrorMessage = "";
            var req = new CategoryUpsertRequest(
                CategoryId: EditingCategoryId,
                CategoryName: CategoryName,
                Description: Description,
                IsActive: IsActive);

            VehicleRules.ValidateCategory(req);

            IsBusy = true;
            var savedId = await _service.SaveCategoryAsync(req);
            HasChanges = true;
            IsBusy = false;

            await LoadAsync();
            SelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == savedId);
            StatusMessage = "Đã lưu thông tin loại xe thành công.";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Không thể lưu loại xe. Vui lòng thử lại.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task ToggleSelectedAsync()
    {
        if (SelectedCategory is null || IsBusy)
            return;

        try
        {
            ErrorMessage = "";
            var targetActive = !SelectedCategory.IsActive;
            var id = SelectedCategory.CategoryId;

            IsBusy = true;
            await _service.SetCategoryActiveAsync(id, targetActive);
            HasChanges = true;
            IsBusy = false;

            await LoadAsync();
            SelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == id);
            StatusMessage = targetActive
                ? "Đã kích hoạt lại loại xe."
                : "Đã ngừng hoạt động loại xe.";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Không thể thay đổi trạng thái loại xe.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
