using System.IO;
using System.Text.Json;
using CRMS.Services.Payments;

internal static class ReceiverConfigurationChecks
{
    public static void Run(Action<bool, string> check)
    {
        var folder = Path.Combine(Path.GetTempPath(), "CRMS_QR_Config_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var shared = Path.Combine(folder, "payment-demo.json");
        var oldLocal = Path.Combine(folder, "payment-demo.local.json");
        var receiver = new DemoPaymentOptions { BankBin = "970436", BankName = "TEST BANK", AccountNumber = "0000000001", AccountName = "TEST RECEIVER" };
        try
        {
            File.WriteAllText(shared, JsonSerializer.Serialize(receiver));
            var loaded = DemoPaymentOptions.Load(folder, true);
            loaded.ValidateReceiver();
            check(loaded.Enabled && loaded.BankBin == receiver.BankBin && loaded.AccountNumber == receiver.AccountNumber,
                "A fresh checkout loads the shared receiving configuration without a private local file");
            File.WriteAllText(oldLocal, JsonSerializer.Serialize(receiver with { AccountNumber = "9999999999" }));
            check(DemoPaymentOptions.Load(folder, true).AccountNumber == receiver.AccountNumber,
                "An old local receiver cannot override the group's shared QR receiving account");
            check(!DemoPaymentOptions.Load(folder, false).Enabled,
                "Shared receiver settings cannot turn on a disabled demo mode");
            File.WriteAllText(shared, "{invalid JSON");
            check(DemoPaymentOptions.Load(folder, true).AccountNumber.Length == 0,
                "Malformed shared configuration fails closed instead of using the old local account");
            File.Delete(shared);
            check(DemoPaymentOptions.Load(folder, true).AccountNumber.Length == 0,
                "Missing shared configuration never silently uses a leftover local receiving account");
        }
        finally
        {
            if (File.Exists(shared)) File.Delete(shared);
            File.Delete(oldLocal);
            Directory.Delete(folder);
        }
        var bundled = DemoPaymentOptions.Load(AppContext.BaseDirectory, true);
        bundled.ValidateReceiver();
        check(File.Exists(Path.Combine(AppContext.BaseDirectory, "payment-demo.json")) && bundled.AccountNumber.Length > 0,
            "Build output includes a valid shared receiver for teammates who only pull and build");
        var payload = VietQrCode.CreatePayload(bundled.BankBin, bundled.AccountNumber, "DEMOSHAREDCONFIG");
        check(payload.Contains("54041000") && payload.Contains(bundled.AccountNumber),
            "The bundled receiver produces a QR for exactly 1,000 VND with the configured receiving account");
    }
}
