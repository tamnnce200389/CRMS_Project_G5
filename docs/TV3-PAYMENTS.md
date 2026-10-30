# TV3 — thanh toán độc lập với các màn hình chưa hoàn thiện

## Đã triển khai

- F12: danh sách cọc tiền mặt, xác nhận Staff/Admin đang hoạt động, cọc đúng 50% tổng sau giảm giá; tạo Payment Pending rồi gọi `usp_ConfirmDeposit` trong cùng transaction.
- F14: tổng hợp tiền thuê sau voucher, phụ phí trả xe, tiền thành công đã thu/hoàn; thu đúng toàn bộ số dư bằng tiền mặt; ghi Payment Balance và gọi `usp_CompleteBooking` cùng transaction. Không đủ tiền hoặc không có biên bản trả xe thì không hoàn tất.
- Hoàn tiền: `IRefundRecordingService.RecordManualRefund` ghi bản ghi Refund mới, chỉ đơn Cancelled/Expired, không vượt tiền thực nhận còn lại; yêu cầu mã duyệt/đối soát. Đây là ghi sổ sau khi người có thẩm quyền đã duyệt và thực hoàn, không phải chức năng tự chuyển tiền hoặc tự duyệt yêu cầu TV7.
- Tra cứu: Staff/Admin xem mọi đơn; Customer chỉ xem đơn của mình. Không đăng nhập hoặc tài khoản bị vô hiệu hóa bị từ chối.
- Chống trùng: Guid request key ổn định cho một thao tác; kiểm tra lại nội dung khi thử lại, khóa bản ghi Booking và transaction, unique index theo schema hiện có. Mã duyệt hoàn tiền không được dùng hai lần cho cùng đơn.

## Cách mở màn hình

Staff/Admin: menu đơn thuê/thanh toán → Xác nhận cọc tiền mặt, Thu tiền còn lại, Ghi nhận hoàn tiền, Tổng hợp thanh toán.
Customer: trang chủ → Thanh toán của tôi → nhập BookingCode của chính mình.

Xác nhận thu/hoàn chỉ bấm sau khi đã thực nhận/chi tiền. Hộp thoại mặc định chọn Không.
Nếu giao dịch báo lỗi kết nối không rõ đã lưu hay chưa, tra cứu lại trước khi nhận/chi thêm tiền; thử lại cùng thao tác giữ nguyên request key.

## Hợp đồng tích hợp cho đồng đội

```csharp
IPaymentSummaryService summaries = new PaymentService();
var summary = await summaries.GetPaymentSummary(bookingId); // TV1, chỉ đọc
var remaining = await summaries.GetOutstandingBalance(bookingId); // TV5
IRefundRecordingService refunds = new PaymentService();
var receipt = await refunds.RecordManualRefund(
    bookingId, amount, "BankTransfer", approvalReference, stableRequestKey); // TV7
```

Namespace: `CRMS.Services.Payments`. Người dùng lấy từ phiên `CurrentUserService`; không truyền ID Staff tùy ý từ giao diện.

- TV6 cung cấp Booking hợp lệ, BookingCode/BookingId, TotalAmount (đã trừ voucher), DepositAmount, DepositMethod, DepositDeadline UTC. Service đọc lại DB, không tin số tiền cũ trên màn hình.
- TV5 lưu VehicleReturn với LateFee/DamageFee/OtherFee và chuyển Booking sang Returned trước khi thu tiền cuối. Chưa có biên bản thì số dư chỉ là tạm tính; không được hoàn tất.
- TV7 cung cấp mã chứng từ duyệt/đối soát duy nhất và request key bền vững cho lần hoàn. Một lần hoàn từng phần phải có chứng từ riêng.
- Tiền thực nhận = Success Deposit + Success Balance − Success Refund. Không cộng Pending/Failed/Cancelled, không trừ voucher lần hai.
- Với đơn hủy/hết hạn, OutstandingBalance bằng 0; dùng RefundableAmount để đối soát khoản hoàn.
- `usp_CancelBooking` hiện có thể tạo Refund Pending. Bản ghi này là yêu cầu, không được tính là tiền đã hoàn. Service thêm Refund Success mới với chứng từ, giữ nguyên yêu cầu và Payment cũ để đối soát. TV7 không được cộng cả yêu cầu Pending vào số tiền đã hoàn.

## F11 VNPay — chưa tích hợp thật

Chưa có backend xác thực IPN, cấu hình merchant hoặc cơ chế xác thực WPF ↔ backend trong repository này. Không có chức năng VNPay thật và không giả lập trạng thái thành công trong bản bàn giao.

