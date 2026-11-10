using CRMS.Services;

namespace CRMS.ViewModels;

public class AdminDashboardViewModel : DashboardViewModel<DashboardSnapshot>
{
    public AdminDashboardViewModel()
        : this(new DashboardService().GetOverviewAsync, CurrentUserService.Instance) { }

    public AdminDashboardViewModel(Func<DateTime, DateTime, Task<DashboardSnapshot>> load, IUserSession session)
        : base(load, session) { }

    protected override bool CanLoad(IUserSession session) => session.Role == "Admin";
    protected override string InitialMessage => "Chọn khoảng ngày để xem thống kê hệ thống.";
    protected override string AccessDeniedMessage => "Chỉ Admin mới được xem Dashboard quản trị.";
    protected override string LoadingMessage => "Đang tải dữ liệu quản trị từ SQL Server...";
    protected override string LoadedMessage => "Đã cập nhật Admin Dashboard thành công.";
    protected override string ErrorPrefix => "Lỗi tải Admin Dashboard: ";
}
