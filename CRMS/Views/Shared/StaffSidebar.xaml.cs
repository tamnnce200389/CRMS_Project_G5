
using System.Windows;
using System.Windows.Controls;

namespace CRMS.Views.Shared
{
    public partial class StaffSidebar : UserControl
    {
        public StaffSidebar()
        {
            InitializeComponent();

            // Mac dinh mo Tong quan
            OverviewGroup.IsExpanded = true;
        }

        // Mo mot nhom, dong cac nhom con lai
        private void Group_Expanded(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Expander selected)
                return;

            Expander[] groups =
            {
                OverviewGroup,
                BookingGroup,
                LookupGroup,
                OperationGroup,
                PaymentGroup,
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
