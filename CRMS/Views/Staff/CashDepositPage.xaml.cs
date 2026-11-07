using System.Windows.Controls;

namespace CRMS.Views.Staff
{
    public partial class CashDepositPage : Page
    {
        public CashDepositPage()
        {
            InitializeComponent();
            CashQueueFrame.Content = new BookingQueuePage();
        }
    }
}
