using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CRMS.Dtos;
using CRMS.Helpers;
using CRMS.Models;
using CRMS.Services;
using CRMS.ViewModels;
using CRMS.Views.Guest;
using CRMS.Views.Customer;
using CRMS.Views.Admin;

internal static class Program
{
    internal static int Passed;
    internal static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); Passed++; }
    internal static async Task Reject(Func<Task> work, string name)
    { try { await work(); } catch (InvalidOperationException) { Check(true, name); return; } catch (ArgumentException) { Check(true, name); return; } throw new Exception("FAIL accepted: " + name); }
    [STAThread]
    private static int Main(string[] args)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        Exception? failure = null;
        dispatcher.BeginInvoke(new Action(async () => { try { await Run(); if (args.Length == 2 && args[0] == "--integration") await DatabaseChecks.Run(args[1]); if (args.Length == 2 && args[0] == "--render") Render(args[1]); Console.WriteLine($"{Passed} MVVM checks passed."); } catch (Exception ex) { failure = ex; Console.Error.WriteLine(ex); } finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); } }));
        Dispatcher.Run(); return failure == null ? 0 : 1;
    }
    private static Task Execute(ICommand command, object? parameter = null) => ((AsyncRelayCommand)command).ExecuteAsync(parameter);
    internal static UserAccountDto Account(long id, string role = "Customer", string name = "Khách hàng") => new(id, name, $"u{id}@test.invalid", "0900000000", role, true, DateTime.UtcNow, [1]);
    private static async Task Run()
    {
        var blocked = new TaskCompletionSource(); int count = 0;
        var command = new AsyncRelayCommand(async _ => { count++; await blocked.Task; });
        var first = command.ExecuteAsync(); await command.ExecuteAsync(); Check(count == 1 && command.IsRunning, "Repeated async clicks execute once"); blocked.SetResult(); await first; Check(command.CanExecute(null), "Async command re-enables after completion");
        var failing = new AsyncRelayCommand(_ => throw new InvalidOperationException("expected")); await Reject(() => failing.ExecuteAsync(), "Async failure remains observable"); Check(!failing.IsRunning, "Async failure does not leave command locked");

        var session = new TestSession(1, "Customer"); var accounts = new AccountFake { Profile = Account(1) };
        var profile = new ProfileViewModel(accounts, session); await profile.LoadAsync(); Check(profile.UserId == 1 && profile.Email == "u1@test.invalid", "Profile loads through service");
        profile.FullName = "Đã sửa"; await profile.SaveAsync(); Check(accounts.LastRequest?.RowVersion.SequenceEqual(new byte[] { 1 }) == true && profile.FullName == "Đã sửa", "Profile save preserves original row version");
        var pending = new TaskCompletionSource<UserAccountDto>(); accounts.Pending = pending.Task;
        var oldLoad = profile.LoadAsync(); session.Change(2, "Customer"); pending.SetResult(Account(1)); await oldLoad;
        Check(profile.UserId == null && profile.FullName == "" && !profile.IsBusy, "Changing account discards stale profile completion");
        accounts.Pending = null; accounts.Profile = Account(2); await profile.LoadAsync(); Check(profile.UserId == 2, "New account can reload profile after cancellation");
        var tripsFake = new TripsFake(); var trips = new MyBookingsViewModel(tripsFake, session); var tripsLoad = trips.LoadAsync(); session.Change(3, "Customer"); tripsFake.Result.SetResult([new("OLD", "Xe cũ", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), 100, "Confirmed", "SelfDrive", "Day", 1)]); await tripsLoad; Check(trips.Bookings.Count == 0, "Old customer's bookings cannot reappear after session change");

        session.Change(10, "Admin"); accounts.Users = [Account(10, "Admin"), Account(20)]; var confirmation = new DialogFake(); var users = new UserManagementViewModel(accounts, session, confirmation); await users.LoadAsync();
        users.SelectedUser = accounts.Users[0]; Check(!users.ToggleActiveCommand.CanExecute(null), "Admin cannot disable own account");
        users.SelectedUser = accounts.Users[1]; users.SelectedRole = "Staff"; await Execute(users.ChangeRoleCommand); Check(accounts.Writes == 0, "Cancelled confirmation prevents role update");
        confirmation.Answer = true; await Execute(users.ChangeRoleCommand); Check(accounts.Writes == 1, "Confirmed role update uses account service");
        users.SearchText = "u20@"; Check(users.Users.Cast<UserAccountDto>().Count() == 1, "Account search stays in ViewModel");
        session.Change(21, "Staff"); Check(users.Users.Cast<object>().Count() == 0 && !users.CanModify, "Session change clears admin account list");

        var search = new SearchFake(); var home = new HomeViewModel(search); await home.LoadCitiesAsync(); home.DriverPickup = home.PickupCities.Single(c => c.CityId == 1); home.DriverDestination = home.DestinationCities.Single(c => c.CityId == 2);
        foreach (var route in new[] { "InterCityRoundTrip", "InterCityOneWay" })
        { home.DriverRoute = route; var ctx = home.BuildSearchContext("WithDriver"); Check(ctx.PickupCityId == 1 && ctx.DestinationCityId == 2 && ctx.RouteType == route, "Destination retained for " + route); }
        await home.ReloadCitiesAsync(); Check(home.DriverDestination?.CityId == 2, "Refreshing city list preserves destination by ID");
        home.DriverDestination = home.DriverPickup; await Reject(() => Task.FromResult(home.BuildSearchContext("WithDriver")), "Reject same city for intercity");
        home.DriverDestination = null; await Execute(home.SearchCommand, "WithDriver"); Check(home.SearchMessage.Contains("điểm đến"), "Missing destination reports a useful message");
        home.DriverRoute = "InnerCity"; Check(home.BuildSearchContext("WithDriver").DestinationCityId == 1, "Inner city uses pickup as destination");
        home.SetLocation("Cần Thơ", 1, ["ChildSeat", "Family"], ["Electric"], ["Economy"]);
        Check(home.BuildSearchContext("LongTerm").PickupCityId == 1 && !home.BuildSearchContext("WithDriver").ChildSeatOnly, "Long-term Can Tho and driver filters remain isolated");
        var self = home.BuildSearchContext("SelfDrive"); Check(self.ChildSeatOnly && self.Fuel == "Electric" && self.PurposeCodes.SequenceEqual(new[] { "Family" }), "Self-drive picker maps choices to search contract");
        SearchNavigationRequest? request = null; home.SearchRequested += r => request = r; await Execute(home.SearchCommand, "SelfDrive"); Check(request?.Context.ChildSeatOnly == true, "Search command emits navigation request without creating a Page");

        var cityByName = new LocationFilterViewModel(null,null,null,null,search,"Cần Thơ");await Execute(cityByName.LoadCommand);Check(cityByName.SelectedCity?.CityId==1,"Location picker retains named city before city ID finishes loading");
        var location = new LocationFilterViewModel(1, null, null, null, search); await Execute(location.LoadCommand);
        location.Trends[0].IsSelected = true; location.Trends[1].IsSelected = true; location.Trends[2].IsSelected = true; Check(!location.Trends[0].IsSelected && location.Trends[1].IsSelected && location.Trends[2].IsSelected, "Hybrid replaces electric while sport remains independent");
        location.Budgets[0].IsSelected = true; location.Budgets[1].IsSelected = true; Check(location.Budgets.Count(c => c.IsSelected) == 1, "Location budget choices are exclusive");
        var applied = false; location.Applied += () => applied = true; location.ResetCommand.Execute(null); Check(!applied && location.SelectedCity?.CityId == 1 && !location.Trends.Any(c => c.IsSelected), "Reset clears filters without applying or changing initial city"); location.ApplyCommand.Execute(null); Check(applied, "Location Apply emits one UI result");

        var time = new RentalTimeViewModel(); Check(time.TryBuildRange(out var dayStart, out var dayEnd, out _) && dayEnd - dayStart == TimeSpan.FromDays(1), "Day picker defaults to one valid day");
        time.ReturnDate = time.PickupDate?.AddDays(3); Check(!time.TryBuildRange(out _, out _, out _), "Picker retains existing 48-hour day limit");
        time.SelectedTab = 1; time.DurationHours = 8; Check(time.TryBuildRange(out var hourStart, out var hourEnd, out _) && hourEnd - hourStart == TimeSpan.FromHours(8), "Hour picker calculates selected duration");
        var driverTime = new RentalTimeViewModel(true); driverTime.SelectedTab = 0; Check(driverTime.RentalUnit == "Hour", "Driver picker stays hourly");

        session.Change(1, "Customer"); var car = new CarSearchItem { VehicleId = 1, Brand = "Toyota", Model = "Vios", CanUseLegacyBooking = true, QuoteUnit = "Hour", EstimatedTripTotal = 400000 };
        var longTerm = new LongTermDetailViewModel(car, DateTime.Today.AddDays(2).AddHours(9), 3, new RatesFake(), session); await longTerm.LoadAsync(); BookingSelection? selection = null; longTerm.CheckoutRequested += s => selection = s; longTerm.ChooseCommand.Execute(null);
        Check(selection?.PriceUnit == "Month" && selection.DurationMonths == 3 && selection.ExpectedTotal == 27000000 && selection.EndAt == selection.StartAt.AddMonths(3), "Monthly checkout uses selected term and exact total");
        longTerm.SelectPlanCommand.Execute("9"); Check(longTerm.SelectedMonths == 3, "Unavailable monthly plans cannot be selected");
        longTerm.PickupDate = DateTime.Today.AddDays(-1); selection = null; longTerm.ChooseCommand.Execute(null); Check(selection == null, "Monthly checkout rejects past pickup");
        var unavailable = new LongTermDetailViewModel(car, null, 9, new RatesFake(), session); await unavailable.LoadAsync(); Check(!unavailable.ChooseCommand.CanExecute(null), "Unavailable initial plan cannot checkout");

        var results = new CarSearchViewModel(search); results.SetRentalPeriod(DateTime.Today.AddDays(2), DateTime.Today.AddDays(3)); await results.SearchAsync(); var loaded = results.Cars.Single();
        var booking = results.PrepareBooking(loaded, session); Check(booking.PriceUnit == "Hour" && booking.ExpectedTotal == 400000, "Search hands original hourly quote to checkout");
        results.DiscountOnly = true; await Reject(() => Task.FromResult(results.PrepareBooking(loaded, session)), "Unapplied filter prevents stale result checkout");
        results.DiscountOnly = false; await results.SearchAsync(); session.Change(5, "Staff"); await Reject(() => Task.FromResult(results.PrepareBooking(results.Cars.Single(), session)), "Staff cannot use customer checkout");
        var start = results.StartAtLocal; var end = results.EndAtLocal; results.HourlyOnly = true; results.AdjustHourlyPeriod(); Check(results.EndAtLocal - results.StartAtLocal == TimeSpan.FromHours(4), "Hourly quick filter adjusts period in ViewModel"); results.HourlyOnly = false; results.AdjustHourlyPeriod(); Check(results.StartAtLocal == start && results.EndAtLocal == end, "Turning hourly off restores prior period");

        CurrentUserService.Instance.SetUser(new() { UserId = 10, FullName = "Admin", UserRole = "Admin" });
        var files = new FileFake(); var reportVm = new RevenueReportViewModel(files); var report = new RevenueReportResult { FromDateVN = DateTime.Today, ToDateVN = DateTime.Today, Rows = [new() { BookingCode = "MVVM", VehicleName = "Xe mẫu", BookingTotal = 1000000, DepositPaid = 500000 }] };
        typeof(RevenueReportViewModel).GetProperty(nameof(RevenueReportViewModel.Report))!.SetValue(reportVm, report);
        await Execute(reportVm.ExportCsvCommand); Check(files.Content == null, "Cancelling save dialog prevents report write");
        files.Path = "report.csv"; await Execute(reportVm.ExportCsvCommand); Check(files.Content?.Contains("MVVM") == true, "CSV report is exported through file abstraction");
        files.Content = null; files.OnChoose = () => CurrentUserService.Instance.Logout(); await Execute(reportVm.ExportCsvCommand); Check(files.Content == null, "Session change during save dialog prevents report export");
        CurrentUserService.Instance.Logout(); var nav = new NavigationFake(); var main = new MainViewModel(nav); Check(main.IsGuest && nav.CurrentRoute == "Home", "Main navigation starts guest on Home");
        CurrentUserService.Instance.SetUser(new() { UserId = 10, FullName = "Admin", UserRole = "Admin" }); Check(nav.Resets == 2 && nav.CurrentRoute == "Dashboard" && main.IsAdmin, "Login clears cached pages and selects role home"); main.LogoutCommand.Execute(null); Check(main.IsGuest && nav.Resets == 3, "Logout resets navigation and session");
    }
    private static void Render(string output)
    {
        Directory.CreateDirectory(output);
        ApplicationServices.NavigationFactory = () => new PageNavigationService();
        ApplicationServices.Confirmation = new WpfConfirmationService();
        var source = System.Xml.Linq.XDocument.Load(Path.Combine("CRMS", "App.xaml"));
        System.Xml.Linq.XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var resources = new System.Xml.Linq.XElement(ns + "ResourceDictionary",
            new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "helpers", "clr-namespace:CRMS.Helpers;assembly=CRMS"),
            source.Root!.Element(ns + "Application.Resources")!.Elements());
        var app = new Application { Resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(resources.ToString().Replace("clr-namespace:CRMS.Helpers\"", "clr-namespace:CRMS.Helpers;assembly=CRMS\"")) };
        using var bindingLog = new StringWriter(); var listener = new System.Diagnostics.TextWriterTraceListener(bindingLog);
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        var session = new TestSession(1, "Customer"); var account = new AccountFake { Profile = Account(1) };
        var profile = new ProfilePage { DataContext = new ProfileViewModel(account, session) };
        var users = new UserManagementPage { DataContext = new UserManagementViewModel(account, new TestSession(10, "Admin"), new DialogFake()) };
        var windows = new Window[] { new RentalTimeWindow(), new RentalTimeWindow(true), new LocationSearchWindow(), new LongTermDetailWindow(new() { VehicleId = 1, Brand = "Toyota", Model = "Vios" }) };
        var location = (LocationSearchWindow)windows[2]; location.DataContext = new LocationFilterViewModel(1, null, null, null, new SearchFake()); Execute(((LocationFilterViewModel)location.DataContext).LoadCommand).GetAwaiter().GetResult();
        var monthly = (LongTermDetailWindow)windows[3]; var monthlyVm = new LongTermDetailViewModel(new() { VehicleId = 1, Brand = "Toyota", Model = "Vios" }, null, 3, new RatesFake(), session); monthly.DataContext = monthlyVm; monthlyVm.LoadAsync().GetAwaiter().GetResult();
        foreach (var root in new FrameworkElement[] { profile, users, new HomePage(new SearchFake()) }.Concat(windows.Select(w => (FrameworkElement)w.Content)))
        {
            Window? host = null; if (Window.GetWindow(root) == null) host = new Window { Content = root, DataContext = new MainViewModel(new NavigationFake()) };
            root.Measure(new Size(1100, 850)); root.Arrange(new Rect(0, 0, 1100, 850)); root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1100, 850, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, root.GetType().Name + "-" + Guid.NewGuid().ToString("N")[..5] + ".png")); encoder.Save(file); Check(true, "Render " + root.GetType().Name);
        }
        var picker = (RentalTimeWindow)windows[0]; var pickerVm = (RentalTimeViewModel)picker.DataContext; var duration = (ComboBox)picker.FindName("DurationCombo");
        Check(duration.SelectedValue?.ToString() == "4" && pickerVm.DurationHours == 4, "Rendered hour duration retains default selection"); duration.SelectedIndex = 4; Check(pickerVm.DurationHours == 8, "Rendered duration selector writes to ViewModel");
        var pickup = (Calendar)picker.FindName("PickupCalendar"); pickup.SelectedDate = DateTime.Today.AddDays(5); Check(pickerVm.PickupDate == pickup.SelectedDate, "Rendered calendar writes to ViewModel");
        Check(((ComboBox)location.FindName("CityCombo")).SelectedItem is SearchCity { CityId: 1 }, "Rendered location displays selected city object");
        Check(((TextBlock)monthly.FindName("TotalPriceText")).Text == monthlyVm.TotalLabel, "Rendered monthly total reads ViewModel");
        listener.Flush(); File.WriteAllText(Path.Combine(output, "bindings.log"), bindingLog.ToString());
        Check(!bindingLog.ToString().Contains("Error:"), "Rendered refactored screens have no binding errors");
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
    }
}
internal sealed class TestSession(long id, string role) : ObservableObject, IUserSession
{
    public long? UserId { get; private set; } = id; public string Role { get; private set; } = role; public long SessionVersion { get; private set; }
    public void Change(long id, string role) { UserId = id; Role = role; SessionVersion++; OnPropertyChanged(nameof(SessionVersion)); }
}
internal sealed class DialogFake : IConfirmationService { public bool Answer; public bool Confirm(string message, string title) => Answer; }
internal sealed class FileFake : IFileExportService
{
    public string? Path, Content; public Action? OnChoose;
    public string? ChooseCsvPath(string name) { OnChoose?.Invoke(); return Path; }
    public Task WriteCsvAsync(string path, string content) { Content = content; return Task.CompletedTask; }
}
internal sealed class AccountFake : IUserAccountService
{
    public UserAccountDto Profile = Program.Account(1); public Task<UserAccountDto>? Pending; public List<UserAccountDto> Users = []; public ProfileUpdateRequest? LastRequest; public int Writes;
    public Task<UserAccountDto> GetProfileAsync(CancellationToken token = default) => Pending ?? Task.FromResult(Profile);
    public Task<UserAccountDto> SaveProfileAsync(ProfileUpdateRequest request, CancellationToken token = default) { LastRequest = request; Profile = Profile with { FullName = request.FullName, Phone = request.Phone }; return Task.FromResult(Profile); }
    public Task<List<UserAccountDto>> GetUsersAsync(CancellationToken token = default) => Task.FromResult(Users);
    public Task ChangeRoleAsync(UserAccountDto target, string role, CancellationToken token = default) { Writes++; return Task.CompletedTask; }
    public Task SetActiveAsync(UserAccountDto target, bool active, CancellationToken token = default) { Writes++; return Task.CompletedTask; }
}
internal sealed class TripsFake : IBookingQueryService
{
    public TaskCompletionSource<List<CustomerTrip>> Result = new(); public Task<List<CustomerTrip>> GetMyBookingsAsync(CancellationToken token = default) => Result.Task;
    public Task<long> ResolveBookingAsync(string lookup, CancellationToken token = default) => Task.FromResult(1L);
}
internal sealed class SearchFake : SearchService
{
    public override Task<List<SearchCity>> GetCitiesAsync() => Task.FromResult(new List<SearchCity> { new() { CityId = 1, CityName = "Cần Thơ" }, new() { CityId = 2, CityName = "Đà Nẵng" } });
    public override Task<List<CarSearchItem>> SearchAsync(CarSearchFilter filter, CancellationToken token = default) => Task.FromResult(new List<CarSearchItem> { new() { VehicleId = 1, Brand = "Toyota", Model = "Vios", CanUseLegacyBooking = true, QuoteUnit = "Hour", EstimatedTripTotal = 400000 } });
}
internal sealed class RatesFake : ILongTermRateService
{
    public Task<List<VehicleLongTermRate>> GetRatesByVehicleAsync(long id) => Task.FromResult(new List<VehicleLongTermRate> { new() { VehicleId = id, DurationMonths = 3, BaseMonthlyRate = 10000000, MonthlyRate = 9000000, DiscountPercent = 10, IsActive = true } });
}
internal sealed class NavigationFake : INavigationService
{
    public object? Current { get; set; }
    public string CurrentRoute { get; private set; } = ""; public event Action? Changed; public int Resets;
    public void Reset() { Resets++; Current = null; CurrentRoute = ""; Changed?.Invoke(); }
    public void Navigate(string route, object? argument = null) { CurrentRoute = route; Changed?.Invoke(); }
}
