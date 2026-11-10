namespace CRMS.Services;

public static class BookingStatusRules
{
    // Derive expired holds on read: opening a page must not rewrite a booking or its history.
    public static string EffectiveStatus(string status, DateTime? depositDeadlineUtc, DateTime? nowUtc = null)
        => status == "PendingDeposit" && depositDeadlineUtc.HasValue && depositDeadlineUtc.Value <= (nowUtc ?? DateTime.UtcNow)
            ? "Expired" : status;

    public static string Label(string status) => status switch
    {
        "PendingDeposit" => "Chờ đặt cọc", "Confirmed" => "Đã xác nhận", "InUse" => "Đang thuê",
        "Returned" => "Đã trả xe", "Completed" => "Hoàn tất", "Cancelled" => "Đã hủy",
        "Expired" => "Hết hạn đặt cọc", _ => status
    };
}
