using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services.Vehicles;

namespace CRMS.ViewModels
{
    public class CategoryManagementViewModel : BaseViewModel
    {
        private readonly IVehicleService _service;

        public ObservableCollection<CategoryListItem> Categories { get; } = new ObservableCollection<CategoryListItem>();

        public bool HasChanges { get; private set; }

        private CategoryListItem? _selectedCategory;
        public CategoryListItem? SelectedCategory
        {
            get { return _selectedCategory; }
            set
            {
                _selectedCategory = value;
                OnPropertyChanged();
                ErrorMessage = string.Empty;

                if (value != null)
                {
                    EditingCategoryId = value.CategoryId;
                    CategoryName = value.CategoryName;
                    Description = value.Description ?? string.Empty;
                    IsActive = value.IsActive;
                }
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private int? _editingCategoryId;
        public int? EditingCategoryId
        {
            get { return _editingCategoryId; }
            private set
            {
                _editingCategoryId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormTitle));
            }
        }

        public string FormTitle
        {
            get
            {
                return EditingCategoryId.HasValue
                    ? $"Chỉnh sửa loại xe #{EditingCategoryId}"
                    : "Thêm loại xe mới";
            }
        }

        private string _categoryName = string.Empty;
        public string CategoryName
        {
            get { return _categoryName; }
            set
            {
                _categoryName = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _description = string.Empty;
        public string Description
        {
            get { return _description; }
            set { _description = value; OnPropertyChanged(); }
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get { return _isActive; }
            set { _isActive = value; OnPropertyChanged(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
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
            get { return _statusMessage; }
            private set { _statusMessage = value; OnPropertyChanged(); }
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

        public int TotalCategories
        {
            get { return Categories.Count; }
        }

        public int ActiveCategories
        {
            get { return Categories.Count(c => c.IsActive); }
        }

        public event Action? RequestClose;

        public ICommand RefreshCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ToggleActiveCommand { get; }
        public ICommand CloseCommand { get; }

        public CategoryManagementViewModel()
            : this(new VehicleService())
        {
        }

        public CategoryManagementViewModel(IVehicleService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            _service = service;

            RefreshCommand = new AsyncRelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
            NewCommand = new RelayCommand(_ => ResetForm(), _ => !IsBusy);
            SaveCommand = new AsyncRelayCommand(
                async _ => await SaveAsync(),
                _ => !IsBusy && !string.IsNullOrWhiteSpace(CategoryName));
            ToggleActiveCommand = new AsyncRelayCommand(
                async _ => await ToggleSelectedAsync(),
                _ => !IsBusy && SelectedCategory != null);
            CloseCommand = new RelayCommand(
                _ =>
                {
                    if (RequestClose != null)
                    {
                        RequestClose();
                    }
                },
                _ => !IsBusy);
        }

        public async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                int? selectedId = EditingCategoryId;
                List<CategoryListItem> items = await _service.GetCategoriesAsync(true);

                Categories.Clear();
                foreach (CategoryListItem item in items)
                {
                    Categories.Add(item);
                }

                OnPropertyChanged(nameof(TotalCategories));
                OnPropertyChanged(nameof(ActiveCategories));

                if (selectedId.HasValue)
                {
                    SelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == selectedId.Value);
                }

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
            CategoryName = string.Empty;
            Description = string.Empty;
            IsActive = true;
            ErrorMessage = string.Empty;
            StatusMessage = "Đang ở chế độ thêm loại xe mới.";
        }

        public async Task SaveAsync()
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                ErrorMessage = string.Empty;
                var req = new CategoryUpsertRequest(
                    EditingCategoryId,
                    CategoryName,
                    Description,
                    IsActive);

                VehicleRules.ValidateCategory(req);

                IsBusy = true;
                int savedId = await _service.SaveCategoryAsync(req);
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
            if (SelectedCategory == null || IsBusy)
            {
                return;
            }

            try
            {
                ErrorMessage = string.Empty;
                bool targetActive = !SelectedCategory.IsActive;
                int id = SelectedCategory.CategoryId;

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
}
