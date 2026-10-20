using System.Globalization;
using System.Windows.Data;

namespace CRMS.Helpers;

// Translate display labels without changing database values or filter keys.
public sealed class VietnameseLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string ?? "";
        return text.Trim().ToUpperInvariant() switch
        {
            "AVAILABLE" => "Sẵn sàng",
            "RENTED" => "Đang cho thuê",
            "RESERVED" => "Đã đặt",
            "MAINTENANCE" => "Bảo dưỡng",
            "INACTIVE" => "Ngừng hoạt động",
            "ACTIVE" => "Hoạt động",
            "UNAVAILABLE" => "Không khả dụng",
            "RETIRED" => "Ngừng khai thác",
            "AUTOMATIC" or "AUTO" or "AT" => "Số tự động",
            "MANUAL" or "MT" => "Số sàn",
            "CVT" => "Vô cấp (CVT)",
            _ => text
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
