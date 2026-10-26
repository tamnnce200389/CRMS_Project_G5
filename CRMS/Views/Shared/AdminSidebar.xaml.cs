
using System.Windows;
using System.Windows.Controls;

namespace CRMS.Views.Shared
{
    public partial class AdminSidebar : UserControl
    {
        public AdminSidebar()
        {
            InitializeComponent();

            // Mac dinh mo nhom Tong quan
            OverviewGroup.IsExpanded = true;
        }

        // Chi cho mo mot nhom tai mot thoi diem
        private void Group_Expanded(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Expander selected)
                return;

            Expander[] groups =
            {
                OverviewGroup,
                VehicleGroup,
                BookingGroup,
                DriverGroup,
                SystemGroup,
                AccountGroup
            };

            foreach (Expander group in groups)
            {
                if (group != null && group != selected)
                {
                    group.IsExpanded = false;
                }
            }
        }
    }
}
