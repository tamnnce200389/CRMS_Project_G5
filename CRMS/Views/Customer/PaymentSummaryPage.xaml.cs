using CRMS.ViewModels;
using System.Windows.Controls;
namespace CRMS.Views.Customer;
public partial class PaymentSummaryPage : Page
{
    public PaymentSummaryPage()
    {
        InitializeComponent();
        DataContext = new PaymentWorkspaceViewModel("Summary");
    }
}
