
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CRMS.Data;
using CRMS.Helpers;
using CRMS.Models;
using CRMS.Services;
using CRMS.Views.Admin;

namespace CRMS.ViewModels
{
    public class VoucherManagementViewModel : BaseViewModel
    {
        private readonly VoucherService _service;

        public ObservableCollection<Voucher> Vouchers { get; } = new();

        private Voucher? _selectedVoucher;

        public Voucher? SelectedVoucher
        {
            get => _selectedVoucher;
            set
            {
                if (_selectedVoucher == value)
                    return;

                _selectedVoucher = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value)
                    return;

                _isBusy = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _statusMessage = "Sẵn sàng.";

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage == value)
                    return;

                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DisableCommand { get; }
        public ICommand EnableCommand { get; }

        // Constructor moi: khong can Repository
        public VoucherManagementViewModel()
        {
            _service = new VoucherService();

            RefreshCommand = new RelayCommand(
                async _ => await LoadAsync(),
                _ => !IsBusy);

            AddCommand = new RelayCommand(
                async _ => await AddVoucherAsync(),
                _ => !IsBusy &&
                     CurrentUserService.Instance.IsAdmin);

            EditCommand = new RelayCommand(
                async _ => await EditVoucherAsync(),
                _ => !IsBusy &&
                     SelectedVoucher != null &&
                     CurrentUserService.Instance.IsAdmin);

            DisableCommand = new RelayCommand(
                async _ => await SetActiveAsync(false),
                _ => !IsBusy &&
                     SelectedVoucher != null &&
                     SelectedVoucher.IsActive &&
                     CurrentUserService.Instance.IsAdmin);

            EnableCommand = new RelayCommand(
                async _ => await SetActiveAsync(true),
                _ => !IsBusy &&
                     SelectedVoucher != null &&
                     !SelectedVoucher.IsActive &&
                     CurrentUserService.Instance.IsAdmin);
        }

        // Giu constructor cu de tuong thich voi View
        // DbContext khong duoc luu lau dai trong ViewModel
        public VoucherManagementViewModel(CrmsContext db)
            : this()
        {
            ArgumentNullException.ThrowIfNull(db);
        }

        // F20 - Tai danh sach Voucher
        public async Task LoadAsync()
        {
            if (IsBusy)
                return;

            if (!CurrentUserService.Instance.IsAdmin)
            {
                Vouchers.Clear();
                SelectedVoucher = null;
                StatusMessage =
                    "Chỉ Admin được quản lý Voucher.";
                return;
            }

            IsBusy = true;

            try
            {
                var data = await _service.GetAllAsync();

                Vouchers.Clear();

                foreach (var voucher in data)
                {
                    Vouchers.Add(voucher);
                }

                SelectedVoucher = null;

                StatusMessage =
                    $"Đã tải {Vouchers.Count} Voucher.";
            }
            catch (Exception ex)
            {
                StatusMessage =
                    "Lỗi tải Voucher: " +
                    ex.GetBaseException().Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        // F20 - Them Voucher
        private async Task AddVoucherAsync()
        {
            if (IsBusy ||
                !CurrentUserService.Instance.IsAdmin)
                return;

            var window = new VoucherEditorWindow
            {
                Owner = Application.Current.MainWindow
            };

            if (window.ShowDialog() != true ||
                window.ResultVoucher == null)
                return;

            IsBusy = true;

            bool success = false;
            string message;

            try
            {
                await _service.AddAsync(
                    window.ResultVoucher);

                success = true;
                message = "Thêm Voucher thành công.";
            }
            catch (Exception ex)
            {
                message =
                    "Lỗi thêm Voucher: " +
                    ex.GetBaseException().Message;
            }
            finally
            {
                IsBusy = false;
            }

            if (success)
            {
                await LoadAsync();
            }

            StatusMessage = message;

            MessageBox.Show(
                message,
                "Quản lý Voucher",
                MessageBoxButton.OK,
                success
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning);
        }

        // F20 - Sua Voucher
        private async Task EditVoucherAsync()
        {
            if (IsBusy ||
                SelectedVoucher == null ||
                !CurrentUserService.Instance.IsAdmin)
                return;

            var selected = SelectedVoucher;

            var window = new VoucherEditorWindow(selected)
            {
                Owner = Application.Current.MainWindow
            };

            if (window.ShowDialog() != true ||
                window.ResultVoucher == null)
                return;

            IsBusy = true;

            bool success = false;
            string message;

            try
            {
                await _service.UpdateAsync(
                    window.ResultVoucher);

                success = true;
                message =
                    "Cập nhật Voucher thành công.";
            }
            catch (Exception ex)
            {
                message =
                    "Lỗi sửa Voucher: " +
                    ex.GetBaseException().Message;
            }
            finally
            {
                IsBusy = false;
            }

            if (success)
            {
                await LoadAsync();
            }

            StatusMessage = message;

            MessageBox.Show(
                message,
                "Quản lý Voucher",
                MessageBoxButton.OK,
                success
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning);
        }

        // F20 - Kich hoat / Vo hieu hoa
        private async Task SetActiveAsync(bool isActive)
        {
            if (IsBusy || SelectedVoucher == null)
                return;

            if (!CurrentUserService.Instance.IsAdmin)
            {
                StatusMessage =
                    "Bạn không có quyền quản lý Voucher.";
                return;
            }

            var voucher = SelectedVoucher;

            IsBusy = true;

            bool success = false;
            string message;

            try
            {
                await _service.SetActiveAsync(
                    voucher.VoucherId,
                    isActive,
                    voucher.RowVersion);

                success = true;

                message = isActive
                    ? "Kích hoạt Voucher thành công."
                    : "Vô hiệu hóa Voucher thành công.";
            }
            catch (Exception ex)
            {
                message =
                    "Lỗi cập nhật: " +
                    ex.GetBaseException().Message;
            }
            finally
            {
                IsBusy = false;
            }

            if (success)
            {
                await LoadAsync();
            }

            StatusMessage = message;
        }
    }
}
