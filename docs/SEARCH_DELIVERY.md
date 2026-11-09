# Thanh bộ lọc và giao nhận theo khu vực

## Hành vi

- Khôi phục các nút lọc nhanh của bản gốc: loại xe, hãng xe, thuê giờ, giao nhận tận nơi, chủ xe 5★, đặt xe nhanh, miễn thế chấp, giảm giá, ghế trẻ em và di chuyển mở rộng. Có nút cuộn khi không đủ chiều ngang.
- Với xe tự lái, bấm **Giao nhận tận nơi** để lọc ngay theo khu vực đã chọn khi tìm xe; bấm lần nữa để bỏ lọc. Không mở hộp chọn điểm giao nhận.
- Xe phải thuộc khu vực đang tìm và có vùng giao nhận đang hoạt động cho đúng khu vực, đúng hình thức thuê. Không lấy xe chỉ giao ở khu vực khác hoặc xe không hỗ trợ giao nhận.
- Khi chưa chọn khu vực cụ thể, giao diện nhắc chọn khu vực; không tự đoán địa điểm.
- Chip điều kiện hiển thị tên khu vực thực tế và có thể xóa riêng, không làm mất các bộ lọc khác.
- Lọc theo khu vực không xác nhận giao được mọi địa chỉ, không tự tính phí giao nhận và không mặc định miễn phí. Bộ lọc giao/đón miễn phí theo địa chỉ vẫn kiểm tra tọa độ và phạm vi phục vụ.

## Các class thay đổi

| File / class | Thay đổi |
| --- | --- |
| `CRMS/Views/Guest/CarSearchPage.xaml` | Khôi phục dãy nút đầy đủ, tooltip dùng khu vực hiện tại; chỉ hiển thị chọn địa chỉ khi cần kiểm tra giao nhận miễn phí theo điểm. |
| `CRMS/Views/Guest/CarSearchPage.xaml.cs` / `CarSearchPage` | Nút giao nhận gọi thẳng thao tác bật/tắt lọc; khôi phục handler ghế trẻ em và di chuyển mở rộng với modal backdrop hiện có. |
| `CRMS/ViewModels/CarSearchViewModel.cs` / `CarSearchViewModel` | Thêm `DeliveryFilterLabel`, `ToggleDeliveryAsync`; dùng khu vực hiện tại và bỏ tọa độ cũ khi chỉ lọc giao nhận theo khu vực. |
| `CRMS/Services/SearchService.cs` / `CarSearchFilter`, `SearchService` | Cho phép giao nhận theo thành phố không cần tọa độ; truy vấn vùng giao nhận đúng thành phố và hình thức thuê; giữ kiểm tra tọa độ cho phí giao nhận theo điểm. |
| `CRMS/ViewModels/FilterHelpViewModel.cs` / `FilterHelpViewModel` | Giải thích cách lọc trực tiếp, phạm vi giao nhận và phí thực tế. |
| `tests/CRMS.SearchChecks` | Kiểm thử luồng bật/tắt, khu vực, tọa độ cũ, chip điều kiện và truy vấn SQL thực tế. |

Không thay đổi schema hoặc dữ liệu trong `CRMS.sql`; không cần chạy lại DB.

## Kiểm thử

```powershell
dotnet run --project tests/CRMS.SearchChecks/CRMS.SearchChecks.csproj
dotnet run --project tests/CRMS.SearchChecks/CRMS.SearchChecks.csproj -- --integration "D:/path/to/CRMS.sql"
```

Chế độ integration tạo database `CRMS_Search_Test_<guid>` riêng trên SQL Server được chỉ định bằng biến môi trường `CRMS_TEST_SERVER` (mặc định `localhost`), nạp dữ liệu demo vào database đó và tự xóa khi chạy xong. Không kết nối database từ `App.config` và không sửa database ứng dụng.
