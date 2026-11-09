namespace CRMS.Services;
// Composition defaults are installed by App; tests can supply their own dependencies.
public static class ApplicationServices
{
    private sealed class NoConfirmation : IConfirmationService { public bool Confirm(string message, string title) => false; }
    public static IConfirmationService Confirmation { get; set; } = new NoConfirmation();
    public static Func<INavigationService>? NavigationFactory { get; set; }
    public static IFileExportService? FileExport { get; set; }
}
