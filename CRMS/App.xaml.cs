using System.Windows;
using CRMS.Helpers;
using CRMS.Services;
namespace CRMS;

public partial class App : Application
{
    public App()
    {
        ApplicationServices.Confirmation = new WpfConfirmationService();
        ApplicationServices.FileExport = new WpfFileExportService();
        ApplicationServices.NavigationFactory = () => new PageNavigationService();
        AsyncRelayCommand.ReportError = ex => MessageBox.Show("Không thể hoàn tất thao tác. Vui lòng thử lại.\n" + ex.Message, "CRMS", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
