using System.Windows;
using System.Windows.Controls;
using CRMS.Models;
using CRMS.Services;
using CRMS.ViewModels;
using CRMS.Views;
namespace CRMS.Views.Guest;

public partial class HomePage : Page
{
    private HomeViewModel ViewModel => (HomeViewModel)DataContext;
    public HomePage() : this(new SearchService()) { }
    public HomePage(SearchService service)
    {
        InitializeComponent(); DataContext = new HomeViewModel(service);
        ViewModel.SearchRequested += request => { if (IsLoaded && Window.GetWindow(this)?.DataContext is MainViewModel main) main.OpenSearch(request); };
        Loaded += (_, _) => ViewModel.LoadCitiesCommand.Execute(null);
    }
    private void Login_Click(
        object sender, RoutedEventArgs e)
    {
        Window? mainWindow = Window.GetWindow(this);

        var loginWindow = new LoginWindow();

        if (mainWindow != null)
            loginWindow.Owner = mainWindow;

        loginWindow.ShowDialog();
    }

    // =====================================
    // 2. DANG KY
    // =====================================
    private void Register_Click(
        object sender, RoutedEventArgs e)
    {
        Window? mainWindow = Window.GetWindow(this);

        var registerWindow = new RegisterWindow();

        if (mainWindow != null)
            registerWindow.Owner = mainWindow;

        if (registerWindow.ShowDialog() == true)
        {
            MessageBox.Show(
                "Đăng ký thành công! Vui lòng đăng nhập.",
                "Thông báo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    // =====================================
    // 3. DANG XUAT
    // =====================================
    private void Logout_Click(
        object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this)?.DataContext is MainViewModel main) main.LogoutCommand.Execute(null);

    }

    // =====================================
    // 4. HO SO CA NHAN
    // =====================================
    private void Profile_Click(
        object sender, RoutedEventArgs e)
    {
        if (!CurrentUserService.Instance.IsLoggedIn)
        {
            MessageBox.Show(
                "Vui lòng đăng nhập để xem hồ sơ cá nhân.",
                "Thông báo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Window? mainWindow = Window.GetWindow(this);

        if (mainWindow?.DataContext is MainViewModel vm)
        {
            vm.NavigateCommand.Execute("Profile");
        }
        else
        {
            MessageBox.Show(
                "Không thể mở trang hồ sơ lúc này.",
                "Thông báo",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    // =====================================
    // 5. VE CHUNG TOI
    // =====================================
    private void About_Click(
        object sender, RoutedEventArgs e)
    {
        if (Content is ScrollViewer { Content: UIElement content } scroll)
            scroll.ScrollToVerticalOffset(AboutSection.TranslatePoint(new Point(), content).Y);
    }


    private void Destination_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SeasonalDestination destination }) { ViewModel.SelectedDestination = destination; UpdateLayout(); DestinationPlan.BringIntoView(); }
    }
    private void PlanPickup_Click(object sender, RoutedEventArgs e)
    {
        SearchPanel.BringIntoView(); if (ViewModel.IsWithDriver) { DriverPickupCombo.Focus(); DriverPickupCombo.IsDropDownOpen = true; }
        else if (ViewModel.IsLongTerm) ViewModel.SearchCommand.Execute("LongTerm"); else SelfDriveLocation_Click(sender, e);
    }
    private void SelfDriveLocation_Click(object sender, RoutedEventArgs e)
    {
        var vm = ViewModel; var dialog = new LocationSearchWindow(vm.SelectedCityName, vm.SelectedTrends, vm.SelectedCityId, vm.SelectedNeeds, vm.SelectedBudgets) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) vm.SetLocation(dialog.SelectedCity, dialog.SelectedCityId, dialog.SelectedNeeds, dialog.SelectedTrends, dialog.SelectedBudgets);
    }
    private void SelfDriveTime_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RentalTimeWindow(false, ViewModel.HourlyOnly) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) ViewModel.SetPeriod(false, dialog.StartAt, dialog.EndAt, dialog.RentalUnit == "Hour");
    }
    private void DriverTime_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RentalTimeWindow(true) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) ViewModel.SetPeriod(true, dialog.StartAt, dialog.EndAt);
    }
}
