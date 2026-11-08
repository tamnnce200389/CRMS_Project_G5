
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services;
using CRMS.Services.Vehicles;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CRMS.ViewModels;

public sealed class BookingCheckoutViewModel : BaseViewModel
{
    private readonly BookingSelection _selection;
    private readonly CustomerBookingService _service;
    private readonly ApplyVoucherService _voucherService = new();
    private readonly Guid _requestKey = Guid.NewGuid();

    private string _voucherCode = "";
    private string _selectedVoucherCode = "";
    private string _voucherMessage = "";
    private decimal _discountAmount;
    private bool _busy;

    private string _status =
        "Đặt cọc tiền mặt; đơn chờ nhân viên xác nhận đã nhận cọc.";

    public string VehicleName { get; }

    public string RentalLabel => _selection.PriceUnit switch
    {
        "Hour" => "Thuê theo giờ",
        "Month" => $"Gói {_selection.DurationMonths} tháng",
        _ => _selection.RentalMode == "WithDriver"
            ? "Thuê có tài xế"
            : "Thuê theo ngày"
    };

    public string PeriodLabel =>
        $"{_selection.StartAt:HH:mm dd/MM/yyyy} → " +
        $"{_selection.EndAt:HH:mm dd/MM/yyyy}";

    public string TotalLabel =>
        Result != null
            ? $"{Result.TotalAmount:N0} ₫"
            : _selection.ExpectedTotal.HasValue
                ? $"{_selection.ExpectedTotal.Value:N0} ₫"
                : "Xác nhận theo giá hiện tại";

    public string DepositLabel =>
        Result != null
            ? $"{Result.DepositAmount:N0} ₫"
            : _selection.ExpectedTotal.HasValue
                ? $"{Math.Round(_selection.ExpectedTotal.Value / 2m, 2):N0} ₫"
                : "50% tổng tiền";

    public string VoucherCode
    {
        get => _voucherCode;
        set
        {
            if (_voucherCode == value)
                return;

            _voucherCode = value;
            OnPropertyChanged();

            _selectedVoucherCode = "";
            VoucherMessage = "";

            OnPropertyChanged(nameof(DiscountLabel));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string VoucherMessage
    {
        get => _voucherMessage;
        private set
        {
            _voucherMessage = value;
            OnPropertyChanged();
        }
    }

    public string DiscountLabel =>
        _discountAmount > 0
            ? $"Đã giảm: {_discountAmount:N0} ₫"
            : !string.IsNullOrWhiteSpace(_selectedVoucherCode)
                ? "Mã sẽ được kiểm tra khi xác nhận đặt xe."
                : "Chưa áp dụng mã giảm giá.";

    public bool IsBusy
    {
        get => _busy;
        private set
        {
            _busy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanClose));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool CanClose => !IsBusy;

    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged();
        }
    }

    public BookingCreated? Result { get; private set; }

    public ICommand ConfirmCommand { get; }
    public ICommand ApplyVoucherCommand { get; }

    public event Action? Completed;

    public BookingCheckoutViewModel(
        BookingSelection selection,
        string vehicleName,
        CustomerBookingService? service = null)
    {
        _selection = selection;
        VehicleName = vehicleName;

        _service = service ?? new CustomerBookingService();

        ConfirmCommand = new RelayCommand(
            async _ => await ConfirmAsync(),
            _ => !IsBusy && Result == null
        );

        ApplyVoucherCommand = new RelayCommand(
            _ => SelectVoucher(),
            _ => !IsBusy && Result == null
        );
    }

    private void SelectVoucher()
    {
        string code = VoucherCode.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            VoucherMessage = "Vui lòng nhập mã Voucher.";
            return;
        }

        if (code.Length > 40)
        {
            VoucherMessage = "Mã Voucher tối đa 40 ký tự.";
            return;
        }

        _selectedVoucherCode = code;

        VoucherMessage =
            $"Đã ghi nhận mã {_selectedVoucherCode}. " +
            "Mã sẽ được kiểm tra sau khi tạo đơn.";

