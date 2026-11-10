
using System;
using System.Windows;
using System.Windows.Controls;
using CRMS.Data;
using CRMS.ViewModels;

namespace CRMS.Views.Admin
{
    public partial class VoucherManagementPage : Page
    {
        private readonly VoucherManagementViewModel _vm;

        public VoucherManagementPage()
        {
            InitializeComponent();

            // ViewModel vẫn nhận CrmsContext để tương thích
            // với constructor hiện tại.
            // Repository mới không giữ DbContext này.
            using (var db = CrmsContextFactory.Create())
            {
                _vm = new VoucherManagementViewModel(db);
            }

            DataContext = _vm;

            Loaded += VoucherManagementPage_Loaded;
        }

        private async void VoucherManagementPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            // Tránh tải lại khi sự kiện Loaded xảy ra nhiều lần
            Loaded -= VoucherManagementPage_Loaded;

            try
            {
                await _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không thể tải danh sách Voucher:\n" +
                    ex.GetBaseException().Message,
                    "Quản lý Voucher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
