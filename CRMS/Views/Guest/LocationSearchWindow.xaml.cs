
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

        private readonly string _initialCity;

        public LocationSearchWindow(string currentCity = "Cần Thơ")
        {
            _initialCity = currentCity;
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

        // Reset UI to the city that was active when the dialog opened.
        // Applying filters remains a separate, explicit action.
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            CityCombo.SelectedItem = CityCombo.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Content?.ToString() == _initialCity)
                ?? CityCombo.Items.OfType<ComboBoxItem>().FirstOrDefault();

            foreach (WrapPanel panel in new[] { NeedsPanel, TrendsPanel, BudgetPanel })
            {
                foreach (ToggleButton chip in panel.Children.OfType<ToggleButton>())
                    chip.IsChecked = false;
            }
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
