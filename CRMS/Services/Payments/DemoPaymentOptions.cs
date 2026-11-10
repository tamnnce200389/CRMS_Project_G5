using System.Configuration;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CRMS.Services.Payments;

public sealed record DemoPaymentOptions
{
    public const decimal TransferAmount = 1000m;
    public bool Enabled { get; init; }
    public string BankBin { get; init; } = "";
    public string BankName { get; init; } = "";
    public string AccountNumber { get; init; } = "";
    public string AccountName { get; init; } = "";

    public static DemoPaymentOptions Load()
    {
        bool enabled = bool.TryParse(ConfigurationManager.AppSettings["DemoPaymentsEnabled"], out var flag) && flag;
        var paths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "payment-demo.local.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "payment-demo.local.json"))
        };
        var path = paths.FirstOrDefault(File.Exists);
        if (path == null) return new() { Enabled = enabled };
        try
        {
            var receiver = JsonSerializer.Deserialize<DemoPaymentOptions>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new DemoPaymentOptions();
            return receiver with { Enabled = enabled };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Keep the UI usable; creation validates the receiver before reserving a car.
            return new() { Enabled = enabled };
        }
    }

    public void RequireEnabled()
    {
        if (!Enabled) throw new InvalidOperationException("Chế độ thanh toán demo đang tắt.");
    }

    public void ValidateReceiver()
    {
        RequireEnabled();
        if (!Regex.IsMatch(BankBin, @"\A[0-9]{6}\z") || !Regex.IsMatch(AccountNumber, @"\A[A-Za-z0-9]{1,34}\z") ||
            string.IsNullOrWhiteSpace(AccountName) || AccountName.Length > 120 || string.IsNullOrWhiteSpace(BankName) || BankName.Length > 80)
            throw new InvalidOperationException("Tài khoản nhận chuyển khoản demo chưa được thiết lập đầy đủ. Vui lòng liên hệ nhân viên.");
    }
}
