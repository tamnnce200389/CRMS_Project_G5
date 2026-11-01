# TV2 — F06, F07 & F24: Quản lý Phương tiện, Lịch trống & Bảo trì xe

## 1. F06 — Quản lý Phương tiện, Loại xe & Bộ ảnh (`feature/tv2-f06-vehicle-crud`)

- **CRUD Xe (`Vehicles`)**:
  - Màn hình Admin `CRMS/Views/Admin/VehiclePage.xaml` + cửa sổ `CRMS/Views/Admin/VehicleEditWindow.xaml`.
  - Hỗ trợ Thêm mới, Chỉnh sửa, Tạm ngưng / Kích hoạt (`OperationalStatus`: `Active` ↔ `Suspended`), tìm kiếm theo từ khóa và lọc theo Loại xe / Trạng thái.
  - Validate đầy đủ các ràng buộc `CRMS.sql`: biển số không trùng (`UX_Vehicles_Plate`), giá thuê `> 0` (tối đa 2 số lẻ), năm sản xuất `1990..2100`, số chỗ `1..30`, hộp số (`Automatic`/`Manual`), nhiên liệu (`Gasoline`/`Diesel`/`Electric`/`Hybrid`), công-tơ-mét không âm và không được giảm khi sửa xe.
  - Chặn chuyển xe sang `Suspended` nếu xe đang có đơn thuê `InUse`, `Confirmed`, hoặc `PendingDeposit` còn hạn giữ chỗ (khóa giao dịch `CRMS_VEHICLE_{id}`).
  - Tự động đồng bộ 5 gói giá thuê dài hạn (`1, 3, 6, 9, 12` tháng = `DailyRate * 30`) trong `dbo.VehicleLongTermRates`.
- **CRUD Loại xe (`VehicleCategories`)**:
  - Cửa sổ `CRMS/Views/Admin/CategoryManagementWindow.xaml`: Thêm, sửa, bật/tắt `IsActive`, chặn trùng tên loại xe, chặn tắt loại xe khi vẫn còn xe `Active`, và chặn kích hoạt xe thuộc loại đang bị tắt.
- **Quản lý Bộ ảnh xe (`VehicleImages`)**:
  - Hỗ trợ chọn tệp ảnh từ máy tính (tự sao chép vào thư mục `VehicleUploads/` dưới dạng `file:///...`) và nhập URL `http(s)://`, đảm bảo đúng 1 ảnh đại diện (`IsPrimary = 1`).

---

## 2. F07 — Kiểm tra Lịch xe trống `[StartAt, EndAt)` & Nguyên nhân không khả dụng (`feature/tv2-f07-availability`)

- **Quy tắc khoảng nửa mở `[StartAt, EndAt)` (`AvailabilityRules`)**:
  - Quy đổi tự động giữa giờ Việt Nam (`SE Asia Standard Time`, `UTC+7`) trên WPF và `UTC` lưu trong SQL Server.
  - Giao khoảng nửa mở `existingStartUtc < queryEndUtc && existingEndUtc > queryStartUtc` → đơn 1 đặt `[10:00, 12:00)`, đơn 2 bắt đầu đúng `12:00` (`[12:00, 14:00)`) **hợp lệ, không báo trùng**.
- **Đầy đủ nguyên nhân không khả dụng (`AvailabilityConflictType`)**:
  - `VehicleSuspended`, `CategoryInactive`, `BookingInUse` (kể cả xe khách trả trễ), `BookingConfirmed`, `BookingPendingDeposit` (chỉ khi `DepositDeadline > UtcNow`), `Maintenance` (`Scheduled` / `InProgress`).
  - Bảo mật: Guest/Customer nhận mô tả khung giờ bận **không lộ `BookingCode`**; Staff/Admin nhận kèm `ReferenceCode` (`BookingCode` / `BT-#MaintenanceId`).
- **Màn hình `AvailabilityPage.xaml` (cho cả Admin & Staff)**:
  - Mở từ `AdminSidebar`, `StaffSidebar`, hoặc bấm nút **"Kiểm tra lịch trống"** trên `Admin/VehiclePage.xaml`.

