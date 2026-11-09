# Chuyển các chức năng hiện có sang MVVM

Nhánh: `feature/MVVM`. Giữ giao diện hiện tại của nhóm và các thủ tục SQL đang dùng. Không sửa `CRMS.sql`, `App.config`, mật khẩu hoặc dữ liệu CRMS đang sử dụng.

## Phân chia trách nhiệm

- `Views`: XAML, mở/đóng cửa sổ, focus, cuộn và các thao tác hiển thị. Code-behind chuyển kết quả của cửa sổ chọn vào ViewModel; không truy vấn hoặc lưu Entity Framework.
- `ViewModels`: trạng thái màn hình, command, kiểm tra đầu vào, lựa chọn bộ lọc, tính giá hiển thị, yêu cầu điều hướng. Không tạo Page/Window, gọi MessageBox/SaveFileDialog hoặc truy cập DbContext.
- `Services`: truy vấn, cập nhật, kiểm tra quyền bằng tài khoản hiện có trong database, giao dịch và chống lưu đè.
- `Dtos`: dữ liệu hiển thị và yêu cầu truyền giữa các lớp. Một số namespace công khai được giữ để không phá các module cũ.
- `Helpers`: các bộ chuyển đổi WPF, command bất đồng bộ và các triển khai phụ thuộc WPF của dịch vụ điều hướng, xác nhận, chọn/lưu CSV.
- `App`: ghép các dịch vụ phụ thuộc WPF khi khởi động ứng dụng.

## Phần đã chuyển và sửa

1. Hồ sơ cá nhân, danh sách chuyến thuê và quản lý người dùng có ViewModel và service riêng. Dữ liệu tải của phiên đăng nhập cũ bị hủy/bỏ qua, không xuất hiện lại khi đổi tài khoản.
2. Cập nhật hồ sơ/vai trò/trạng thái tài khoản dùng RowVersion; không cho tự khóa hoặc tự đổi vai trò. DTO không mang mật khẩu.
3. Quản lý tài xế dùng DriverService thay đường ghi trực tiếp qua repository. Staff được đọc, Admin được ghi; kiểm tra lại quyền trong database. So sánh bản gốc khi sửa để phát hiện thay đổi đồng thời, không cần thêm cột SQL.
4. Trạng thái tìm xe ở trang chủ, điểm đón/đến, lựa chọn địa điểm/nhu cầu, chọn ngày/giờ, ảnh xe và chi tiết thuê tháng được chuyển vào ViewModel. Hai lộ trình liên tỉnh giữ điểm đến theo ID khi tải lại thành phố.
5. Giá thuê tháng và nhãn ưu đãi đọc từ bảng giá. Chỉ được chọn gói tồn tại; thời hạn và tổng tiền được truyền sang luồng checkout cũ.
6. Việc tạo Page và bộ nhớ trang nằm ở PageNavigationService. Đổi tài khoản xóa bộ nhớ trang cũ; ViewModel chính chỉ dùng giao diện INavigationService.
7. Dùng AsyncRelayCommand cho thao tác bất đồng bộ: chống bấm lặp, khôi phục trạng thái sau lỗi, có thể chờ kết quả trong kiểm thử.
8. Xác nhận thu/hoàn tiền và lưu CSV qua các giao diện dịch vụ. Giữ giao dịch thanh toán, kiểm tra lịch xe và nghiệp vụ đặt xe hiện hành.
9. Loại bỏ DriverRepository và bộ nhớ BookingSelectionService không còn được dùng; BookingSelection được truyền trực tiếp vào checkout.

Trang quản lý đơn và phân công tài xế đang là trang mẫu vẫn giữ phạm vi đó. Đợt này không thêm nghiệp vụ giao/trả xe hoặc phân công mới. Code-behind phục vụ hiển thị và vòng đời cửa sổ vẫn được giữ; đây là phần phù hợp với WPF MVVM.

## Kiểm tra lại

Chạy tại thư mục chứa solution, với .NET 8 Windows và SQL Server cho phần kiểm thử tích hợp:

```powershell
dotnet build CRMS/CRMS.csproj
dotnet run --project tests/CRMS.MvvmChecks
dotnet run --project tests/CRMS.VehicleChecks
dotnet run --project tests/CRMS.PaymentChecks
```

Kiểm tra service với SQL:

```powershell
dotnet run --project tests/CRMS.MvvmChecks -- --integration CRMS.sql
```

Lệnh tích hợp tự tạo database `CRMS_MVVM_Test_<ngẫu nhiên>`, cài dữ liệu mẫu trong database đó và xóa trong `finally`. Dùng Windows Authentication, server mặc định `localhost`; có thể đặt biến `CRMS_TEST_SERVER` để chọn SQL Server kiểm thử. Tài khoản chạy kiểm thử cần quyền tạo/xóa database tạm. Không dùng connection string CRMS trong App.config.

Kiểm tra dựng màn hình và binding mà không truy cập database:

```powershell
dotnet run --project tests/CRMS.MvvmChecks -- --render "$env:TEMP\CRMS_MVVM_render"
```

Sau kiểm thử tự động, nên mở Visual Studio, chạy F5 và kiểm tra tài khoản Admin/Staff/Customer, tìm xe cả ba hình thức, chọn địa điểm/thời gian và xuất báo cáo. Kiểm thử tự động không thay thế toàn bộ thao tác thật của nhóm trên dữ liệu đang dùng.
