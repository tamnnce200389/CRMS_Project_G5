# TV2 — F06 & F07: Quản lý Phương tiện, Loại xe, Ảnh xe & Kiểm tra Lịch xe trống

## 1. F06 — Quản lý Phương tiện, Loại xe & Bộ ảnh (`feature/tv2-f06-vehicle-crud`)

- **CRUD Xe (`Vehicles`)**:
  - Màn hình Admin `CRMS/Views/Admin/VehiclePage.xaml` + cửa sổ `CRMS/Views/Admin/VehicleEditWindow.xaml`.
  - Hỗ trợ Thêm mới, Chỉnh sửa, Tạm ngưng / Kích hoạt (`OperationalStatus`: `Active` ↔ `Suspended`), tìm kiếm theo từ khóa và lọc theo Loại xe / Trạng thái.
  - Validate đầy đủ các ràng buộc `CRMS.sql`: biển số không trùng (`UX_Vehicles_Plate`), giá thuê `> 0` (tối đa 2 số lẻ), năm sản xuất `1990..2100`, số chỗ `1..30`, hộp số (`Automatic`/`Manual`), nhiên liệu (`Gasoline`/`Diesel`/`Electric`/`Hybrid`), công-tơ-mét không âm và không được giảm khi sửa xe.
  - Chặn chuyển xe sang `Suspended` nếu xe đang có đơn thuê `InUse`, `Confirmed`, hoặc `PendingDeposit` còn hạn giữ chỗ (sử dụng khóa giao dịch `CRMS_VEHICLE_{id}` đồng bộ với `usp_CreateBooking` và `usp_ScheduleMaintenance`).
  - Tự động đồng bộ 5 gói giá thuê dài hạn (`1, 3, 6, 9, 12` tháng = `DailyRate * 30`) trong `dbo.VehicleLongTermRates` ngay trong transaction khi thêm xe mới hoặc cập nhật `DailyRate`.
- **CRUD Loại xe (`VehicleCategories`)**:
  - Cửa sổ `CRMS/Views/Admin/CategoryManagementWindow.xaml`: Thêm, sửa, bật/tắt `IsActive`, chặn trùng tên loại xe, chặn tắt loại xe khi vẫn còn xe `Active`, và chặn kích hoạt xe thuộc loại đang bị tắt.
- **Quản lý Bộ ảnh xe (`VehicleImages`)**:
  - Hỗ trợ chọn tệp ảnh từ máy tính (tự sao chép vào thư mục `VehicleUploads/` dưới dạng `file:///...`) và nhập URL `http(s)://`.
  - Xem trước ảnh, đổi thứ tự `SortOrder`, xóa ảnh, và đảm bảo mỗi xe có đúng 1 ảnh đại diện (`IsPrimary = 1`) khi có ảnh.

---

## 2. F07 — Kiểm tra Lịch xe trống `[StartAt, EndAt)` & Nguyên nhân không khả dụng (`feature/tv2-f07-availability`)

- **Quy tắc khoảng nửa mở `[StartAt, EndAt)` (`AvailabilityRules`)**:
  - Chuyển đổi tự động giữa giờ Việt Nam (`SE Asia Standard Time`, `UTC+7`) trên WPF và `UTC` lưu trong SQL Server (nếu đầu vào đã mang `DateTimeKind.Utc` thì giữ nguyên UTC).
  - Giao khoảng nửa mở `existingStartUtc < queryEndUtc && existingEndUtc > queryStartUtc` → đơn 1 đặt `[10:00, 12:00)`, đơn 2 bắt đầu đúng `12:00` (`[12:00, 14:00)`) **hợp lệ, không báo trùng**.
- **Đầy đủ 5 nhóm nguyên nhân không khả dụng (`AvailabilityConflictType`)**:
  1. `VehicleSuspended`: Xe đang ở trạng thái `Suspended`.
  2. `CategoryInactive`: Loại xe đang ngừng hoạt động (`IsActive = 0`).
  3. `BookingInUse`: Xe đang trong chuyến thuê chưa hoàn trả (`StartAt < queryEndUtc`, bao gồm cả trường hợp khách trả xe trễ quá giờ `EndAt` dự kiến).
  4. `BookingConfirmed`: Trùng khoảng với đơn thuê đã xác nhận cọc (`Confirmed`).
  5. `BookingPendingDeposit`: Trùng khoảng với đơn đang giữ chỗ chờ đặt cọc còn hạn (`DepositDeadline > UtcNow`; đơn hết hạn giữ cọc tự động bỏ qua).
  6. `Maintenance`: Trùng khoảng với lịch bảo trì `Scheduled` hoặc `InProgress` (`Completed` / `Cancelled` tự động giải phóng xe).