Phần cần thống nhất trước khi nối:
- Backend tạo phiên từ Booking hợp lệ và người dùng đã xác thực; server tự lấy số tiền cọc từ DB.
- WPF nhận URL HTTPS được backend cho phép, mở bằng trình duyệt hệ thống và truy vấn trạng thái đã xác thực.
- Backend kiểm tra chữ ký, mã merchant/transaction, số tiền, Booking và kết quả; xử lý callback trùng; gọi usp_ConfirmDeposit; xử lý NeedsManualRefund khi tiền đến muộn.
- Browser return không xác nhận tiền; VNPay secret không được lưu trong WPF.
- Bài test chữ ký sai, tiền sai và IPN trùng chỉ đánh dấu đạt khi backend thực sự có và được kiểm thử. Test procedure đến muộn bên dưới không thay thế test VNPay.

## Kiểm thử mặc định: không kết nối hoặc thay đổi database

Từ thư mục chứa CRMS.slnx:

```powershell
dotnet run --project tests/CRMS.PaymentChecks/CRMS.PaymentChecks.csproj
# Chỉ chạy tùy chọn sau khi chủ động cho phép tạo DB kiểm thử:
dotnet run --project tests/CRMS.PaymentChecks/CRMS.PaymentChecks.csproj -- --integration "$PWD/CRMS.sql"
```

Mặc định dùng Windows Authentication trên localhost. Đặt `$env:CRMS_TEST_SERVER` nếu SQL Server có instance riêng.
Bộ integration test tạo database mới `CRMS_TV3_Test_<GUID>`, nạp schema và dữ liệu kiểm thử, gọi code thật rồi xóa đúng database vừa tạo trong finally. Không dùng App.config, không sửa database CRMS hiện tại. Cần quyền CREATE/DROP DATABASE; nếu test bị dừng cưỡng bức hãy kiểm tra database test còn lại trước khi chạy lại.

Bao phủ: cọc 50%, không trừ voucher hai lần, loại giao dịch chưa thành công, quyền đọc/ghi, lần gọi lặp, yêu cầu đồng thời, tiền không khớp, cọc quá hạn, receipt đến muộn không hồi sinh đơn, phụ phí, chặn hoàn tất khi thiếu tiền/biên bản, hoàn từng phần, chống hoàn vượt, đổi phiên giữa thao tác và rollback.

## Điều kiện chạy trên DB của nhóm

Schema/index/procedure phải tương ứng với CRMS.sql trong repo. Không tự động áp dụng script vào DB hiện tại. SQL script không phải migration cho mọi phiên bản schema cũ; đối chiếu trước nếu máy bạn từng có schema khác.
Các thay đổi này dùng schema hiện có, không thêm bảng/cột và không cần các màn hình đồng đội chạy để kiểm thử service.

## Xem giao diện mẫu không dùng database

```powershell
dotnet run --project tests/CRMS.PaymentChecks/CRMS.PaymentChecks.csproj -- --render "$PWD/tests/previews"
```

Lệnh này chỉ tạo ảnh PNG bằng dữ liệu trong bộ nhớ, không kết nối SQL Server. Các nút ghi tiền trong ảnh được khóa vì không có phiên đăng nhập thật.

## Tùy chọn tạo database demo — không chạy nếu chưa muốn ghi database

```powershell
dotnet run --project tests/CRMS.PaymentChecks/CRMS.PaymentChecks.csproj -- --integration "$PWD/CRMS.sql" --demo
```

Lệnh này chủ động GIỮ một database demo mới và in tên database cùng ba BookingCode:
1. PendingDeposit: vào trang cọc, xác nhận 450.000 đ.
2. Returned: tra cứu ở trang tiền còn lại, cọc đã thu 450.000 đ, phụ phí 70.000 đ, còn phải thu 520.000 đ.
3. Cancelled: tra cứu ở trang hoàn tiền, có thể hoàn tối đa 450.000 đ; nhập mã chứng từ trước khi ghi nhận.

Đổi **Database** trong App.config trên máy sang đúng tên database demo vừa in (giữ Server đúng máy), chạy ứng dụng, đăng nhập `staff01@crms.example.com` / `Staff@12345!` từ seed cục bộ. Customer demo `customer01@crms.example.com` / `Customer@12345!` sở hữu các đơn mẫu. Đây là tài khoản demo có sẵn trong SQL của nhóm, không dùng ngoài môi trường thử nghiệm.

Kết thúc demo thì khôi phục App.config, không commit cấu hình máy riêng. Database demo được giữ để bạn thao tác tiếp, không tự xóa và không thay đổi CRMS thật. Mỗi lần chạy tạo tên database mới; xóa đúng database demo của mình trong SSMS khi không cần nữa.
