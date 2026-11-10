using System.Windows;
using CRMS.Services;
namespace CRMS.Helpers;

public sealed class WpfConfirmationService : IConfirmationService
{
    public bool Confirm(string message, string title)
    {
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        return (owner == null
            ? MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel)
            : MessageBox.Show(owner, message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel)) == MessageBoxResult.Yes;
    }
}
