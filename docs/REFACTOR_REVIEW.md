# Rà soát và refactor CRMS — giữ nguyên hành vi

## Phạm vi và mốc đối chiếu

- Nhánh làm việc: `codex/refactor-mvvm-boundaries`.
- Mốc ban đầu: `aa18df1498e44013f0f64a44eca854ac407a647a` trên `feature/fix_UI`.
- Không có thay đổi tracked chưa commit trước khi bắt đầu. File không được Git quản lý `CRMS.sql.local-backup-20261027` được giữ nguyên.
- Không tìm thấy `AGENTS.md` trong repository hoặc các thư mục cha đã kiểm tra.
- Bản sao mã nguồn trước sửa: `C:/Users/tamnn/.codex/visualizations/2026/11/02/01a19d26-fbc3-70b3-a675-e3a1aa00f28e/crms-refactor-20261110/before-refactor.zip`.
- Đã quét 216 file C#/XAML sau sửa thuộc View, ViewModel, Service, Data, Models, DTO và Helpers. Inventory tĩnh: cùng thư mục sao lưu, file `source-inventory.json`. Các vị trí có phụ thuộc/logic liên quan được đọc và đối chiếu riêng; đây không phải cam kết đã kiểm thử thủ công mọi màn hình.
- Bản sửa ban đầu được bàn giao trong working tree, chưa commit/push. Theo yêu cầu tiếp theo của chủ dự án, bản refactor được commit và push lên nhánh riêng nêu trên; không merge hoặc tạo PR.

## Những vấn đề đã xác minh và giải quyết

| Vấn đề thực tế | Thay đổi | Hành vi giữ nguyên |
| --- | --- | --- |
| `VehicleRepository.GetAllAsync` chỉ bọc truy vấn EF; toàn bộ source, XAML và project không có nơi sử dụng lớp này | Bỏ lớp và thư mục Repository rỗng. Không chuyển một truy vấn chưa từng được gọi vào Service | Các màn hình xe vẫn gọi `IVehicleService`/`VehicleService` như trước; truy vấn, phân quyền, tracking không đổi |
| `VietQrCode` trong Services vừa tạo payload vừa tạo `BitmapSource`, phụ thuộc WPF và đọc stream ảnh | Giữ payload/CRC trong Service; chuyển nguyên thuật toán render ảnh sang `Helpers/QrImageRenderer`; ViewModel gọi helper trình bày | Cùng payload, mức sửa lỗi M, kích thước 8, cache OnLoad và Freeze. Không đổi số tiền, nơi nhận hoặc điều kiện xác nhận |
| `CustomerTrip` trộn kết quả truy vấn với nhãn tiếng Việt, ngày hiển thị và chữ nút | Giữ record dữ liệu; tách `TripItemViewModel` ở tầng trình bày; `MyBookingsViewModel` bọc record khi công bố lên màn hình | Service tiếp tục trả cùng dữ liệu và chạy cùng truy vấn; dữ liệu không bị thay bằng `object`/`dynamic`; không phát sinh phụ thuộc Service → ViewModel |
| Chữ nút thanh toán nằm trong DTO khiến sửa giao diện phải sửa lớp dữ liệu | Chuyển chữ và điều kiện đổi chữ sang DataTrigger trong `MyBookingsPage.xaml` | Giữ “Thanh toán” cho PendingDeposit, “Xem trạng thái demo” cho Expired/Cancelled và “Xem biên nhận demo” cho các trạng thái còn lại; điều kiện hiện nút và thao tác mở cửa sổ giữ nguyên |
| Hai ViewModel dashboard lặp lại trạng thái ngày, dữ liệu, loading, Command, kiểm tra phản hồi và xử lý lỗi | Tách `DashboardViewModel<TSnapshot>` dùng chung; Admin/Staff giữ phương thức query, quyền màn hình và thông báo riêng | Không sửa DashboardService, SQL, số liệu, múi giờ hay chính sách quyền. Các property Binding và thông báo cũ được giữ. Cho phép truyền hàm tải/IUserSession để kiểm thử mà không thêm tầng Repository/interface mới |
| `SeasonalDestination` chứa `Rect` cắt ảnh WPF nhưng nằm ở DTO và namespace Models | Đưa kiểu chỉ phục vụ trang chủ về ViewModels; bỏ import Models không còn cần thiết tại hai nơi dùng | Giữ nguyên danh sách điểm đến, ảnh, vùng cắt, chọn mùa và thao tác trên trang chủ |

