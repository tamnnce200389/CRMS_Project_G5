using CRMS.Models;
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
        private readonly CarSearchItem _car;
        private readonly Dictionary<int, VehicleLongTermRate> _rates = new();
        private int _selectedMonths = 0;

        public int SelectedMonths => _selectedMonths;
        public DateTime? SelectedPickupDate { get; private set; }
        public DateTime? SelectedReturnDate { get; private set; }
        public decimal? SelectedPackageTotal { get; private set; }

        public LongTermDetailWindow(CarSearchItem car)
        {
            InitializeComponent();
            _car = car;
            DataContext = _car;

            CategoryPlanText.Text = $"Loại xe: {_car.CategoryName}";
            PickupDatePicker.DisplayDateStart = DateTime.Today.AddDays(1);
            PickupDatePicker.SelectedDate = DateTime.Today.AddDays(1);

            foreach (Button button in GetPlanButtons())
            {
                button.IsEnabled = false;
            }

            Loaded += LongTermDetailWindow_Loaded;
        }

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

        private static string FormatMoney(decimal amount)
        {
            return amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " ₫";
        }

        private async void LongTermDetailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= LongTermDetailWindow_Loaded;

            try
            {
                StatusText.Text = "Đang tải các gói thuê...";

                var rates = await _rateService.GetRatesByVehicleAsync(_car.VehicleId);
                _rates.Clear();

                foreach (var rate in rates)
                {
                    _rates[(int)rate.DurationMonths] = rate;
                }

                if (_rates.Count == 0)
                {
                    StatusText.Text = "Xe này chưa có bảng giá thuê dài hạn.";
                    return;
                }

                foreach (Button button in GetPlanButtons())
                {
                    if (int.TryParse(button.Tag?.ToString(), out int months))
                    {
                        button.IsEnabled = _rates.ContainsKey(months);
                    }
                }

                int defaultMonths = _rates.ContainsKey(1) ? 1 : _rates.Keys.Min();
                SelectPlan(defaultMonths);

                ChoosePlanButton.IsEnabled = true;
                StatusText.Text = "Giá được lấy từ bảng VehicleLongTermRates.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Không thể tải bảng giá: " + ex.Message;
                MessageBox.Show(ex.Message, "Lỗi tải bảng giá", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Plan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            if (!int.TryParse(button.Tag?.ToString(), out int months)) return;
            SelectPlan(months);
        }

        private void SelectPlan(int months)
        {
            if (!_rates.TryGetValue(months, out VehicleLongTermRate? rate))
                return;

            _selectedMonths = months;

            foreach (Button button in GetPlanButtons())
            {
                bool isSelected = button.Tag?.ToString() == months.ToString();

                button.Background = isSelected
                    ? new SolidColorBrush(Color.FromRgb(220, 252, 231))
                    : Brushes.White;

                button.BorderBrush = isSelected
                    ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                    : new SolidColorBrush(Color.FromRgb(203, 213, 225));

                button.Foreground = isSelected
                    ? new SolidColorBrush(Color.FromRgb(21, 128, 61))
                    : new SolidColorBrush(Color.FromRgb(17, 24, 39));
            }

            CategoryPlanText.Text = $"Loại xe: {_car.CategoryName} — Gói {months} tháng";
            BaseMonthlyText.Text = FormatMoney(rate.BaseMonthlyRate) + "/tháng";
            MonthlyPriceText.Text = FormatMoney(rate.MonthlyRate) + "/tháng";
            DiscountText.Text = rate.DiscountPercent > 0
                ? $"Ưu đãi {rate.DiscountPercent:0}% cho gói {months} tháng"
                : "Gói 1 tháng - giá tiêu chuẩn";

            decimal total = rate.MonthlyRate * months;
            TotalPriceText.Text = FormatMoney(total);
            UpdateReturnDate();
        }

        private void UpdateReturnDate()
        {
            if (_selectedMonths <= 0 || PickupDatePicker.SelectedDate == null)
                return;

            DateTime pickup = PickupDatePicker.SelectedDate.Value.Date;
            DateTime returnDate = pickup.AddMonths(_selectedMonths);
            int days = (int)(returnDate - pickup).TotalDays;
            ReturnDateText.Text = $"{returnDate:dd/MM/yyyy} ({_selectedMonths} tháng · {days} ngày)";
        }

        private void PickupDateChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateReturnDate();
        }

        private void ChooseRental_Click(object sender, RoutedEventArgs e)
        {
            if (!_rates.TryGetValue(_selectedMonths, out VehicleLongTermRate? rate))
            {
                MessageBox.Show("Vui lòng chọn gói thuê hợp lệ.");
                return;
            }

            if (PickupDatePicker.SelectedDate == null || PickupDatePicker.SelectedDate.Value.Date <= DateTime.Today)
            {
                MessageBox.Show("Vui lòng chọn ngày nhận xe từ ngày mai trở đi.");
                return;
            }

            DateTime pickup = PickupDatePicker.SelectedDate.Value.Date;
            DateTime returnDate = pickup.AddMonths(_selectedMonths);
            decimal total = rate.MonthlyRate * _selectedMonths;

            SelectedPickupDate = pickup;
            SelectedReturnDate = returnDate;
            SelectedPackageTotal = total;

            DialogResult = true;
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
