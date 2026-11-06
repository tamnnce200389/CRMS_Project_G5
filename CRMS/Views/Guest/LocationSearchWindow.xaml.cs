using CRMS.Models;
using CRMS.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CRMS.Views.Guest;

public partial class LocationSearchWindow : Window
{
    public string SelectedCity { get; private set; } = "Tất cả địa điểm";
    public int? SelectedCityId { get; private set; }
    public List<string> SelectedNeeds { get; private set; } = new();
    public List<string> SelectedTrends { get; private set; } = new();
    public List<string> SelectedBudgets { get; private set; } = new();
    private readonly int? _initialCityId;
    public LocationSearchWindow(string currentCity = "", List<string>? currentTrends = null,
        int? currentCityId = null, List<string>? currentNeeds = null, List<string>? currentBudgets = null)
    {
        InitializeComponent();
        _initialCityId = currentCityId;
        Restore(NeedsPanel, currentNeeds); Restore(TrendsPanel, currentTrends); Restore(BudgetPanel, currentBudgets);
        Loaded += async (_, _) =>
        {
            try
            {
                ApplyButton.IsEnabled = false;
                var cities = await new SearchService().GetCitiesAsync();
                cities.Insert(0, new SearchCity { CityId = 0, CityName = "Tất cả địa điểm" });
                CityCombo.ItemsSource = cities;
                CityCombo.SelectedItem = cities.FirstOrDefault(c => c.CityId == currentCityId) ?? cities[0];
                LocationStatus.Text = cities.Count == 1 ? "Chưa có địa điểm thực tế được cấu hình trong database thử." : "Các điều kiện sẽ được áp dụng khi tìm xe.";
                ApplyButton.IsEnabled = true;
            }
            catch (Exception ex) { LocationStatus.Text = "Không thể tải địa điểm: " + ex.Message; }
        };
    }
    private static void Restore(WrapPanel panel, List<string>? codes)
    {
        foreach (var chip in panel.Children.OfType<ToggleButton>()) chip.IsChecked = codes?.Contains(chip.Tag?.ToString() ?? "") == true;
    }
    private static List<string> GetSelectedFilters(WrapPanel panel) => panel.Children.OfType<ToggleButton>()
        .Where(b => b.IsChecked == true).Select(b => b.Tag?.ToString() ?? "").Where(s => s.Length > 0).ToList();
    private void FuelTrend_Checked(object sender, RoutedEventArgs e)
    {
        if (TrendsPanel == null || sender is not ToggleButton selected || selected.Tag?.ToString() == "Sport") return;
        foreach (var chip in TrendsPanel.Children.OfType<ToggleButton>())
            if (chip != selected && chip.Tag?.ToString() != "Sport") chip.IsChecked = false;
    }
    private void Budget_Checked(object sender, RoutedEventArgs e)
    {
        if (BudgetPanel == null || sender is not ToggleButton selected) return;
        foreach (var chip in BudgetPanel.Children.OfType<ToggleButton>()) if (chip != selected) chip.IsChecked = false;
    }
    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        CityCombo.SelectedItem = CityCombo.Items.OfType<SearchCity>().FirstOrDefault(c => c.CityId == _initialCityId) ?? CityCombo.Items.OfType<SearchCity>().FirstOrDefault();
        Restore(NeedsPanel, null); Restore(TrendsPanel, null); Restore(BudgetPanel, null);
    }
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var city = CityCombo.SelectedItem as SearchCity;
        SelectedCityId = city?.CityId > 0 ? city.CityId : null;
        SelectedCity = city?.CityName ?? "Tất cả địa điểm";
        SelectedNeeds = GetSelectedFilters(NeedsPanel); SelectedTrends = GetSelectedFilters(TrendsPanel); SelectedBudgets = GetSelectedFilters(BudgetPanel);
        DialogResult = true;
    }
    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