`TripItemViewModel` là một hàng hiển thị bất biến. Khi trạng thái đổi, `MyBookingsViewModel` vẫn thay phần tử trong ObservableCollection như trước; không chuyển sang một cơ chế cập nhật hoặc lưu DB khác.

## Những lớp chủ động giữ lại

| Kiểu/lớp | Lý do giữ |
| --- | --- |
| `UserAccountDto`, `ProfileUpdateRequest` | Không trả `User` nguyên bản chứa PasswordHash. RowVersion cần cho chống lưu đè hồ sơ/quản trị tài khoản. Giữ cơ chế kiểm tra quyền và concurrency hiện có |
| `CustomerTrip` | Kết quả tổng hợp từ Booking và Vehicle, không phải bản sao vô ích của một Entity. Giữ record dữ liệu; chỉ phần trình bày được tách ra |
| `BookingSelection` | Yêu cầu đặt xe đã chọn, mang kỳ thuê/giá dự kiến/phương thức cọc; dùng trong Service và luồng checkout |
| `CarSearchFilter`, `CarSearchContext`, `CarSearchItem` | Điều kiện tìm kiếm và kết quả tổng hợp có mục đích. Không viết lại query/giá/lọc để giảm số lớp |
| `BrandFacet`, `VehicleTypeFacet`, `SelfDriveFacets` | Kết quả thống kê facet cho hộp lọc. Giữ số lượng và lựa chọn đang có |
| `SearchCatalogRow` | Ánh xạ keyless tới `vVehicleSearchCatalog`; SearchService thực sự dùng |
| `SearchAvailableVehicle`, `SearchDeliveryQuote` | Kiểu kết quả TVF và được dùng trong truy vấn lịch trống/giao nhận |
| `SearchOwnerRating` | Vẫn là ánh xạ EF của view. Không xóa chỉ vì ít tham chiếu C# trực tiếp; không đụng SQL view liên quan |
| `VehicleDetailsDto`, `VehicleImageDto` | Kết quả của Service xe đang có nơi sử dụng, không bỏ theo hậu tố tên |
| Các Entity và CrmsContext/partial | Giữ entity của DB, DbSet và cấu hình EF. Không gom nghiệp vụ hoặc tất cả kiểu dữ liệu vào DbContext |
| Các interface đang sử dụng | Giữ ranh giới phiên đăng nhập, giao dịch, xác nhận, điều hướng, xuất file; không tạo thêm interface cho mỗi Entity |

Vì vậy thư mục `Dtos` vẫn còn. Không thể kết luận “không còn DTO” chỉ bằng cách chuyển nguyên các lớp sang Services. Đợt này ưu tiên trách nhiệm và chiều phụ thuộc, theo yêu cầu refactor mới nhất.

## Luồng trước và sau

### Chuyến thuê

- Trước: `BookingQueryService → CustomerTrip (data + nhãn/chữ nút) → MyBookingsViewModel → XAML`.
- Sau: `BookingQueryService → CustomerTrip (data) → MyBookingsViewModel/TripItemViewModel → XAML/DataTrigger`.
- Code-behind tiếp tục chỉ mở cửa sổ, nhận tương tác, xử lý vòng đời và chuyển session thanh toán mới về ViewModel.

### QR

- Trước: Service tạo payload và trực tiếp trả ảnh WPF.
- Sau: Service tạo payload; helper trình bày tạo ảnh WPF; ViewModel công bố ảnh để View Binding.
- Không thay QR test 1.000đ, cọc mô phỏng, quyền Staff, tham chiếu ngân hàng, giao dịch SQL hoặc chống ghi trùng.

### Dashboard

