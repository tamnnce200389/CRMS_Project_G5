using CRMS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CRMS.Views.Admin
{
    public partial class ReportPage : Page
    {
        private readonly BookingService _service = new();
        private List<PaymentSummaryItem> _allItems = new();
        private bool _initialized;

        public ReportPage()
        {
            InitializeComponent();
            Loaded += async (_, _) =>
            {
                _initialized = true;
                await LoadDataAsync();
            };
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var report = await _service.GetPaymentSummaryAsync();

                // Nếu là Customer thì chỉ xem các đơn của chính mình; Staff/Admin xem toàn hệ thống.
                var user = CurrentUserService.Instance;
                if (user.IsCustomer && user.UserId.HasValue)
                {
                    var myBookings = await _service.GetForCustomerAsync(user.UserId.Value);
                    var myIds = myBookings.Select(b => b.BookingId).ToHashSet();
                    _allItems = report.Items.Where(i => myIds.Contains(i.BookingId)).ToList();
                }
                else
                {
                    _allItems = report.Items;
                }

                ValidRevenueText.Text = $"{report.TotalValidBookingAmount:N0} đ";
                TotalBookingsSubText.Text = $"Tổng cộng {report.TotalBookingsCount} đơn thuê trên hệ thống";
                CollectedDepositText.Text = $"{report.TotalDepositCollected:N0} đ";
                PendingDepositText.Text = $"{report.TotalDepositPending:N0} đ";
                PendingRefundText.Text = $"{report.TotalRefundPending:N0} đ";
                CompletedRefundSubText.Text = $"Đã hoàn tất: {report.TotalRefundCompleted:N0} đ";

                ApplyFilters();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Không tải được dữ liệu tổng hợp thanh toán.";
                MessageBox.Show(ex.Message, "Lỗi tải dữ liệu", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters()
        {
            if (!_initialized) return;

            IEnumerable<PaymentSummaryItem> query = _allItems;

            var statusFilter = (PaymentFilterBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tất cả trạng thái TT";
            query = statusFilter switch
            {
                "Đã thu cọc" => query.Where(i => i.PaymentStatus == "Đã thu cọc"),
                "Chưa thanh toán" => query.Where(i => i.PaymentStatus is "Chưa thanh toán" or "Chờ thanh toán"),
                "Chờ hoàn cọc" => query.Where(i => i.PaymentStatus == "Chờ hoàn cọc"),
                "Đã hoàn cọc" => query.Where(i => i.PaymentStatus == "Đã hoàn cọc"),
                "Failed / Cancelled" => query.Where(i => i.PaymentStatus is "Failed" or "Cancelled"),
                _ => query
            };

            var methodFilter = (MethodFilterBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tất cả phương thức";
            query = methodFilter switch
            {
                "VNPay" => query.Where(i => i.DepositMethod == "VNPay"),
                "Tiền mặt" => query.Where(i => i.DepositMethod == "Cash"),
                _ => query
            };

            var rentalTypeFilter = (RentalTypeFilterBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tất cả loại thuê";
            query = rentalTypeFilter switch
            {
                "Tự lái" => query.Where(i => i.RentalType == "Tự lái"),
                "Có tài xế" => query.Where(i => i.RentalType == "Có tài xế"),
                _ => query
            };

            var keyword = SearchBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(i =>
                    i.BookingCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    i.CustomerName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    i.VehicleName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    i.LicensePlate.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            var filtered = query.ToList();
            SummaryGrid.ItemsSource = filtered;
            StatusText.Text = $"Hiển thị {filtered.Count}/{_allItems.Count} đơn.";
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadDataAsync();

        private async void ConfirmCashDeposit_Click(object sender, RoutedEventArgs e)
        {
            if (!CurrentUserService.Instance.IsStaff && !CurrentUserService.Instance.IsAdmin)
            {
                MessageBox.Show("Chỉ Staff/Admin mới được xác nhận thu cọc tiền mặt.");
                return;
            }

            if (SummaryGrid.SelectedItem is not PaymentSummaryItem selected)
            {
                MessageBox.Show("Hãy chọn một dòng đơn thuê trong bảng.");
                return;
            }

            if (!selected.CanConfirmCashDeposit)
            {
                MessageBox.Show(
                    "Chỉ đơn đặt cọc bằng Tiền mặt và đang ở trạng thái PendingDeposit mới xác nhận thu cọc tại đây.",
                    "Không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Xác nhận đã thu đủ tiền cọc {selected.DepositAmount:N0} đ (Tiền mặt) cho đơn {selected.BookingCode}?",
                "Xác nhận thu cọc", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var actorId = CurrentUserService.Instance.UserId
                    ?? throw new InvalidOperationException("Không xác định được tài khoản Staff/Admin hiện tại.");

                await _service.ConfirmCashDepositAsync(selected.BookingId, actorId);
                await LoadDataAsync();
                MessageBox.Show($"Đã xác nhận thu cọc tiền mặt cho đơn {selected.BookingCode}.",
                    "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Không thể xác nhận cọc", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void ConfirmRefund_Click(object sender, RoutedEventArgs e)
        {
            if (!CurrentUserService.Instance.IsStaff && !CurrentUserService.Instance.IsAdmin)
            {
                MessageBox.Show("Chỉ Staff/Admin mới được xác nhận hoàn tiền cọc.");
                return;
            }

            if (SummaryGrid.SelectedItem is not PaymentSummaryItem selected)
            {
                MessageBox.Show("Hãy chọn một dòng đơn đang ở trạng thái 'Chờ hoàn cọc' trong bảng.");
                return;
            }

            if (!selected.CanConfirmRefund)
            {
                MessageBox.Show(
                    "Chỉ những đơn đã hủy có trạng thái 'Chờ hoàn cọc' mới cần xác nhận hoàn tiền.",
                    "Không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Xác nhận đã hoàn trả tiền cọc {selected.DepositAmount:N0} đ cho khách hàng của đơn {selected.BookingCode}?",
                "Xác nhận hoàn cọc", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var actorId = CurrentUserService.Instance.UserId
                    ?? throw new InvalidOperationException("Không xác định được tài khoản Staff/Admin hiện tại.");

                await _service.ConfirmRefundAsync(selected.BookingId, actorId);
                await LoadDataAsync();
                MessageBox.Show($"Đã xác nhận hoàn cọc thành công cho đơn {selected.BookingCode}.",
                    "Hoàn cọc thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Không thể xác nhận hoàn cọc", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
