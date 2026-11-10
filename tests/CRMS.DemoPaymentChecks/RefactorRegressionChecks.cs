using System.ComponentModel;
using CRMS.Dtos;
using CRMS.Services;
using CRMS.ViewModels;

internal static class RefactorRegressionChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        await Dashboard<DashboardSnapshot>((load, session) => new AdminDashboardViewModel(load, session), "Admin", check);
        await Dashboard<StaffDashboardSnapshot>((load, session) => new StaffDashboardViewModel(load, session), "Staff", check);
        var start = new DateTime(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);
        var data = new CustomerTrip("REF-TRIP", "Toyota Vios", start, start.AddDays(1), 700000, "Confirmed", IsDemo: true);
        var vm = new TripItemViewModel(data);
        check(vm.Trip == data && vm.StartLocal.Hour == 22 && vm.EndLocal.Date == new DateTime(2026, 11, 11), "Trip presentation preserves source data and Vietnam dates");
        check(vm.StatusLabel == "Đã xác nhận" && vm.BillingLabel == "DEMO · Thuê theo ngày", "Trip presentation preserves confirmed and daily labels");
        check(new TripItemViewModel(data with { PriceUnit = "Hour", RentalUnits = 4 }).BillingLabel == "DEMO · Thuê theo giờ · 4 giờ", "Hourly trip label is unchanged");
        check(new TripItemViewModel(data with { PriceUnit = "Month", RentalUnits = 3 }).BillingLabel == "DEMO · Thuê dài hạn · 3 tháng", "Monthly trip label is unchanged");
        check(new TripItemViewModel(data with { IsDemo = false, RentalType = "WithDriver" }).BillingLabel == "Thuê có tài xế", "Driver trip label is unchanged");
        await TripsAsync(data, check);
    }

    private static async Task Dashboard<T>(Func<Func<DateTime, DateTime, Task<T>>, IUserSession, DashboardViewModel<T>> create, string role, Action<bool, string> check) where T : class, new()
    {
        var session = new Session { UserId = 1, Role = role };
        int calls = 0;
        DateTime queriedFrom = default, queriedTo = default;
        Func<DateTime, DateTime, Task<T>> query = (from, to) => { calls++; queriedFrom = from; queriedTo = to; return Task.FromResult(new T()); };
        var vm = create((from, to) => query(from, to), session);
        var today = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time").Date;
        check(vm.FromDate == today.AddDays(-29) && vm.ToDate == today && !vm.HasData && !vm.IsBusy, role + " dashboard retains initial Vietnam date range and empty state");
        session.Role = "Customer";
        await vm.LoadAsync();
        check(calls == 0 && !vm.HasData && !vm.IsBusy && vm.StatusMessage.StartsWith("Chỉ "), role + " dashboard preserves role gate before querying");
        session.Role = role;
        vm.FromDate = null;
        await vm.LoadAsync();
        check(calls == 0 && vm.StatusMessage == "Vui lòng chọn đầy đủ khoảng thời gian.", role + " dashboard preserves missing-date validation");
        vm.FromDate = today.AddDays(1);
        await vm.LoadAsync();
        check(calls == 0 && vm.StatusMessage == "Ngày bắt đầu không được sau ngày kết thúc.", role + " dashboard preserves reversed-date validation");
        vm.FromDate = today.AddDays(-2).AddHours(14);
        vm.ToDate = today.AddHours(20);
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        await vm.LoadAsync();
        check(vm.HasData && !vm.IsBusy && queriedFrom == today.AddDays(-2) && queriedTo == today && notifications.Contains("Snapshot") && notifications.Contains("HasData"), role + " dashboard publishes results and binding notifications using date-only filters");
        vm.FromDate = today.AddDays(-3);
        check(!vm.HasData && vm.StatusMessage.StartsWith("Khoảng ngày đã thay đổi"), role + " dashboard clears a displayed result after date edits");
        var pending = new TaskCompletionSource<T>();
        query = (_, _) => pending.Task;
        var loading = vm.LoadAsync();
        check(vm.IsBusy && !vm.HasData && !vm.RefreshCommand.CanExecute(null), role + " dashboard keeps loading state and disables duplicate command");
        await vm.LoadAsync();
        vm.ToDate = today.AddDays(1);
        pending.SetResult(new T());
        await loading;
        check(!vm.HasData && !vm.IsBusy && vm.StatusMessage.StartsWith("Khoảng ngày đã thay đổi"), role + " dashboard discards a successful response for an edited filter");
        pending = new TaskCompletionSource<T>();
        query = (_, _) => pending.Task;
        loading = vm.LoadAsync();
        session.UserId = 2;
        pending.SetResult(new T());
        await loading;
        check(!vm.HasData && !vm.IsBusy && vm.StatusMessage == "Phiên đăng nhập đã thay đổi. Hãy tải lại.", role + " dashboard discards success after account changes");
        query = (_, _) => Task.FromException<T>(new InvalidOperationException("Fixture disconnected"));
        await vm.LoadAsync();
        check(!vm.HasData && !vm.IsBusy && vm.RefreshCommand.CanExecute(null) && vm.StatusMessage.EndsWith("Fixture disconnected"), role + " dashboard preserves error and retry states");
        // Diagnostic only: this user/role-only guard also existed before refactoring.
        // Do not silently change its behavior as part of the extraction.
        pending = new TaskCompletionSource<T>();
        query = (_, _) => pending.Task;
        loading = vm.LoadAsync();
        session.RestartSameAccount();
        pending.SetResult(new T());
        await loading;
        Console.WriteLine($"AUDIT: {role} dashboard accepts an old response after only SessionVersion changes: {vm.HasData}. Existing behavior; not fixed in this refactor.");
    }

    private static async Task TripsAsync(CustomerTrip data, Action<bool, string> check)
    {
        var session = new Session { UserId = 1, Role = "Customer" };
        var source = new Trips();
        var vm = new MyBookingsViewModel(source, session);
        await vm.LoadAsync();
        check(vm.ShowState && !vm.ShowRetry && vm.StateTitle == "Bạn chưa có chuyến thuê nào", "Trips preserve no-data state after presentation extraction");
        source.Rows = [data];
        await vm.LoadAsync();
        check(!vm.ShowState && vm.Bookings.Single().Trip == data, "Trips keep the query result intact in the row view model");
        source.Fail = true;
        await vm.LoadAsync();
        check(vm.ShowState && vm.ShowRetry && vm.Bookings.Count == 0, "Trips preserve load-error and retry states");
        source.Fail = false;
        source.Pending = new TaskCompletionSource<List<CustomerTrip>>();
        var loading = vm.LoadAsync();
        session.Switch();
        source.Pending.SetResult([data]);
        await loading;
        check(vm.Bookings.Count == 0 && !vm.IsBusy, "Old trip response cannot populate a new login session");
    }

    private sealed class Session : IUserSession, INotifyPropertyChanged
    {
        public long? UserId { get; set; }
        public string Role { get; set; } = "Customer";
        public long SessionVersion { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Switch() { UserId++; SessionVersion++; PropertyChanged?.Invoke(this, new(nameof(SessionVersion))); }
        public void RestartSameAccount() { SessionVersion++; PropertyChanged?.Invoke(this, new(nameof(SessionVersion))); }
    }
    private sealed class Trips : IBookingQueryService
    {
        public List<CustomerTrip> Rows = [];
        public bool Fail;
        public TaskCompletionSource<List<CustomerTrip>>? Pending;
        public Task<List<CustomerTrip>> GetMyBookingsAsync(CancellationToken token = default) => Fail
            ? Task.FromException<List<CustomerTrip>>(new InvalidOperationException("Fixture disconnected"))
            : Pending?.Task ?? Task.FromResult(Rows);
        public Task<long> ResolveBookingAsync(string lookup, CancellationToken token = default) => throw new NotSupportedException();
    }
}
