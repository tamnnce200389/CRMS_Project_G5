# Entity của cơ sở dữ liệu CRMS

`CRMS/Models` chứa trực tiếp **34 file `.cs`**, mỗi file là một entity tương ứng với một bảng trong `CRMS.sql`. Không có thư mục con trong `Models`.

| File trong Models | Bảng SQL |
| --- | --- |
| `Amenity.cs` | `Amenities` |
| `Booking.cs` | `Bookings` |
| `BookingOfferSnapshot.cs` | `BookingOfferSnapshots` |
| `BookingStatusHistory.cs` | `BookingStatusHistory` |
| `BookingVoucher.cs` | `BookingVouchers` |
| `Driver.cs` | `Drivers` |
| `MaintenanceRecord.cs` | `MaintenanceRecords` |
| `Payment.cs` | `Payments` |
| `RentalLocation.cs` | `RentalLocations` |
| `RentalPolicy.cs` | `RentalPolicies` |
| `RentalPurpose.cs` | `RentalPurposes` |
| `Review.cs` | `Reviews` |
| `SearchCity.cs` | `Cities` |
| `SearchSetting.cs` | `SearchSettings` |
| `User.cs` | `Users` |
| `Vehicle.cs` | `Vehicles` |
| `VehicleAmenity.cs` | `VehicleAmenities` |
| `VehicleCalendarBlock.cs` | `VehicleCalendarBlocks` |
| `VehicleCategory.cs` | `VehicleCategories` |
| `VehicleDeliveryArea.cs` | `VehicleDeliveryAreas` |
| `VehicleDriverRoute.cs` | `VehicleDriverRoutes` |
| `VehicleHandover.cs` | `VehicleHandovers` |
| `VehicleHourlyRate.cs` | `VehicleHourlyRates` |
| `VehicleImage.cs` | `VehicleImages` |
| `VehicleLongTermRate.cs` | `VehicleLongTermRates` |
| `VehicleOwner.cs` | `VehicleOwners` |
| `VehicleRentalMode.cs` | `VehicleRentalModes` |
| `VehicleRentalPolicy.cs` | `VehicleRentalPolicies` |
| `VehicleRentalPurpose.cs` | `VehicleRentalPurposes` |
| `VehicleReturn.cs` | `VehicleReturns` |
| `VehicleSearchProfile.cs` | `VehicleSearchProfiles` |
| `VehicleTravelArea.cs` | `VehicleTravelAreas` |
| `VehicleVoucherEligibility.cs` | `VehicleVoucherEligibility` |
| `Voucher.cs` | `Vouchers` |

Các thuộc tính trước đây nằm trong `Booking.Billing.cs` đã gộp vào `Booking.cs`. `BillingLabel` vẫn có `[NotMapped]`, không tạo cột DB.

## Kiểu hỗ trợ truy vấn và giao diện

Tám kiểu dưới đây nằm trực tiếp trong `CRMS/Dtos`, mỗi kiểu một file riêng:

- `SearchCatalogRow`: kết quả view `dbo.vVehicleSearchCatalog`.
- `SearchOwnerRating`: kết quả view `dbo.vSearchOwnerRatings`.
- `SearchAvailableVehicle`: kết quả hàm `dbo.ufn_SearchAvailableVehicles`.
- `SearchDeliveryQuote`: kết quả hàm `dbo.ufn_SearchDeliveryQuotes`.
- `VehicleTypeFacet`, `BrandFacet`, `SelfDriveFacets`: dữ liệu các lựa chọn bộ lọc.
- `SeasonalDestination`: dữ liệu hiển thị điểm đến theo mùa.

Các kiểu giữ nguyên namespace `CRMS.Models` để tương thích với các nơi sử dụng hiện tại. Namespace không quyết định vị trí file trong Solution Explorer.

`Data/CrmsContext.cs` và `Data/CrmsContext.Search.cs` giữ nguyên cấu hình EF: 34 bảng, 2 view và 2 hàm truy vấn. Việc sắp xếp lại file không thay đổi schema hay dữ liệu; không cần migration hoặc chạy lại SQL.
