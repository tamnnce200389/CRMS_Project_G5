using CRMS.ViewModels;
using System.Windows.Controls;
namespace CRMS.Views.Staff;
public partial class OutstandingBalancePage : Page
{
    public OutstandingBalancePage()
    {
        InitializeComponent();
        DataContext = new PaymentWorkspaceViewModel("Balance");
    }
}
