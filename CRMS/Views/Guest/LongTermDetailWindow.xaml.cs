
using CRMS.Models;
using CRMS.Views.Customer;
using CRMS.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CRMS.Views.Guest
{
    public partial class LongTermDetailWindow : Window
    {
        private readonly LongTermRateService _rateService = new();

        // Xe duoc khach chon
        private readonly CarSearchItem _car;

        // Bang gia cac goi thue cua xe
        private readonly Dictionary<int, VehicleLongTermRate>
            _rates = new();

        // So thang dang chon
        private int _selectedMonths = 0;

        private readonly int _initialMonths;
        private readonly TimeSpan _pickupTime;

        public LongTermDetailWindow(CarSearchItem car, DateTime? initialStart = null, int initialMonths = 1)
        {
            InitializeComponent();

            _car = car;
            _initialMonths = initialMonths;
            _pickupTime = initialStart?.TimeOfDay ?? TimeSpan.FromHours(9);

            // Hien thi thong tin xe tren XAML
            DataContext = _car;

            // Mac dinh nhan xe sau 7 ngay
            PickupDatePicker.DisplayDateStart = DateTime.Today;

            PickupDatePicker.SelectedDate =
                initialStart ?? DateTime.Today.AddDays(7);

            // Khoa cac goi khi chua tai bang gia
            foreach (Button button in GetPlanButtons())
            {
                button.IsEnabled = false;
            }

            Loaded += LongTermDetailWindow_Loaded;
        }

        // =====================================
        // 1. CAC NUT CHON GOI THUE
        // =====================================
        private Button[] GetPlanButtons()
        {
            return new[]
            {
                Month1Button,
                Month3Button,
                Month6Button,
                Month9Button,
                Month12Button
            };
        }

        // =====================================
        // 2. DINH DANG TIEN VIET NAM
        // =====================================
        private static string FormatMoney(decimal amount)
        {
            return amount.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN")) + " ₫";
        }

        // =====================================
        // 3. LAY BANG GIA TU SQL SERVER
        // =====================================
        private async void LongTermDetailWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            // Chi load mot lan
            Loaded -= LongTermDetailWindow_Loaded;

            try
            {
                StatusText.Text = "Đang tải các gói thuê...";

                var rates =
                    await _rateService.GetRatesByVehicleAsync(
                        _car.VehicleId);

                _rates.Clear();

                foreach (var rate in rates)
                {
                    _rates[(int)rate.DurationMonths] = rate;
                }

                if (_rates.Count == 0)
                {
                    StatusText.Text =
                        "Xe này chưa có bảng giá thuê dài hạn.";

                    return;
                }

                // Nam san xuat cua xe
                var firstRate = rates.First();

                YearText.Text =
                    firstRate.Vehicle?.ManufactureYear.ToString()
                    ?? "Chưa cập nhật";

                // Chi bat cac goi co trong SQL
                foreach (Button button in GetPlanButtons())
                {
                    if (int.TryParse(
                        button.Tag?.ToString(),
                        out int months))
                    {
                        button.IsEnabled =
                            _rates.ContainsKey(months);
                    }
                }

                // Mac dinh chon goi 1 thang
                int defaultMonths = _initialMonths;
                if (!_rates.ContainsKey(defaultMonths))
                {
                    StatusText.Text = "Gói thuê đã chọn không còn khả dụng. Vui lòng tìm kiếm lại.";
                    return;
                }

                SelectPlan(defaultMonths);

                ChoosePlanButton.IsEnabled = true;

                StatusText.Text =
                    "Giá được lấy từ bảng VehicleLongTermRates.";
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Không thể tải bảng giá: " + ex.Message;

                MessageBox.Show(
                    ex.Message,
                    "Lỗi tải bảng giá",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =====================================
        // 4. KHI KHACH BAM CHON GOI
        // =====================================
        private void Plan_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (!int.TryParse(
                button.Tag?.ToString(),
                out int months))
            {
                return;
            }

            SelectPlan(months);
        }

        // =====================================
        // 5. CAP NHAT GIA KHI CHON GOI
        // =====================================
        private void SelectPlan(int months)
        {
            if (!_rates.TryGetValue(
                months,
                out VehicleLongTermRate? rate))
            {
                return;
            }

            _selectedMonths = months;

            // To mau goi dang chon
            foreach (Button button in GetPlanButtons())
            {
                bool isSelected =
                    button.Tag?.ToString() ==
                    months.ToString();

                // Theme chung: khong khai bao mau trong code-behind.
                button.Background = (System.Windows.Media.Brush)FindResource(
                    isSelected ? "PrimarySoft" : "Surface");

                button.BorderBrush = (System.Windows.Media.Brush)FindResource(
                    isSelected ? "Primary" : "Line");

                button.Foreground = (System.Windows.Media.Brush)FindResource(
                    isSelected ? "PrimaryPressed" : "Ink");
            }

            // Gia goc chua giam
            BaseMonthlyText.Text =
                FormatMoney(rate.BaseMonthlyRate)
                + "/tháng";

            // Gia thang da giam tu SQL
            MonthlyPriceText.Text =
                FormatMoney(rate.MonthlyRate)
                + "/tháng";

            // Phan tram giam gia
            DiscountText.Text =
                rate.DiscountPercent > 0
                ? $"Ưu đãi {rate.DiscountPercent:0}% " +
                  $"cho gói {months} tháng"
                : "Gói 1 tháng - giá tiêu chuẩn";

            // Tong tien ca hop dong
            decimal total =
                rate.MonthlyRate * months;

            TotalPriceText.Text =
                FormatMoney(total);

            // Cap nhat ngay tra xe du kien
            UpdateReturnDate();
        }

        // =====================================
        // 6. CAP NHAT NGAY TRA XE
        // =====================================
        private void UpdateReturnDate()
        {
            if (_selectedMonths <= 0 ||
                PickupDatePicker.SelectedDate == null)
            {
                return;
            }

            DateTime pickup =
                PickupDatePicker.SelectedDate.Value;

            DateTime returnDate =
                pickup.AddMonths(_selectedMonths);

            ReturnDateText.Text =
                returnDate.ToString("dd/MM/yyyy");
        }

        // =====================================
        // 7. KHI DOI NGAY NHAN XE
        // =====================================
        private void PickupDateChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            UpdateReturnDate();
        }

        // =====================================
        // 8. CHON THUE
        // =====================================
        private void ChooseRental_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_rates.TryGetValue(
                _selectedMonths,
                out VehicleLongTermRate? rate))
            {
                MessageBox.Show(
                    "Vui lòng chọn gói thuê hợp lệ.");
                return;
            }

            if (PickupDatePicker.SelectedDate == null ||
                PickupDatePicker.SelectedDate.Value.Date
                    < DateTime.Today)
            {
                MessageBox.Show(
                    "Vui lòng chọn ngày nhận xe hợp lệ.");
                return;
            }

            DateTime pickup =
                PickupDatePicker.SelectedDate.Value;

            DateTime returnDate =
                pickup.AddMonths(_selectedMonths);

            decimal total =
                rate.MonthlyRate * _selectedMonths;

            var session = CurrentUserService.Instance;
            if (!session.IsCustomer || !session.UserId.HasValue)
            {
                MessageBox.Show("Vui lòng đăng nhập tài khoản Customer để đặt xe.");
                return;
            }
            pickup = pickup.Date.Add(_pickupTime);
            returnDate = pickup.AddMonths(_selectedMonths);
            if (pickup <= DateTime.Now) { MessageBox.Show("Thời gian nhận xe phải ở tương lai."); return; }
            var selection = new BookingSelection(session.UserId.Value, _car.VehicleId, "LongTerm",
                pickup, returnDate, "Month", _selectedMonths, total);
            var checkout = new BookingCheckoutWindow(selection, $"{_car.Brand} {_car.Model}") { Owner = this };
            if (checkout.ShowDialog() == true) DialogResult = true;
        }

        // =====================================
        // 9. DONG CUA SO
        // =====================================
        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }
    }
}
