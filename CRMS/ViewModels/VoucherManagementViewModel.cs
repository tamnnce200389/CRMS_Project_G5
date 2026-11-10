
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CRMS.Data;
using CRMS.Helpers;
using CRMS.Models;
using CRMS.Repositories;
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
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DisableCommand { get; }
        public ICommand EnableCommand { get; }

        public VoucherManagementViewModel(CrmsContext db)
        {
            var repository = new VoucherRepository(db);
            _service = new VoucherService(repository);

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
                     SelectedVoucher.IsActive);

            EnableCommand = new RelayCommand(
                async _ => await SetActiveAsync(true),
                _ => !IsBusy &&
                     SelectedVoucher != null &&
                     !SelectedVoucher.IsActive);
        }

        public async Task LoadAsync()
        {
            if (IsBusy)
                return;

            if (!CurrentUserService.Instance.IsAdmin)
            {
                Vouchers.Clear();
                SelectedVoucher = null;
                StatusMessage = "Chỉ Admin được quản lý Voucher.";
                return;
            }

            IsBusy = true;

            try
            {
                var data = await _service.GetAllAsync();

                Vouchers.Clear();

                foreach (var voucher in data)
                    Vouchers.Add(voucher);

                SelectedVoucher = null;
                StatusMessage = $"Đã tải {Vouchers.Count} Voucher.";
            }
            catch (Exception ex)
            {
                StatusMessage =
                    "Lỗi tải Voucher: " + ex.GetBaseException().Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task AddVoucherAsync()
        {
            if (IsBusy || !CurrentUserService.Instance.IsAdmin)
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
                await _service.AddAsync(window.ResultVoucher);
                success = true;
                message = "Thêm Voucher thành công.";
            }
            catch (Exception ex)
            {
                message = "Lỗi thêm Voucher: " +
                          ex.GetBaseException().Message;
            }
            finally
            {
                IsBusy = false;
            }

            if (success)
                await LoadAsync();

            StatusMessage = message;

            MessageBox.Show(
                message,
                "Quản lý Voucher",
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information
                        : MessageBoxImage.Warning);
        }

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
                await _service.UpdateAsync(window.ResultVoucher);
                success = true;
                message = "Cập nhật Voucher thành công.";
            }
            catch (Exception ex)
            {
                message = "Lỗi sửa Voucher: " +
                          ex.GetBaseException().Message;
            }
            finally
            {
                IsBusy = false;
            }

            if (success)
                await LoadAsync();

            StatusMessage = message;

            MessageBox.Show(
                message,
                "Quản lý Voucher",
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information
                        : MessageBoxImage.Warning);
        }

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
                message = "Cập nhật trạng thái Voucher thành công.";
            }
            catch (Exception ex)
            {
                message = "Lỗi cập nhật: " +
                          ex.GetBaseException().Message;
            }
            finally
            {
                IsBusy = false;
            }

            if (success)
                await LoadAsync();

            StatusMessage = message;
        }
    }
}