        OnPropertyChanged(nameof(DiscountLabel));
    }

    private async Task<BookingCreated> CreateOrRecoverAsync()
    {
        try
        {
            return await _service.CreateAsync(
                _selection,
                _requestKey
            );
        }
        catch (Exception createException)
        {
            BookingCreated? recovered;

            try
            {
                Status =
                    "Đang kiểm tra xem đơn đã được tạo hay chưa...";

                recovered =
                    await _service.FindByRequestKeyAsync(
                        _selection.CustomerId,
                        _requestKey
                    );
            }
            catch (Exception lookupException)
            {
                throw new InvalidOperationException(
                    "Không thể xác định đơn đã được tạo hay chưa. " +
                    "Vui lòng kiểm tra danh sách đơn thuê " +
                    "trước khi thực hiện yêu cầu mới.",
                    new AggregateException(
                        createException,
                        lookupException
                    )
                );
            }

            if (recovered != null)
                return recovered;

            // Khong tim thay don: giu nguyen loi tao don.
            System.Runtime.ExceptionServices
                .ExceptionDispatchInfo
                .Capture(createException)
                .Throw();

            throw new InvalidOperationException(
                "Không thể khôi phục kết quả tạo đơn.");
        }
    }

    public async Task ConfirmAsync()
    {
        if (IsBusy || Result != null)
            return;

        IsBusy = true;

        try
        {
            Status = "Đang kiểm tra lịch trống và tạo đơn...";

            Result = await CreateOrRecoverAsync();

            OnPropertyChanged(nameof(TotalLabel));
            OnPropertyChanged(nameof(DepositLabel));

            if (!string.IsNullOrWhiteSpace(_selectedVoucherCode))
            {
                try
                {
                    Status = "Đang áp dụng mã giảm giá...";

                    decimal discount =
                        await _voucherService.ApplyAsync(
                            Result.BookingId,
                            _selection.CustomerId,
                            _selectedVoucherCode
                        );

                    _discountAmount = discount;

                    using var db =
                        CRMS.Data.CrmsContextFactory.Create();

                    var updatedBooking =
                        await db.Bookings
                            .AsNoTracking()
                            .SingleAsync(
                                b => b.BookingId == Result.BookingId
                            );

                    Result = new BookingCreated(
                        updatedBooking.BookingId,
                        updatedBooking.BookingCode,
                        updatedBooking.TotalAmount,
                        updatedBooking.DepositAmount,
                        updatedBooking.DepositDeadline
                    );

                    VoucherMessage =
                        $"Áp dụng thành công! Giảm {discount:N0} ₫.";

                    OnPropertyChanged(nameof(DiscountLabel));
                    OnPropertyChanged(nameof(TotalLabel));
                    OnPropertyChanged(nameof(DepositLabel));
                }
                catch (SqlException ex)
                {
                    VoucherMessage = ex.Number switch
                    {
                        51100 => "Đơn không đủ điều kiện áp dụng Voucher.",
                        51101 => "Đơn đã có giao dịch thanh toán.",
                        51102 => "Đơn đã sử dụng Voucher.",
                        51103 => "Voucher không hợp lệ hoặc đã hết hạn.",
                        51104 => "Chưa đạt giá trị đơn hàng tối thiểu.",
                        51105 => "Voucher đã hết lượt sử dụng.",
                        51106 => "Voucher giảm vượt mức cho phép.",
                        51107 => "Voucher không áp dụng cho đơn thuê này.",
                        _ => "Không thể áp dụng Voucher: " + ex.Message
                    };
                }
                catch (Exception ex)
                {
                    VoucherMessage =
                        "Không thể xác nhận kết quả áp dụng Voucher: " +
                        ex.Message;
                }
            }

            Status =
                $"Đã tạo đơn {Result.BookingCode}. " +
                $"Tổng tiền {Result.TotalAmount:N0} ₫. " +
                $"Cọc {Result.DepositAmount:N0} ₫ trước " +
                $"{AvailabilityRules.ToVietnamLocal(Result.DepositDeadline):HH:mm dd/MM/yyyy}. " +
                VoucherMessage;

            Completed?.Invoke();
        }
        catch (SqlException ex) when (
            ex.Number is 51004 or 51005 or 51009)
        {
            Status =
                "Xe vừa có lịch thuê, bảo trì hoặc lịch khóa. " +
                "Vui lòng tìm lại xe.";
        }
        catch (SqlException ex) when (ex.Number == 51014)
        {
            Status =
                "Giá thuê vừa thay đổi. " +
                "Vui lòng tìm lại xe và xác nhận giá mới.";
        }
        catch (SqlException ex) when (
            ex.Number is 1205 or 51003)
        {
            Status =
                "Hệ thống đang bận. Bạn có thể thử xác nhận lại.";
        }
        catch (InvalidOperationException ex)
        {
            Status = ex.Message;
        }
        catch (Exception ex)
        {
            Status = "Chưa thể xác nhận đơn: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