- Trước: hai ViewModel tự lặp toàn bộ cùng một luồng tải.
- Sau: hai ViewModel cấu hình chung luồng tải của `DashboardViewModel<TSnapshot>`; Service thực hiện quyền và truy vấn y như cũ.

## File đã thay đổi

### Mã ứng dụng

- Sửa `CRMS/Dtos/CustomerTrip.cs`: chỉ giữ dữ liệu kết quả.
- Thêm `CRMS/ViewModels/TripItemViewModel.cs`: nhãn, ngày và trạng thái hiển thị.
- Sửa `CRMS/ViewModels/MyBookingsViewModel.cs`: bọc/cập nhật phần tử trình bày.
- Sửa `CRMS/Views/Customer/MyBookingsPage.xaml`: Binding vào record bên trong, chữ nút bằng DataTrigger.
- Sửa `CRMS/Views/Customer/MyBookingsPage.xaml.cs`: lấy BookingId từ hàng trình bày mới.
- Thêm `CRMS/Helpers/QrImageRenderer.cs`; sửa `CRMS/Services/Payments/VietQrCode.cs` và `CRMS/ViewModels/DemoPaymentViewModel.cs`.
- Thêm `CRMS/ViewModels/DashboardViewModel.cs`; sửa `AdminDashboardViewModel.cs`, `StaffDashboardViewModel.cs`.
- Chuyển `CRMS/Dtos/SeasonalDestination.cs` thành `CRMS/ViewModels/SeasonalDestination.cs`; sửa import ở `HomeViewModel.cs`, `Views/Guest/HomePage.xaml.cs`.
- Xóa `CRMS/Repositories/VehicleRepository.cs` sau khi kiểm tra tham chiếu.

### Kiểm thử và báo cáo

- Sửa `tests/CRMS.DemoPaymentChecks/Program.cs`: dữ liệu/hàng hiển thị mới, kiểm tra quyền dashboard/report, biên ngày Việt Nam và CSV trên DB tạm.
- Thêm `tests/CRMS.DemoPaymentChecks/RefactorRegressionChecks.cs`: trạng thái dashboard, phản hồi bất đồng bộ, nhãn và trạng thái tải/lỗi/rỗng của chuyến thuê.
- Sửa `tests/CRMS.PaymentChecks/TripNavigationChecks.cs`: kiểm tra template WPF thật và DataTrigger tại 7 trạng thái + hết hạn theo deadline + không phải demo.
- Sửa `tests/CRMS.PaymentChecks/CustomerUiChecks.cs`: dữ liệu dựng trang dùng đúng hàng ViewModel mới.
- Thêm báo cáo này. Không sửa các báo cáo Word/phân công chức năng.

## Kiểm chứng

- Trước sửa: build thành công; 61 kiểm tra demo không cần DB đạt. Log: `baseline-demo-checks.txt` tại thư mục sao lưu.
- Sau sửa: build ứng dụng và các project test liên quan thành công.
- DemoPaymentChecks: 141 kiểm tra, gồm unit và SQL integration; bao gồm thu QR, chống lặp/đồng thời, đơn quá hạn, cập nhật cửa sổ đang mở, báo cáo và dashboard loại demo, quyền Staff/Customer, biên ngày Việt Nam và CSV.
- PaymentChecks: 41 kiểm tra SQL về tiền mặt, phần còn lại, hoàn tiền, chống lưu trùng và đổi phiên trước commit.
- SearchChecks: 16 kiểm tra hiện có về lọc giao nhận theo khu vực/facet và SQL. Mã query Search không thay đổi.
- VehicleChecks: 55 kiểm tra unit hiện có; không chạy lại SQL integration của module xe vì query/nghiệp vụ module này không đổi.
- WPF thực tế: 16 kiểm tra điều hướng, trạng thái nút, template chuyến thuê và Binding kế thừa của hai trang dashboard (snapshot, ngày và RefreshCommand). Không có cảnh báo/lỗi Binding trong vùng chuyến thuê được kiểm tra. Đây không phải phép so sánh pixel của toàn bộ ứng dụng.
- Các kiểm tra sau sửa trên có tổng 269 lượt PASS; không cộng 61 lượt baseline vào tổng này.
- File SQL gốc, App.config, payment-demo.json, Data, Models, SearchService, ReportService và DashboardService được đối chiếu Git và không đổi.
- Các kiểm thử ghi dữ liệu tạo DB tên ngẫu nhiên `CRMS_DemoPay_Test_*`, `CRMS_TV3_Test_*`, `CRMS_Search_Test_*` và tự xóa. Không chạy SQL cập nhật/seed lên CRMS gốc.
- Scan tĩnh sau sửa không phát hiện ViewModel/View truy vấn DbContext/SQL; không còn Service import WPF, View hoặc ViewModel.
- Build VehicleChecks có cảnh báo NU1900 từ dữ liệu audit NuGet không tải được; không có lỗi biên dịch. Không nâng phiên bản package hoặc sửa cấu hình để che cảnh báo.

