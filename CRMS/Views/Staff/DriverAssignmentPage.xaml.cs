using CRMS.Data;
using CRMS.Models;
using CRMS.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Views.Staff
{
    public partial class DriverAssignmentPage : Page
    {
        private readonly BookingService _bookingService = new();

        public DriverAssignmentPage()
        {
            InitializeComponent();
            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (!(CurrentUserService.Instance.IsStaff || CurrentUserService.Instance.IsAdmin))
            {
                StatusText.Text = "Chỉ Staff/Admin mới được phân công tài xế.";
                BookingsGrid.ItemsSource = null;
                DriversCombo.ItemsSource = null;
                return;
            }
            try
            {
                var bookings = await _bookingService.GetForStaffAsync();
                BookingsGrid.ItemsSource = bookings
                    .Where(b => b.RentalType == "WithDriver" && b.BookingStatus == "Confirmed")
                    .OrderBy(b => b.StartAt).ToList();

                await using var db = CrmsContextFactory.Create();
                DriversCombo.ItemsSource = await db.Drivers.AsNoTracking()
                    .Where(d => d.IsActive).OrderBy(d => d.FullName).ToListAsync();
                StatusText.Text = $"Có {BookingsGrid.Items.Count} Booking đã xác nhận cần/được phân công.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Không tải được dữ liệu phân công.";
                MessageBox.Show(ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

        private async void Assign_Click(object sender, RoutedEventArgs e)
        {
            if (BookingsGrid.SelectedItem is not BookingListItem booking)
            {
                MessageBox.Show("Vui lòng chọn một Booking trong danh sách.");
                return;
            }
            if (DriversCombo.SelectedValue is not long driverId)
            {
                MessageBox.Show("Vui lòng chọn tài xế đang hoạt động.");
                return;
            }
            var staffId = CurrentUserService.Instance.UserId;
            if (!staffId.HasValue) return;
            try
            {
                await _bookingService.AssignDriverAsync(booking.BookingId, driverId, staffId.Value);
                MessageBox.Show("Phân công tài xế thành công.", "Thông báo",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Không thể phân công",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