- **Bảo mật theo vai trò**:
  - Khi Guest / Customer gọi `CheckAvailabilityAsync`, danh sách xung đột trả khung giờ bận theo giờ Việt Nam và **ẩn `BookingCode` nội bộ**.
  - Khi Staff / Admin đang đăng nhập (xác thực qua `db.Users`), `AvailabilityConflict.ReferenceCode` trả kèm `BookingCode` hoặc `BT-#MaintenanceId` để tra cứu vận hành.
- **Màn hình `AvailabilityPage.xaml` (cho cả Admin & Staff)**:
  - Mở từ `AdminSidebar` (*Quản lý phương tiện → Kiểm tra lịch trống*), `StaffSidebar` (*Tra cứu → Kiểm tra lịch trống*), hoặc bấm nút tắt **"Kiểm tra lịch trống"** trên `Admin/VehiclePage.xaml` để nhảy thẳng sang xe đang chọn.

---

## 3. Hợp đồng tích hợp cho `TV1` & `TV6`

### Hợp đồng `IVehicleService` (`F06`)

```csharp
using CRMS.Services.Vehicles;

IVehicleService vehicles = new VehicleService();

// Danh sách xe có lọc theo từ khóa, CategoryId, OperationalStatus, khoảng DailyRate
var list = await vehicles.GetVehiclesAsync(new VehicleFilter(
    Keyword: "Toyota",
    CategoryId: null,
    OperationalStatus: "Active",
    OnlyActiveCategories: true));

// Chi tiết đầy đủ 1 xe kèm toàn bộ danh sách ảnh đã sắp theo SortOrder
var detail = await vehicles.GetVehicleDetailsAsync(vehicleId);
```

### Hợp đồng `IAvailabilityService` (`F07`)

> `CRMS.Services.VehicleAvailabilityService` giữ nguyên 100% chữ ký cũ (`GetAvailableVehicleIdsAsync`, `IsAvailableAsync`) nên `CarSearchViewModel` của `TV1` không cần chỉnh sửa, đồng thời cài đặt `IAvailabilityService` cho `TV1` và `TV6`.

```csharp
using CRMS.Services;
using CRMS.Services.Vehicles;

IAvailabilityService availability = new VehicleAvailabilityService();

// 1. Lấy tập VehicleId còn trống trong khoảng nửa mở [startLocal, endLocal)
HashSet<long> freeIds = await availability.GetAvailableVehicleIdsAsync(startLocal, endLocal);

// 2. Kiểm tra chi tiết 1 xe kèm lý do không khả dụng (cho TV1 CarDetails / TV6 Booking)
VehicleAvailabilityResult check = await availability.CheckAvailabilityAsync(
    vehicleId,
    startLocal,
    endLocal,
    requireFutureStart: true);

if (!check.IsAvailable)
{
    // Chuỗi tóm tắt gọn cho hộp thoại cảnh báo:
    string message = check.SummaryReason;

    // Hoặc duyệt từng xung đột chi tiết:
    foreach (AvailabilityConflict c in check.Conflicts)
    {
        // c.Type, c.Title, c.Reason, c.StartAtLocal, c.EndAtLocal, c.ReferenceCode
    }
}
```

---

## 4. Chạy kiểm thử tự động

Từ thư mục gốc chứa `CRMS.slnx`:

```powershell
# 1. Kiểm thử logic offline & MVVM F06 + F07 (không kết nối hoặc thay đổi database)
dotnet run --project tests/CRMS.VehicleChecks/CRMS.VehicleChecks.csproj

# 2. Kiểm thử tích hợp SQL Server F06 + F07 trên DB tạm độc lập (tự tạo và tự xóa sạch sau khi chạy)
dotnet run --project tests/CRMS.VehicleChecks/CRMS.VehicleChecks.csproj -- --integration "$PWD/CRMS.sql"

# 3. Kiểm thử render 5 giao diện WPF F06 + F07 & xác nhận 0 lỗi DataBinding
dotnet run --project tests/CRMS.VehicleChecks/CRMS.VehicleChecks.csproj -- --render "$PWD/artifacts_preview"
```