Lưu ý kiểm thử cũ có một assertion tích hợp còn mong chữ “Thanh toán QR demo”, trong khi commit gốc hiện tại đã đổi chữ thành “Thanh toán”. Kiểm thử sau refactor kiểm tra chữ đang chạy thật qua XAML; không đổi giao diện để khớp assertion cũ.

## Điểm ngoài phạm vi — chưa sửa, cần quyết định riêng

### Đã xác minh: vòng đời phiên của dashboard

Dashboard hiện chỉ so UserId/Role trước và sau khi tải. Nếu phiên đăng nhập bị kết thúc rồi tái lập cùng UserId/Role trong lúc query đang chạy, SessionVersion đổi nhưng hai giá trị kiểm tra vẫn giống nhau, nên phản hồi cũ vẫn có thể được nhận. Cách kiểm tra này đã có ở cả hai ViewModel trước refactor và được giữ nguyên khi tách lớp chung. Bài chẩn đoán riêng in `AUDIT` cho trường hợp này; không tính nó là kiểm thử PASS cho việc chống mọi phản hồi cũ.

Chưa bổ sung SessionStamp/SessionVersion guard vì đó là thay đổi hành vi xử lý phiên, cần chủ dự án xác nhận trước.

### Hành vi Search cần phân biệt với lỗi

Khi sửa bộ lọc trong lúc request đang chạy, mã hiện tại vẫn có thể công bố kết quả request cũ nhưng đặt `IsFilterPending=true`, khiến `CanChooseResults=false`. Nó không đổi lại các giá trị bộ lọc vừa nhập. Không tự chuyển sang tự tìm lại/xóa thẻ xe/chặn mọi phản hồi nếu chưa chốt trải nghiệm. Không sửa trong đợt này.

### Đề xuất cải thiện, chưa thực hiện

- Một số kiểu thẻ xe/facet vẫn chứa các property định dạng và hình học cho giao diện. Muốn tách tiếp cần lớp trình bày tương ứng và kiểm thử đầy đủ cả ba chế độ thuê; không chuyển chúng sang ViewModel khiến SearchService phụ thuộc ngược.
- Một số ViewModel vẫn tạo Service mặc định và dùng CurrentUserService tĩnh. Giữ các constructor/luồng đang chạy, không thêm DI framework hoặc interface hàng loạt chỉ để thay cấu trúc.
- Các trang đang là mẫu không được bổ sung chức năng. Không tuyên bố dự án hoàn thiện mọi chức năng hoặc MVVM tuyệt đối.

## Chưa kiểm chứng và giới hạn

- Chưa thao tác thủ công toàn bộ màn hình trên mọi kích thước/DPI.
- Không chạy trọn ma trận Search ba chế độ/giá/lịch/bộ lọc sau search trong đợt này: không chạm SearchService, filter contract, truy vấn hoặc layout thẻ xe. Bộ regression Search hiện có đã chạy.
- Đã kiểm tra nội dung CSV; chưa thao tác hộp thoại Save của Windows bằng tay.
- Không xác minh chuyển tiền ngân hàng thực tế, không quét QR, không tự xác nhận tiền thật.
- Không thay schema hoặc dữ liệu dự án; đợt refactor này không yêu cầu thành viên chạy lại DB.
