using CRMS.Services;

namespace CRMS.ViewModels;

public class StaffDashboardViewModel : DashboardViewModel<StaffDashboardSnapshot>
{
    public StaffDashboardViewModel()
        : this(new DashboardService().GetStaffOverviewAsync, CurrentUserService.Instance) { }

    public StaffDashboardViewModel(Func<DateTime, DateTime, Task<StaffDashboardSnapshot>> load, IUserSession session)
        : base(load, session) { }

    protected override bool CanLoad(IUserSession session) => session.Role is "Staff" or "Admin";
    protected override string InitialMessage => "Chọn khoảng thời gian để xem thống kê vận hành.";
    protected override string AccessDeniedMessage => "Chỉ Staff hoặc Admin được xem thống kê vận hành.";
    protected override string LoadingMessage => "Đang tải dữ liệu vận hành từ SQL Server...";
    protected override string LoadedMessage => "Đã cập nhật thống kê vận hành thành công.";
    protected override string ErrorPrefix => "Lỗi tải Staff Dashboard: ";
}
