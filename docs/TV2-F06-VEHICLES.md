# TV2 — F06: Quản lý Phương tiện, Loại xe & Ảnh xe

## Đã triển khai trong nhánh `feature/tv2-f06-vehicle-crud`

- **F06 — CRUD Xe (`Vehicles`)**:
  - Màn hình Admin `CRMS/Views/Admin/VehiclePage.xaml` + cửa sổ `CRMS/Views/Admin/VehicleEditWindow.xaml`.
  - Hỗ trợ Thêm mới, Chỉnh sửa, Tạm ngưng / Kích hoạt (`OperationalStatus`: `Active` ↔ `Suspended`), tìm kiếm theo từ khóa và lọc theo Loại xe / Trạng thái.
  - Validate đầy đủ các ràng buộc `CRMS.sql`: biển số không trùng (`UX_Vehicles_Plate`), giá thuê `> 0` (tối đa 2 số lẻ), năm sản xuất `1990..2100`, số chỗ `1..30`, hộp số (`Automatic`/`Manual`), nhiên liệu (`Gasoline`/`Diesel`/`Electric`/`Hybrid`), công-tơ-mét không âm và không được giảm khi sửa xe.
  - Chặn chuyển xe sang `Suspended` nếu xe đang có đơn thuê `InUse`, `Confirmed`, hoặc `PendingDeposit` còn hạn giữ chỗ (sử dụng khóa giao dịch `CRMS_VEHICLE_{id}` đồng bộ với `usp_CreateBooking` và `usp_ScheduleMaintenance`).
  - Tự động đồng bộ 5 gói giá thuê dài hạn (`1, 3, 6, 9, 12` tháng = `DailyRate * 30`) trong `dbo.VehicleLongTermRates` ngay trong transaction khi thêm xe mới hoặc cập nhật `DailyRate` (không chạm file Model của `TV1`).
- **F06 — CRUD Loại xe (`VehicleCategories`)**:
  - Cửa sổ `CRMS/Views/Admin/CategoryManagementWindow.xaml`: Thêm, sửa, bật/tắt `IsActive`, chặn trùng tên loại xe, chặn tắt loại xe khi vẫn còn xe `Active`, và chặn kích hoạt xe thuộc loại đang bị tắt.
- **F06 — Quản lý Bộ ảnh xe (`VehicleImages`)**:
  - Hỗ trợ cả 2 nguồn ảnh: chọn tệp ảnh từ máy tính (tự sao chép vào thư mục `VehicleUploads/` của ứng dụng dưới dạng URI `file:///...`) và nhập URL `http(s)://`.
  - Xem trước ảnh, đổi thứ tự `SortOrder`, xóa ảnh, và đảm bảo mỗi xe có đúng 1 ảnh đại diện (`IsPrimary = 1`) khi có ảnh.
- **Tương thích ngược**:
  - Màn hình `CRMS/Views/Staff/StaffVehiclePage.xaml` của Staff giữ nguyên chế độ tra cứu chỉ đọc.
  - Mọi thao tác ghi đều kiểm tra quyền `Admin` đang hoạt động tại tầng `VehicleService` (không chỉ ẩn nút trên giao diện).

## Ghi chú thay đổi file dùng chung (cho Pull Request review)

- `CRMS/Models/Vehicle.cs` & `CRMS/Data/CrmsContext.cs`: Sửa lỗi EF Core Database-First scaffold thiếu `.HasFilter("([IsPrimary]=(1))")` trên `UX_Images_OnePrimary` khiến quan hệ `Vehicle` ↔ `VehicleImages` bị hiểu nhầm là `1-1`. Đã chuyển sang `ICollection<VehicleImage> VehicleImages` (`1-N`) khớp đúng thiết kế `CRMS.sql` (tương tự bản sửa `Booking.Payments` trước đó của `TV3`). Không có module nào của thành viên khác bị ảnh hưởng.
- `CRMS/Helpers/VietnameseLabelConverter.cs`: Bổ sung nhãn tiếng Việt cho `SUSPENDED` (`Tạm ngưng`) và 4 loại nhiên liệu (`Gasoline`, `Diesel`, `Electric`, `Hybrid`).

## Hợp đồng tích hợp cho `TV1`

Namespace: `CRMS.Services.Vehicles`

```csharp
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

## Chạy kiểm thử tự động

Từ thư mục gốc chứa `CRMS.slnx`:

```powershell
# 1. Kiểm thử logic offline & MVVM (mặc định không kết nối hoặc thay đổi database)
dotnet run --project tests/CRMS.VehicleChecks/CRMS.VehicleChecks.csproj

# 2. Kiểm thử tích hợp SQL Server trên DB tạm độc lập (tự tạo và tự xóa sạch sau khi chạy)
dotnet run --project tests/CRMS.VehicleChecks/CRMS.VehicleChecks.csproj -- --integration "$PWD/CRMS.sql"

# 3. Kiểm thử render giao diện WPF & kiểm tra 0 lỗi DataBinding
dotnet run --project tests/CRMS.VehicleChecks/CRMS.VehicleChecks.csproj -- --render "$PWD/artifacts_preview"
```
