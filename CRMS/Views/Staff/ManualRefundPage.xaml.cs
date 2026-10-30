using CRMS.ViewModels;
using System.Windows.Controls;
namespace CRMS.Views.Staff;
public partial class ManualRefundPage : Page
{
    public ManualRefundPage()
    {
        InitializeComponent();
        DataContext = new PaymentWorkspaceViewModel("Refund");
    }
}
