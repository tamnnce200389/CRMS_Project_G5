
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

        // Mui gio Viet Nam (UTC+7)
        private static readonly TimeZoneInfo VietnamTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById(
                "SE Asia Standard Time");

        public Voucher? ResultVoucher { get; private set; }

        // Chuyen UTC sang gio Viet Nam
        private static DateTime ToVietnamTime(DateTime utcTime)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(
                    utcTime,
                    DateTimeKind.Utc),
                VietnamTimeZone);
        }

        // Chuyen gio Viet Nam sang UTC
        private static DateTime ToUtcTime(DateTime vietnamTime)
        {
            return TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(
                    vietnamTime,
                    DateTimeKind.Unspecified),
                VietnamTimeZone);
        }

        public VoucherEditorWindow(Voucher? voucher = null)
        {
            InitializeComponent();

            _originalVoucher = voucher;

            if (voucher == null)
            {
                Title = "Thêm Voucher";
                txtTitle.Text = "Thêm Voucher mới";

                DateTime todayVietnam =
                    ToVietnamTime(DateTime.UtcNow).Date;

                dpStartsAt.SelectedDate = todayVietnam;

                dpEndsAt.SelectedDate =
                    todayVietnam.AddDays(30);

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

                // UTC -> Viet Nam khi hien thi
                dpStartsAt.SelectedDate =
                    ToVietnamTime(voucher.StartsAt).Date;

                dpEndsAt.SelectedDate =
                    ToVietnamTime(voucher.EndsAt).Date;

                txtUsageLimit.Text =
                    voucher.UsageLimit?.ToString() ?? "";

                txtPerCustomerLimit.Text =
                    voucher.PerCustomerLimit.ToString();

                chkIsActive.IsChecked = voucher.IsActive;
            }
        }

        private void Cancel_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Save_Click(
            object sender,
            RoutedEventArgs e)
        {
            string code = txtVoucherCode.Text.Trim();

            string discountType =
                (cboDiscountType.SelectedItem as ComboBoxItem)?
                .Content?.ToString() ?? "Percent";

            // Kiem tra ma Voucher
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

            // Kiem tra gia tri giam
            if (!decimal.TryParse(
                    txtDiscountValue.Text,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal discountValue))
            {
                ShowError(
                    "Giá trị giảm không hợp lệ. " +
                    "Ví dụ: 10 hoặc 100000.");
                return;
            }

            // Kiem tra don hang toi thieu
            if (!decimal.TryParse(
                    txtMinimumOrder.Text,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal minimumOrder))
            {
                ShowError(
                    "Giá trị đơn hàng tối thiểu không hợp lệ.");
                return;
            }

            // Kiem tra muc giam toi da
            decimal? maxDiscount = null;

            if (!string.IsNullOrWhiteSpace(
                    txtMaxDiscount.Text))
            {
                if (!decimal.TryParse(
                        txtMaxDiscount.Text,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out decimal maxValue))
                {
                    ShowError(
                        "Mức giảm tối đa không hợp lệ.");
                    return;
                }

                maxDiscount = maxValue;
            }

            // Kiem tra tong luot su dung
            int? usageLimit = null;

            if (!string.IsNullOrWhiteSpace(
                    txtUsageLimit.Text))
            {
                if (!int.TryParse(
                        txtUsageLimit.Text,
                        out int usage))
                {
                    ShowError(
                        "Tổng lượt sử dụng phải là số nguyên.");
                    return;
                }

                usageLimit = usage;
            }

            // Kiem tra gioi han moi khach
            if (!int.TryParse(
                    txtPerCustomerLimit.Text,
                    out int perCustomerLimit))
            {
                ShowError(
                    "Giới hạn mỗi khách phải là số nguyên.");
                return;
            }

            // Kiem tra ngay
            if (!dpStartsAt.SelectedDate.HasValue ||
                !dpEndsAt.SelectedDate.HasValue)
            {
                ShowError(
                    "Vui lòng chọn ngày bắt đầu " +
                    "và ngày kết thúc.");
                return;
            }

            // Ngay nguoi dung chon la ngay Viet Nam
            DateTime startsAtVietnam =
                dpStartsAt.SelectedDate.Value.Date;

            DateTime endsAtVietnam =
                dpEndsAt.SelectedDate.Value.Date;

            if (_originalVoucher != null)
            {
                // Lay gio Viet Nam cua Voucher cu
                DateTime originalStartVietnam =
                    ToVietnamTime(
                        _originalVoucher.StartsAt);

                DateTime originalEndVietnam =
                    ToVietnamTime(
                        _originalVoucher.EndsAt);

                // Giu nguyen gio, phut, giay Viet Nam
                startsAtVietnam = startsAtVietnam.Add(
                    originalStartVietnam.TimeOfDay);

                endsAtVietnam = endsAtVietnam.Add(
                    originalEndVietnam.TimeOfDay);
            }
            else
            {
                // Voucher moi:
                // Bat dau luc 00:00:00
                // Ket thuc luc 23:59:59
                endsAtVietnam =
                    endsAtVietnam.AddDays(1)
                                 .AddSeconds(-1);
            }

            // Chuyen gio Viet Nam -> UTC de luu SQL
            DateTime startsAt =
                ToUtcTime(startsAtVietnam);

            DateTime endsAt =
                ToUtcTime(endsAtVietnam);

            if (endsAt <= startsAt)
            {
                ShowError(
                    "Ngày kết thúc phải sau ngày bắt đầu.");
                return;
            }

            // Kiem tra gia tri giam
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

            // Tao Voucher voi thoi gian UTC
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

                IsActive =
                    chkIsActive.IsChecked == true,

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
