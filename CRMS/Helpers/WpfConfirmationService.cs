using System.Windows;
using CRMS.Services;
namespace CRMS.Helpers;

public sealed class WpfConfirmationService : IConfirmationService
{
    public bool Confirm(string message, string title) => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
}
