using System.Windows;
using CRMS.ViewModels;
namespace CRMS.Views.Guest;

public partial class LocationSearchWindow : Window
{
    private LocationFilterViewModel ViewModel => (LocationFilterViewModel)DataContext;
    public string SelectedCity => ViewModel.SelectedCity?.CityName ?? "Tất cả địa điểm";
    public int? SelectedCityId => ViewModel.SelectedCity?.CityId > 0 ? ViewModel.SelectedCity.CityId : null;
    public List<string> SelectedNeeds => ViewModel.Needs.Where(c => c.IsSelected).Select(c => c.Code).ToList();
    public List<string> SelectedTrends => ViewModel.Trends.Where(c => c.IsSelected).Select(c => c.Code).ToList();
    public List<string> SelectedBudgets => ViewModel.Budgets.Where(c => c.IsSelected).Select(c => c.Code).ToList();
    public LocationSearchWindow(string currentCity = "", List<string>? currentTrends = null, int? currentCityId = null, List<string>? currentNeeds = null, List<string>? currentBudgets = null)
    {
        InitializeComponent(); DataContext = new LocationFilterViewModel(currentCityId, currentNeeds, currentTrends, currentBudgets, currentCityName: currentCity);
        ViewModel.Applied += () => DialogResult = true; Loaded += (_, _) => ViewModel.LoadCommand.Execute(null);
    }
    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
