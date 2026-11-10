
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CRMS.Helpers;
using CRMS.Services;
using CRMS.Services.Vehicles;
using CRMS.Services.Payments;
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
    private bool _attempted;
    private bool _completed;
    private bool _voucherApplied;
    private bool _voucherResultUncertain;

    private string _depositMethod = "Cash";

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

    public bool DemoPaymentsEnabled { get; }

    public bool IsDemoSelection => DepositMethod == "Demo";

    public string DepositCaption =>
        IsDemoSelection
            ? "Cọc mô phỏng · 50% tiền thuê"
            : "Tiền cọc 50%";

    public bool IsBusy
    {
        get => _busy;
        private set
        {
            _busy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanClose));
            OnPropertyChanged(nameof(CanChooseDepositMethod));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool CanClose => !IsBusy;

    public bool CanChooseDepositMethod =>
        !IsBusy && !_attempted && Result == null;

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

    public string DepositMethod
    {
        get => _depositMethod;
        set
        {
            if (!CanChooseDepositMethod ||
                Result != null ||
                value is not ("Cash" or "Demo") ||
                (value == "Demo" && !DemoPaymentsEnabled))
            {
                return;
            }

            if (_depositMethod == value)
                return;

            _depositMethod = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDemoSelection));
            OnPropertyChanged(nameof(DepositCaption));

            Status = value == "Demo"
                ? "Chế độ demo: chỉ chuyển khoản thử nghiệm 1.000đ. " +
                  "Tiền cọc hiển thị là mô phỏng; " +
                  "Staff xác nhận sau khi kiểm tra tiền về."
                : "Đặt cọc tiền mặt; đơn chờ nhân viên xác nhận đã nhận cọc.";
        }
    }

    public string VoucherCode
    {
        get => _voucherCode;
        set
        {
            if (_voucherCode == value)
                return;

            _voucherCode = value ?? "";
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

    public ICommand ConfirmCommand { get; }
    public ICommand ApplyVoucherCommand { get; }

    public event Action? Completed;

    public BookingCheckoutViewModel(
        BookingSelection selection,
        string vehicleName,
        CustomerBookingService? service = null,
        DemoPaymentOptions? demoOptions = null)
    {
        _selection = selection
            ?? throw new ArgumentNullException(nameof(selection));

        VehicleName = vehicleName;
        _service = service ?? new CustomerBookingService();

        DemoPaymentsEnabled =
            (demoOptions ?? DemoPaymentOptions.Load()).Enabled;

        if (DemoPaymentsEnabled)
            DepositMethod = "Demo";

        ConfirmCommand = new RelayCommand(
            async _ => await ConfirmAsync(),
            _ => !IsBusy && !_completed && !_voucherResultUncertain);

        ApplyVoucherCommand = new RelayCommand(
            _ => SelectVoucher(),
            _ => !IsBusy &&
                 !_completed &&
                 !_voucherApplied &&
                 !_voucherResultUncertain);
    }

    private void SelectVoucher()
    {
        string code = VoucherCode.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            _selectedVoucherCode = "";
            VoucherMessage = "Vui lòng nhập mã Voucher.";
            OnPropertyChanged(nameof(DiscountLabel));
            return;
        }

        if (code.Length > 40)
        {
            _selectedVoucherCode = "";
            VoucherMessage = "Mã Voucher tối đa 40 ký tự.";
            OnPropertyChanged(nameof(DiscountLabel));
            return;
        }

        _selectedVoucherCode = code;

        VoucherMessage =
            $"Đã ghi nhận mã {_selectedVoucherCode}. " +
            "Mã sẽ được kiểm tra khi xác nhận đặt xe.";

        OnPropertyChanged(nameof(DiscountLabel));
    }

    private async Task<BookingCreated> CreateOrRecoverAsync()
    {
        try
        {
            return await _service.CreateAsync(
                _selection with
                {
                    DepositMethod = "Cash",
                    IsDemo = IsDemoSelection
                },
                _requestKey);
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
                        _requestKey);
            }
            catch (Exception lookupException)
            {
                throw new InvalidOperationException(
                    "Không thể xác định đơn đã được tạo hay chưa. " +
                    "Vui lòng kiểm tra danh sách đơn thuê " +
                    "trước khi thực hiện yêu cầu mới.",
                    new AggregateException(
                        createException,
                        lookupException));
            }

            if (recovered != null)
                return recovered;

            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(createException)
                .Throw();

            throw new InvalidOperationException(
                "Không thể khôi phục kết quả tạo đơn.");
        }
    }

    private async Task RefreshBookingAsync()
    {
        if (Result == null)
            return;

        using var db =
            CRMS.Data.CrmsContextFactory.Create();

        var updated = await db.Bookings
            .AsNoTracking()
            .SingleAsync(
                b => b.BookingId == Result.BookingId);

        Result = new BookingCreated(
            updated.BookingId,
            updated.BookingCode,
            updated.TotalAmount,
            updated.DepositAmount,
            updated.DepositDeadline,
            updated.IsDemo);

        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(TotalLabel));
        OnPropertyChanged(nameof(DepositLabel));
    }

    private static string VoucherError(SqlException ex)
    {
        return ex.Number switch
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

    private async Task<bool> ApplySelectedVoucherAsync()
    {
        if (Result == null ||
            string.IsNullOrWhiteSpace(_selectedVoucherCode))
        {
            return true;
        }

        try
        {
            Status = "Đang áp dụng mã giảm giá...";

            decimal discount =
                await _voucherService.ApplyAsync(
                    Result.BookingId,
                    _selection.CustomerId,
                    _selectedVoucherCode);

            _discountAmount = discount;
            _voucherApplied = true;

            VoucherMessage =
                $"Áp dụng thành công! Giảm {discount:N0} ₫.";

            await RefreshBookingAsync();

            OnPropertyChanged(nameof(DiscountLabel));
            return true;
        }
        catch (SqlException ex) when (
            ex.Number is >= 51100 and <= 51107)
        {
            VoucherMessage = VoucherError(ex);

            Status =
                "Đơn đã được tạo nhưng Voucher chưa được áp dụng. " +
                "Bạn có thể nhập mã khác và thử lại, " +
                "hoặc xóa mã để tiếp tục với giá gốc.";

            return false;
        }
        catch (Exception ex)
        {
            _voucherResultUncertain = true;

            VoucherMessage =
                "Chưa xác định được Voucher đã áp dụng hay chưa: " +
                ex.Message;

            Status =
                "Không thể xác nhận kết quả Voucher. " +
                "Vui lòng kiểm tra đơn thuê trước khi thử lại, " +
                "để tránh áp dụng mã nhiều lần.";

            CommandManager.InvalidateRequerySuggested();
            return false;
        }
    }

    public async Task ConfirmAsync()
    {
        if (IsBusy || _completed || _voucherResultUncertain)
            return;

        IsBusy = true;

        try
        {
            if (Result == null)
            {
                _attempted = true;
                OnPropertyChanged(nameof(CanChooseDepositMethod));

                Status = "Đang kiểm tra lịch trống và tạo đơn...";

                Result = await CreateOrRecoverAsync();

                OnPropertyChanged(nameof(Result));
                OnPropertyChanged(nameof(TotalLabel));
                OnPropertyChanged(nameof(DepositLabel));
            }

            if (!_voucherApplied &&
                !string.IsNullOrWhiteSpace(_selectedVoucherCode))
            {
                bool applied = await ApplySelectedVoucherAsync();

                if (!applied)
                    return;
            }

            if (Result == null)
                return;

            if (Result.IsDemo)
            {
                Status =
                    $"Đã tạo đơn demo {Result.BookingCode}. " +
                    $"Cọc mô phỏng {Result.DepositAmount:N0} ₫; " +
                    "chỉ chuyển khoản thử nghiệm 1.000đ " +
                    "và chờ Staff xác nhận.";
            }
            else
            {
                Status =
                    $"Đã tạo đơn {Result.BookingCode}. " +
                    $"Tổng tiền {Result.TotalAmount:N0} ₫. " +
                    $"Cọc {Result.DepositAmount:N0} ₫ trước " +
                    $"{AvailabilityRules.ToVietnamLocal(Result.DepositDeadline):HH:mm dd/MM/yyyy}.";
            }

            _completed = true;
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
