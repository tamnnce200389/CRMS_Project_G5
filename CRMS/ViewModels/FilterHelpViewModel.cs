namespace CRMS.ViewModels;

public sealed record FilterHelpViewModel(string Title, string Description)
{
    public static FilterHelpViewModel? For(string title) => title switch
    {
        "Miễn phí đưa đón" => new(title,
            "Xe có chính sách miễn phí đưa đón trong nội thành của thành phố đón đã chọn. Bấm bộ lọc để áp dụng trực tiếp, không cần chọn điểm đưa đón.\n\nPhạm vi miễn phí phụ thuộc từng xe; không mặc định miễn phí mọi địa chỉ trong thành phố. Miễn phí đưa đón không phải miễn tiền thuê xe và tài xế."),
        "Xe điện" => new(title, "Chỉ hiển thị xe sử dụng điện, phục vụ đúng hành trình và còn trống trong thời gian đã chọn."),
        "Xe xăng & điện" => new(title, "Chỉ hiển thị xe hybrid kết hợp động cơ xăng và điện. Đây là một nhóm nhiên liệu riêng; không bao gồm tất cả xe chạy xăng.\n\nNếu không có xe hybrid phù hợp hành trình và lịch thuê, kết quả sẽ trống."),
        "Thuê giờ" => new(title,
            "Xe có biểu giá thuê theo giờ, phù hợp các chuyến đi ngắn. Thời gian thuê tối thiểu, tối đa và bước làm tròn tùy biểu giá từng xe.\n\nChọn thời gian nhận và trả xe để xem số giờ tính phí và tổng tiền trước khi xác nhận đặt xe."),
        "Giao nhận tận nơi" => new(title,
            "Xe có dịch vụ giao nhận tại nhà, sân bay hoặc điểm giao nhận trong phạm vi phục vụ của xe.\n\nChọn điểm giao nhận để tìm xe phù hợp. Phạm vi và phí phụ thuộc từng xe, từng địa điểm; giá thuê chưa bao gồm phí giao nhận."),
        "Chủ xe 5★" => new(title,
            "Nhóm chủ xe uy tín, đạt yêu cầu về điểm đánh giá và số lượng đánh giá từ các chuyến thuê đã hoàn tất.\n\nĐiểm và số đánh giá được hiển thị trên thẻ xe. Nhãn 5★ không có nghĩa mọi chủ xe đều có điểm tuyệt đối 5,0."),
        "Đặt xe nhanh" => new(title,
            "Xe được khai báo hỗ trợ chính sách đặt nhanh. Khi bạn xác nhận đặt, hệ thống vẫn kiểm tra lịch trống, lịch bảo trì và lịch khóa của xe.\n\nĐơn được giữ chỗ sau khi tạo và chỉ xác nhận khi đã nhận đủ tiền cọc trong thời hạn quy định."),
        "Miễn thế chấp" => new(title,
            "Xe có chính sách không yêu cầu để lại tài sản thế chấp.\n\nMiễn thế chấp không phải miễn đặt cọc: khách vẫn thanh toán cọc 50% tiền thuê và các phụ phí phát sinh theo điều kiện thuê xe."),
        "Giảm giá" => new(title,
            "Xe có voucher hoặc ưu đãi phù hợp với điều kiện tìm kiếm. Khả năng sử dụng mã phụ thuộc thời hạn, giá trị đơn và số lượt còn lại.\n\nMỗi đơn chỉ áp dụng một voucher. Số tiền giảm được xác nhận khi áp dụng mã; bật bộ lọc không tự trừ voucher vào giá hiển thị."),
        _ => null
    };
}
