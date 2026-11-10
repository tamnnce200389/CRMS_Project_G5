using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using QRCoder;

namespace CRMS.Services.Payments;

public static class VietQrCode
{
    public static string CreatePayload(string bankBin, string accountNumber, string transferContent)
    {
        if (!Regex.IsMatch(bankBin, @"\A[0-9]{6}\z") || !Regex.IsMatch(accountNumber, @"\A[A-Za-z0-9]{1,34}\z") ||
            !Regex.IsMatch(transferContent, @"\A[A-Z0-9 ]{1,25}\z"))
            throw new ArgumentException("Thông tin tài khoản hoặc nội dung QR demo không hợp lệ.");
        var beneficiary = Field("00", bankBin) + Field("01", accountNumber);
        var merchant = Field("00", "A000000727") + Field("01", beneficiary) + Field("02", "QRIBFTTA");
        var payload = Field("00", "01") + Field("01", "12") + Field("38", merchant) + Field("53", "704")
            + Field("54", DemoPaymentOptions.TransferAmount.ToString("0", CultureInfo.InvariantCulture))
            + Field("58", "VN") + Field("62", Field("08", transferContent)) + "6304";
        return payload + Crc16(payload);
    }

    private static string Field(string tag, string value)
    {
        var length = Encoding.UTF8.GetByteCount(value);
        if (length > 99) throw new ArgumentException("Dữ liệu QR quá dài.");
        return tag + length.ToString("00", CultureInfo.InvariantCulture) + value;
    }

    public static string Crc16(string text)
    {
        ushort crc = 0xffff;
        foreach (byte value in Encoding.UTF8.GetBytes(text))
        {
            crc ^= (ushort)(value << 8);
            for (int bit = 0; bit < 8; bit++)
                crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
        }
        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }

    public static BitmapSource CreateImage(string payload)
    {
        using var data = QRCodeGenerator.GenerateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        using var stream = new MemoryStream(qr.GetGraphic(8));
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
