using CRMS.Services.Payments;
namespace CRMS.Services;
// Composition defaults are installed by App; tests can supply their own dependencies.
public static class ApplicationServices
{
    public static Func<IPaymentService> CreatePaymentService { get; set; } = () => new PaymentService();
    public static Func<ICashDepositService> CreateCashDepositService { get; set; } = () => new CashDepositService();
    public static Func<IDemoPaymentService> CreateDemoPaymentService { get; set; } = () => new DemoPaymentService();
    public static Func<IBookingQueryService> CreateBookingQueryService { get; set; } = () => new BookingQueryService();
    private sealed class NoConfirmation : IConfirmationService { public bool Confirm(string message, string title) => false; }
    public static IConfirmationService Confirmation { get; set; } = new NoConfirmation();
    public static Func<INavigationService>? NavigationFactory { get; set; }
    public static IFileExportService? FileExport { get; set; }
}
