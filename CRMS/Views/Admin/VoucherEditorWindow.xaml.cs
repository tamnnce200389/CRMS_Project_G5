
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CRMS.Models;

namespace CRMS.Views.Admin
{
    public partial class VoucherEditorWindow : Window
    {
        private readonly Voucher? _originalVoucher;

        public Voucher? ResultVoucher { get; private set; }

        public VoucherEditorWindow(Voucher? voucher = null)
        {
            InitializeComponent();

            _originalVoucher = voucher;

            if (voucher == null)
            {
                Title = "Thêm Voucher";
                txtTitle.Text = "Thêm Voucher mới";

                dpStartsAt.SelectedDate = DateTime.Today;
                dpEndsAt.SelectedDate = DateTime.Today.AddDays(30);

                txtMinimumOrder.Text = "0";
                txtPerCustomerLimit.Text = "1";
                chkIsActive.IsChecked = true;
            }
            else
            {
                Title = "Sửa Voucher";
                txtTitle.Text = "Chỉnh sửa Voucher";

                txtVoucherCode.Text = voucher.VoucherCode;

                cboDiscountType.SelectedIndex =
                    voucher.DiscountType == "Fixed" ? 1 : 0;

                txtDiscountValue.Text =
                    voucher.DiscountValue.ToString(
                        CultureInfo.InvariantCulture);

                txtMaxDiscount.Text =
                    voucher.MaxDiscountAmount?.ToString(
                        CultureInfo.InvariantCulture) ?? "";

                txtMinimumOrder.Text =
                    voucher.MinimumOrderAmount.ToString(
                        CultureInfo.InvariantCulture);

                dpStartsAt.SelectedDate = voucher.StartsAt.Date;
                dpEndsAt.SelectedDate = voucher.EndsAt.Date;

                txtUsageLimit.Text =
                    voucher.UsageLimit?.ToString() ?? "";

                txtPerCustomerLimit.Text =
                    voucher.PerCustomerLimit.ToString();

                chkIsActive.IsChecked = voucher.IsActive;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string code = txtVoucherCode.Text.Trim();

            string discountType =
                (cboDiscountType.SelectedItem as ComboBoxItem)?
                .Content?.ToString() ?? "Percent";

            if (string.IsNullOrWhiteSpace(code))
            {
                ShowError("Vui lòng nhập mã Voucher.");
                return;
            }

            if (code.Length > 40)
            {
                ShowError("Mã Voucher tối đa 40 ký tự.");
                return;
            }

            if (!decimal.TryParse(
                    txtDiscountValue.Text,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal discountValue))
            {
                ShowError(
                    "Giá trị giảm không hợp lệ. Ví dụ: 10 hoặc 100000.");
                return;
            }

            if (!decimal.TryParse(
                    txtMinimumOrder.Text,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal minimumOrder))
            {
                ShowError("Giá trị đơn hàng tối thiểu không hợp lệ.");
                return;
            }

            decimal? maxDiscount = null;

            if (!string.IsNullOrWhiteSpace(txtMaxDiscount.Text))
            {
                if (!decimal.TryParse(
                        txtMaxDiscount.Text,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out decimal maxValue))
                {
                    ShowError("Mức giảm tối đa không hợp lệ.");
                    return;
                }

                maxDiscount = maxValue;
            }

            int? usageLimit = null;

            if (!string.IsNullOrWhiteSpace(txtUsageLimit.Text))
            {
                if (!int.TryParse(
                        txtUsageLimit.Text,
                        out int usage))
                {
                    ShowError("Tổng lượt sử dụng phải là số nguyên.");
                    return;
                }

                usageLimit = usage;
            }

            if (!int.TryParse(
                    txtPerCustomerLimit.Text,
                    out int perCustomerLimit))
            {
                ShowError("Giới hạn mỗi khách phải là số nguyên.");
                return;
            }

            if (!dpStartsAt.SelectedDate.HasValue ||
                !dpEndsAt.SelectedDate.HasValue)
            {
                ShowError(
                    "Vui lòng chọn ngày bắt đầu và ngày kết thúc.");
                return;
            }

            // Lay ngay tu DatePicker
            DateTime startsAt =
                dpStartsAt.SelectedDate.Value.Date;

            DateTime endsAt =
                dpEndsAt.SelectedDate.Value.Date;

            if (_originalVoucher != null)
            {
                // Khi sua: giu nguyen gio, phut, giay cu
                startsAt = startsAt.Add(
                    _originalVoucher.StartsAt.TimeOfDay);

                endsAt = endsAt.Add(
                    _originalVoucher.EndsAt.TimeOfDay);
            }
            else
            {
                // Khi them moi: ket thuc luc 23:59:59
                endsAt = endsAt.AddDays(1).AddSeconds(-1);
            }

            if (endsAt <= startsAt)
            {
                ShowError(
                    "Ngày kết thúc phải sau ngày bắt đầu.");
                return;
            }

            if (discountValue <= 0)
            {
                ShowError(
                    "Giá trị giảm phải lớn hơn 0.");
                return;
            }

            if (discountType == "Percent" &&
                discountValue > 100)
            {
                ShowError(
                    "Phần trăm giảm không được vượt quá 100%.");
                return;
            }

            if (minimumOrder < 0)
            {
                ShowError(
                    "Đơn hàng tối thiểu không được âm.");
                return;
            }

            if (maxDiscount.HasValue &&
                maxDiscount.Value <= 0)
            {
                ShowError(
                    "Mức giảm tối đa phải lớn hơn 0.");
                return;
            }

            if (usageLimit.HasValue &&
                usageLimit.Value <= 0)
            {
                ShowError(
                    "Tổng lượt sử dụng phải lớn hơn 0.");
                return;
            }

            if (perCustomerLimit <= 0)
            {
                ShowError(
                    "Giới hạn mỗi khách phải lớn hơn 0.");
                return;
            }

            ResultVoucher = new Voucher
            {
                VoucherId =
                    _originalVoucher?.VoucherId ?? 0,

                VoucherCode = code,

                DiscountType = discountType,

                DiscountValue = discountValue,

                MaxDiscountAmount = maxDiscount,

                MinimumOrderAmount = minimumOrder,

                StartsAt = startsAt,

                EndsAt = endsAt,

                UsageLimit = usageLimit,

                PerCustomerLimit = perCustomerLimit,

                IsActive = chkIsActive.IsChecked == true,

                RowVersion =
                    _originalVoucher?.RowVersion
                    ?? Array.Empty<byte>()
            };

            DialogResult = true;
        }

        private void ShowError(string message)
        {
            MessageBox.Show(
                message,
                "Dữ liệu không hợp lệ",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
