
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace CRMS.Views.Guest
{
    public partial class RentalTimeWindow : Window
    {
        private readonly bool _withDriver;

        public DateTime StartAt { get; private set; }
        public DateTime EndAt { get; private set; }

        public string RentalUnit =>
            _withDriver || RentalTabs.SelectedItem == HourTab
                ? "Hour"
                : "Day";

        public RentalTimeWindow(bool withDriver = false)
        {
            InitializeComponent();

            _withDriver = withDriver;

            // Tao moc gio cach nhau 30 phut
            for (int i = 0; i < 48; i++)
            {
                string time = TimeSpan
                    .FromMinutes(i * 30)
                    .ToString(@"hh\:mm");

                DayPickupTime.Items.Add(time);
                DayReturnTime.Items.Add(time);
                HourlyPickupTime.Items.Add(time);
            }


            DateTime next = DateTime.Now.AddHours(1);

            DateTime defaultPickup = new DateTime(
                next.Year,
                next.Month,
                next.Day,
                next.Hour,
                0,
                0);

            PickupCalendar.DisplayDateStart = DateTime.Today;
            ReturnCalendar.DisplayDateStart = DateTime.Today;

            PickupCalendar.SelectedDate = defaultPickup.Date;
            ReturnCalendar.SelectedDate = defaultPickup.Date.AddDays(1);

            PickupCalendar.DisplayDate = defaultPickup.Date;
            ReturnCalendar.DisplayDate = defaultPickup.Date.AddDays(1);

            HourlyDate.DisplayDateStart = DateTime.Today;
            HourlyDate.SelectedDate = defaultPickup.Date;

            string defaultTime = defaultPickup.ToString("HH:mm");

            DayPickupTime.SelectedItem = defaultTime;
            DayReturnTime.SelectedItem = defaultTime;
            HourlyPickupTime.SelectedItem = defaultTime;

            // Mac dinh 4 gio
            DurationCombo.SelectedIndex = 2;


            if (withDriver)
            {
                // Xe co tai xe chi dung che do theo gio
                DayTab.Visibility = Visibility.Collapsed;
                RentalTabs.SelectedItem = HourTab;

                Title = "Thời gian - Xe có tài xế";
            }
            else
            {
                RentalTabs.SelectedItem = DayTab;
            }

            UpdateSummary();
        }

        private void Close_Click(
            object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void InputChanged(
            object sender, RoutedEventArgs e)
        {
            if (SummaryText != null)
                UpdateSummary();
        }

        private bool TryBuildRange(
            out DateTime start,
            out DateTime end,
            out string error)
        {
            start = default;
            end = default;
            error = "";

            bool dayMode =
                !_withDriver &&
                RentalTabs.SelectedItem == DayTab;

            if (dayMode)
            {
                if (!PickupCalendar.SelectedDate.HasValue ||
                    !ReturnCalendar.SelectedDate.HasValue)
                {
                    error = "Hãy chọn ngày nhận và ngày trả xe.";
                    return false;
                }

                if (!TryGetTime(DayPickupTime, out var pickup) ||
                    !TryGetTime(DayReturnTime, out var dropoff))
                {
                    error = "Hãy chọn giờ nhận và trả xe.";
                    return false;
                }

                start = PickupCalendar.SelectedDate.Value.Date
                    .Add(pickup);

                end = ReturnCalendar.SelectedDate.Value.Date
                    .Add(dropoff);
            }
            else
            {
                if (!HourlyDate.SelectedDate.HasValue ||
                    !TryGetTime(HourlyPickupTime, out var pickup))
                {
                    error = "Hãy chọn ngày và giờ nhận xe.";
                    return false;
                }

                if (DurationCombo.SelectedItem
                    is not ComboBoxItem selected ||
                    !int.TryParse(
                        selected.Tag?.ToString(),
                        out int hours))
                {
                    error = "Hãy chọn thời gian thuê.";
                    return false;
                }

                start = HourlyDate.SelectedDate.Value.Date
                    .Add(pickup);

                end = start.AddHours(hours);
            }

            if (start <= DateTime.Now)
            {
                error = "Thời gian nhận xe phải ở tương lai.";
                return false;
            }

            if (end <= start)
            {
                error = "Thời gian trả phải sau thời gian nhận.";
                return false;
            }

            if (dayMode &&
                end - start > TimeSpan.FromHours(48))
            {
                error = "Thuê ngày chỉ được tối đa 48 giờ.";
                return false;
            }

            return true;
        }

        private static bool TryGetTime(
            ComboBox combo, out TimeSpan value)
        {
            return TimeSpan.TryParseExact(
                combo.SelectedItem as string,
                @"hh\:mm",
                CultureInfo.InvariantCulture,
                out value);
        }

        private void UpdateSummary()
        {
            if (SummaryText == null)
                return;

            if (TryBuildRange(
                out DateTime start,
                out DateTime end,
                out string error))
            {
                double hours = (end - start).TotalHours;

                SummaryText.Text =
                    $"{start:HH:mm dd/MM/yyyy} - " +
                    $"{end:HH:mm dd/MM/yyyy}\n" +
                    $"Thời gian thuê: {hours:0.#} giờ";
            }
            else
            {
                SummaryText.Text = error;
            }
        }

        private void Confirm_Click(
            object sender, RoutedEventArgs e)
        {
            if (!TryBuildRange(
                out DateTime start,
                out DateTime end,
                out string error))
            {
                MessageBox.Show(
                    error,
                    "Thời gian không hợp lệ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            StartAt = start;
            EndAt = end;

            DialogResult = true;
        }
    }
}
