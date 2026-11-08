
using System;
using System.Windows;
using CRMS.Services;
using CRMS.ViewModels;

namespace CRMS.Views.Customer;

public partial class BookingCheckoutWindow : Window
{
    public BookingCheckoutViewModel ViewModel { get; }

    public BookingCheckoutWindow(
        BookingSelection selection,
        string vehicleName,
        CustomerBookingService? service = null)
    {
        InitializeComponent();

        ViewModel = new BookingCheckoutViewModel(
            selection,
            vehicleName,
            service
        );

        DataContext = ViewModel;

        ViewModel.Completed += () =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                bool voucherFailed =
                    !string.IsNullOrWhiteSpace(
                        ViewModel.VoucherMessage
                    )
                    && !ViewModel.VoucherMessage.StartsWith(
                        "Áp dụng thành công!",
                        StringComparison.Ordinal
                    )
                    && !ViewModel.VoucherMessage.StartsWith(
                        "Đã ghi nhận mã",
                        StringComparison.Ordinal
                    );

                string title = voucherFailed
                    ? "Đơn đã tạo - Voucher chưa áp dụng"
                    : "Đã tạo đơn thuê";

                MessageBoxImage image = voucherFailed
                    ? MessageBoxImage.Warning
                    : MessageBoxImage.Information;

                MessageBox.Show(
                    this,
                    ViewModel.Status,
                    title,
                    MessageBoxButton.OK,
                    image
                );

                DialogResult = true;
            }));

        Closing += (_, e) =>
            e.Cancel = !ViewModel.CanClose;
    }
}
