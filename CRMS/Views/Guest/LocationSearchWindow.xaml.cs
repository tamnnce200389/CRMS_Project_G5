
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CRMS.Views.Guest
{
    public partial class LocationSearchWindow : Window
    {
        public string SelectedCity { get; private set; } = "Cần Thơ";

        public List<string> SelectedNeeds { get; private set; } = new();

        public List<string> SelectedTrends { get; private set; } = new();

        public List<string> SelectedBudgets { get; private set; } = new();

        public LocationSearchWindow(string currentCity = "Cần Thơ")
        {
            InitializeComponent();

            foreach (ComboBoxItem item in CityCombo.Items)
            {
                if (item.Content?.ToString() == currentCity)
                {
                    CityCombo.SelectedItem = item;
                    break;
                }
            }
        }

        private static List<string> GetSelectedFilters(WrapPanel panel)
        {
            return panel.Children
                .OfType<ToggleButton>()
                .Where(button => button.IsChecked == true)
                .Select(button => button.Tag?.ToString() ?? "")
                .Where(value => value.Length > 0)
                .ToList();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            SelectedCity =
                (CityCombo.SelectedItem as ComboBoxItem)
                    ?.Content?.ToString() ?? "Cần Thơ";

            SelectedNeeds = GetSelectedFilters(NeedsPanel);
            SelectedTrends = GetSelectedFilters(TrendsPanel);
            SelectedBudgets = GetSelectedFilters(BudgetPanel);

            DialogResult = true;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
