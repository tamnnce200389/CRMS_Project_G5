using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace CRMS.Helpers;

// WPF rendering belongs to the presentation layer; the payment service builds the payload.
public static class QrImageRenderer
{
    public static BitmapSource Create(string payload)
    {
        using var data = QRCodeGenerator.GenerateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        using var stream = new MemoryStream(qr.GetGraphic(8));
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
