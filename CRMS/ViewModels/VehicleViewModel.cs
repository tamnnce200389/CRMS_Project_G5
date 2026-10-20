
using CRMS.Helpers;
using CRMS.Models;
using CRMS.Repositories;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace CRMS.ViewModels
{
    public class VehicleViewModel : BaseViewModel
    {
        private readonly VehicleRepository _repository = new();

        public ObservableCollection<Vehicle> Vehicles { get; } = new();

        public ICollectionView VehiclesView { get; }

        public ObservableCollection<string> StatusOptions { get; } = new()
        {
            "Tất cả trạng thái"
        };

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                VehiclesView.Refresh();
                OnPropertyChanged(nameof(FilteredCount));
            }
        }

        private string _selectedStatus = "Tất cả trạng thái";
        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                _selectedStatus = value;
                OnPropertyChanged();
                VehiclesView.Refresh();
                OnPropertyChanged(nameof(FilteredCount));
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public int TotalVehicles => Vehicles.Count;
        private string _statusMessage = "Sẵn sàng tải danh sách xe.";
        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        public int FilteredCount =>
            VehiclesView.Cast<Vehicle>().Count();

        public ICommand RefreshCommand { get; }

        public VehicleViewModel()
        {
            VehiclesView = CollectionViewSource.GetDefaultView(Vehicles);
            VehiclesView.Filter = FilterVehicle;

            RefreshCommand = new RelayCommand(
                async _ => await LoadVehiclesAsync(),
                _ => !IsLoading);
        }

        private bool FilterVehicle(object obj)
        {
            if (obj is not Vehicle vehicle)
                return false;

            string keyword = SearchText.Trim();

            bool matchesSearch =
                string.IsNullOrEmpty(keyword) ||
                vehicle.LicensePlate.Contains(
                    keyword, StringComparison.OrdinalIgnoreCase) ||
                vehicle.Brand.Contains(
                    keyword, StringComparison.OrdinalIgnoreCase) ||
                vehicle.Model.Contains(
                    keyword, StringComparison.OrdinalIgnoreCase);

            bool matchesStatus =
                SelectedStatus == "Tất cả trạng thái" ||
                vehicle.OperationalStatus == SelectedStatus;

            return matchesSearch && matchesStatus;
        }


        public async Task LoadVehiclesAsync()
        {
            if (IsLoading)
                return;

            try
            {
                IsLoading = true;
                StatusMessage = "Đang tải danh sách xe…";

                // Lay du lieu moi tu SQL Server
                var data = await _repository.GetAllAsync();

                // Cap nhat danh sach xe
                Vehicles.Clear();

                foreach (var vehicle in data)
                {
                    Vehicles.Add(vehicle);
                }

                // Cap nhat cac trang thai trong ComboBox
                string previousStatus = SelectedStatus;

                StatusOptions.Clear();
                StatusOptions.Add("Tất cả trạng thái");

                foreach (var status in data
                    .Select(v => v.OperationalStatus)
                    .Distinct()
                    .OrderBy(s => s))
                {
                    StatusOptions.Add(status);
                }

                SelectedStatus =
                    StatusOptions.Contains(previousStatus)
                        ? previousStatus
                        : "Tất cả trạng thái";

                // Cap nhat lai DataGrid
                VehiclesView.Refresh();

                OnPropertyChanged(nameof(TotalVehicles));
                OnPropertyChanged(nameof(FilteredCount));

                StatusMessage = $"Cập nhật lúc {DateTime.Now:HH:mm} · {data.Count} xe";
            }
            catch (Exception ex)
            {
                StatusMessage = "Không thể tải danh sách xe. Kiểm tra kết nối cơ sở dữ liệu và nhấn Làm mới để thử lại.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsLoading = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

    }
}