---

## 3. F24 — Quản lý Lịch Bảo trì Phương tiện (`feature/tv2-f24-maintenance`)

- **Tích hợp `dbo.usp_ScheduleMaintenance` & Vòng đời Bảo trì (`IMaintenanceService` / `MaintenanceService`)**:
  - Phân quyền chặt tại tầng Service + DB: chỉ `Admin` đang hoạt động mới có quyền xếp lịch, cập nhật chi phí/khung giờ hoặc chuyển trạng thái bảo trì.
  - Hỗ trợ cả **Đặt lịch bảo trì trước (`Scheduled`)** (gọi trực tiếp `dbo.usp_ScheduleMaintenance` kèm lưu `Cost`) và **Đưa xe vào bảo trì khẩn cấp ngay (`InProgress`)** (khóa `CRMS_VEHICLE_{id}`).
  - Chặn xếp hoặc dời lịch bảo trì chồng lên đơn thuê đang giữ xe (`InUse`, `Confirmed`, `PendingDeposit` còn hạn) hoặc phiếu bảo trì `Scheduled`/`InProgress` khác; tôn trọng biên nửa mở `[StartAt, EndAt)`.
  - Vòng đời `Scheduled → InProgress → Completed / Cancelled`: khi chốt `Completed` (kèm chi phí thực tế `Cost`) hoặc `Cancelled`, phiếu tự động khóa chỉnh sửa để bảo toàn lịch sử và **giải phóng ngay phương tiện** cho `F07` (`VehicleAvailabilityService`) & `usp_CreateBooking`.
- **Màn hình `MaintenancePage.xaml` (Admin)**:
  - Bật nút **"Quản lý bảo trì"** trên `AdminSidebar.xaml` và nút tắt **"Lịch bảo trì"** trên `Admin/VehiclePage.xaml`.

---

## 4. Hợp đồng tích hợp cho `TV1` & `TV6`

```csharp
using CRMS.Services;
using CRMS.Services.Vehicles;

// --- F06: Danh sách & chi tiết xe cho TV1 ---
IVehicleService vehicles = new VehicleService();
var list = await vehicles.GetVehiclesAsync(new VehicleFilter(
    Keyword: "Toyota",
    OperationalStatus: "Active",
    OnlyActiveCategories: true));
var detail = await vehicles.GetVehicleDetailsAsync(vehicleId);

// --- F07: Kiểm tra lịch trống [startLocal, endLocal) cho TV1 & TV6 ---
IAvailabilityService availability = new VehicleAvailabilityService();
HashSet<long> freeIds = await availability.GetAvailableVehicleIdsAsync(startLocal, endLocal);

VehicleAvailabilityResult check = await availability.CheckAvailabilityAsync(
    vehicleId,
    startLocal,
    endLocal,
    requireFutureStart: true);

if (!check.IsAvailable)
{
    string summary = check.SummaryReason;
    IReadOnlyList<AvailabilityConflict> reasons = check.Conflicts;
}
```

---

## 5. Chạy kiểm thử tự động (`F06 + F07 + F24`)

Từ thư mục gốc chứa `CRMS.slnx`:

```powershell
# 1. Kiểm thử logic offline & MVVM F06 + F07 + F24
dotnet run --project tests/CRMS.VehicleChecks/CRMS.VehicleChecks.csproj

# 2. Kiểm thử tích hợp SQL Server F06 + F07 + F24 trên DB tạm độc lập (tự tạo & tự xóa sạch)
dotnet run --project tests/CRMS.VehicleChecks/CRMS.VehicleChecks.csproj -- --integration "$PWD/CRMS.sql"

# 3. Kiểm thử render đủ 6 giao diện WPF của TV2 & xác nhận 0 lỗi DataBinding
dotnet run --project tests/CRMS.VehicleChecks/CRMS.VehicleChecks.csproj -- --render "$PWD/artifacts_preview"
```
