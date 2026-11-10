using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using CRMS.Models;
using CRMS.Services;
using CRMS.Services.Payments;
using CRMS.ViewModels;

internal static class StaffConfirmationChecks
{
    public static Task Run(Action<bool, string> check)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await RunOnUiThread(check); done.SetResult(); }
                catch (Exception ex) { done.SetException(ex); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            }));
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task;
    }

    private static async Task RunOnUiThread(Action<bool, string> check)
    {
        CurrentUserService.Instance.SetUser(new User { UserId = 456, UserRole = "Staff" });
        var row = new CashDepositItem
        {
            BookingId = 11, BookingCode = "TEST-CANCEL", CustomerName = "Fixture", IsDemo = true,
            DepositAmount = 2000000, ActualAmount = 1000, TransferContent = "DEMOFIXTURE", DepositDeadline = DateTime.Now.AddHours(1)
        };
        var service = new ReceiptFixture();
        bool failLoad = false;
        int reads = 0;
        async Task<List<CashDepositItem>> Load()
        {
            reads++;
            await Task.Yield();
            if (failLoad) throw new InvalidOperationException("Fixture disconnected");
            return service.Writes == 0 ? [row] : [];
        }
        var answer = MessageBoxResult.Cancel;
        CashDepositViewModel? vm = null;
        int dialogs = 0;
        vm = new CashDepositViewModel(true, deposits: new CashFixture(Load), demoPayments: service, confirmation: new DialogFixture(_ =>
        {
            dialogs++;
            check(vm!.IsLoading && !vm.CanConfirmDeposit && !vm.CanEdit, "Confirmation dialog blocks reload and duplicate submission");
            vm.LoadAsync().GetAwaiter().GetResult();
            vm.ConfirmAsync().GetAwaiter().GetResult();
            return answer == MessageBoxResult.Yes;
        }));
        var grid = new DataGrid { DataContext = vm, SelectionMode = DataGridSelectionMode.Single, CanUserAddRows = false };
        grid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(vm.Bookings)));
        grid.SetBinding(DataGrid.SelectedItemProperty, new Binding(nameof(vm.SelectedBooking)) { Mode = BindingMode.TwoWay });
        await vm.LoadAsync();
        vm.SelectedBooking = row;
        vm.BankTransactionReference = "BANK-FIXTURE";
        vm.HasVerifiedDemoReceipt = true;
        await Dispatcher.Yield(DispatcherPriority.DataBind);
        check(ReferenceEquals(grid.SelectedItem, row) && vm.CanConfirmDeposit, "WPF table selection enables the verified receipt form");
        foreach (var canceled in new[] { MessageBoxResult.Cancel, MessageBoxResult.No, MessageBoxResult.None })
        {
            answer = canceled;
            await vm.ConfirmAsync();
            await Dispatcher.Yield(DispatcherPriority.DataBind);
            check(service.Writes == 0 && reads == 1 && vm.Bookings.Cast<object>().Count() == 1 &&
                ReferenceEquals(vm.SelectedBooking, row) && ReferenceEquals(grid.SelectedItem, row) &&
                vm.BankTransactionReference == "BANK-FIXTURE" && vm.HasVerifiedDemoReceipt && vm.CanConfirmDeposit,
                $"{canceled}: keeps order, receipt and bank reference; performs no write or refresh");
        }
        check(dialogs == 3, "Reentrant confirmation never opens a second dialog");
        vm.SelectedBooking = row;
        check(vm.BankTransactionReference == "BANK-FIXTURE" && vm.HasVerifiedDemoReceipt, "Reselecting the same order keeps receipt evidence");
        await vm.LoadAsync();
        check(vm.SelectedBooking == row && vm.BankTransactionReference == "BANK-FIXTURE" && vm.CanConfirmDeposit, "Refresh restores the unchanged selected order and draft");
        failLoad = true;
        await vm.LoadAsync();
        check(vm.Bookings.Cast<object>().Count() == 1 && vm.SelectedBooking == row &&
            vm.BankTransactionReference == "BANK-FIXTURE" && !vm.CanConfirmDeposit, "Failed refresh retains rows and form but requires a fresh read before confirmation");
        failLoad = false;
        await vm.LoadAsync();
        answer = MessageBoxResult.Yes;
        service.Fail = true;
        await vm.ConfirmAsync();
        check(service.Writes == 0 && vm.SelectedBooking == row && vm.CanConfirmDeposit && vm.BankTransactionReference == "BANK-FIXTURE", "Rejected confirmation retains the pending order and evidence for retry");
        service.Fail = false;
        await vm.ConfirmAsync();
        check(service.Writes == 1 && vm.Bookings.Cast<object>().Count() == 0 && vm.HasLastConfirmation &&
            vm.LastConfirmation.Contains("BANK-FIXTURE") && vm.LastConfirmation.Contains("TEST-CANCEL"), "Success removes the pending row and retains a visible receipt");

        var expiredCash = new CashDepositItem { BookingId = 90, BookingCode = "QA-EXPIRED-CASH", DepositDeadline = DateTime.Now.AddDays(-1), DepositMethod = "Cash" };
        var oldVnpay = new CashDepositItem { BookingId = 91, BookingCode = "QA-VNPAY", DepositDeadline = DateTime.Now.AddHours(1), DepositMethod = "VNPay" };
        int unavailableDialogs = 0;
        var historyVm = new CashDepositViewModel(true, deposits: new CashFixture(() => Task.FromResult(new List<CashDepositItem>()),
            () => Task.FromResult(new List<CashDepositItem> { expiredCash, oldVnpay })),
            demoPayments: service, confirmation: new DialogFixture(_ => { unavailableDialogs++; return true; }));
        await historyVm.LoadAsync();
        historyVm.IncludeUnavailable = true;
        check(historyVm.Bookings.Cast<object>().Count() == 2, "Staff can reveal expired cash and legacy VNPay orders without recreating sample bookings");
        foreach (var unavailable in new[] { expiredCash, oldVnpay })
        {
            historyVm.SelectedBooking = unavailable;
            await historyVm.ConfirmAsync();
            check(!historyVm.CanConfirmDeposit && unavailable.Explanation.Length > 0 && unavailable.ActualAmountLabel == "—" && unavailableDialogs == 0,
                $"{unavailable.DepositMethod}: read-only archived order explains why confirmation is unavailable");
        }
        historyVm.IncludeUnavailable = false;
        check(historyVm.Bookings.Cast<object>().Count() == 0 && historyVm.SelectedBooking == null, "Turning off archived orders returns to actionable pending deposits");
        var crossingDeadline = new CashDepositItem { BookingId = 92, DepositMethod = "Cash", DepositDeadline = DateTime.Now.AddMinutes(1) };
        var clockVm = new CashDepositViewModel(true, deposits: new CashFixture(() => Task.FromResult(new List<CashDepositItem> { crossingDeadline })));
        await clockVm.LoadAsync(); clockVm.SelectedBooking = crossingDeadline;
        check(clockVm.CanConfirmDeposit, "Unexpired cash deposit remains actionable");
        crossingDeadline.DepositDeadline = DateTime.Now.AddSeconds(-1);
        clockVm.RefreshTimeSensitiveState();
        check(!clockVm.CanConfirmDeposit && crossingDeadline.StatusText == "Hết hạn đặt cọc", "An open Staff page disables cash confirmation as soon as the deadline passes");

        var ledger = new PaymentFixture();
        var cashRow = new CashDepositItem { BookingId = 12, BookingCode = "CASH-INTERFACE", DepositAmount = 2000000, DepositDeadline = DateTime.Now.AddHours(1) };
        var cashVm = new CashDepositViewModel(false,
            deposits: new CashFixture(() => Task.FromResult(ledger.CashWrites == 0 ? new List<CashDepositItem> { cashRow } : new List<CashDepositItem>())),
            payments: ledger, confirmation: new DialogFixture(_ => true));
        await cashVm.LoadAsync(); cashVm.SelectedBooking = cashRow; await cashVm.ConfirmAsync();
        check(ledger.CashWrites == 1 && cashVm.HasLastConfirmation && cashVm.Bookings.Cast<object>().Count() == 0,
            "Cash deposit calls the injected payment interface and retains its receipt without a database dependency");

        var approve = false;
        var balanceVm = new PaymentWorkspaceViewModel("Balance", new QueryFixture(), new DialogFixture(_ => approve), ledger);
        balanceVm.BookingLookup = "PAY-INTERFACE";
        await ((CRMS.Helpers.AsyncRelayCommand)balanceVm.CalculateBalanceCommand).ExecuteAsync();
        check(balanceVm.CanCollect && ledger.SummaryReads == 1, "Payment workspace reads balance through its injected service interface");
        await ((CRMS.Helpers.AsyncRelayCommand)balanceVm.RecordFinalPaymentCommand).ExecuteAsync();
        check(ledger.FinalWrites == 0 && balanceVm.CanCollect, "Cancelling the injected confirmation does not record a final payment");
        approve = true;
        await ((CRMS.Helpers.AsyncRelayCommand)balanceVm.RecordFinalPaymentCommand).ExecuteAsync();
        check(ledger.FinalWrites == 1 && balanceVm.Summary?.Status == "Completed" && !balanceVm.CanCollect,
            "Confirmed final collection delegates to the payment interface and reloads the completed summary");
        ledger.Summary = ledger.Summary with { Status = "Cancelled", DepositsPaid = 1000, BalancesPaid = 0 };
        var refundVm = new PaymentWorkspaceViewModel("Refund", new QueryFixture(), new DialogFixture(_ => true), ledger)
        { BookingLookup = "PAY-INTERFACE", RefundAmount = "1000", ApprovalReference = "APPROVAL-TEST" };
        await ((CRMS.Helpers.AsyncRelayCommand)refundVm.CalculateBalanceCommand).ExecuteAsync();
        await ((CRMS.Helpers.AsyncRelayCommand)refundVm.RecordManualRefundCommand).ExecuteAsync();
        check(ledger.RefundWrites == 1 && refundVm.Summary?.NetPaid == 0 && !refundVm.CanRefund,
            "Manual refund uses the same injected interface and reloads the reconciled balance");

        CurrentUserService.Instance.SetUser(new User { UserId = 123, UserRole = "Customer" });
        using var customer = new DemoPaymentViewModel(11, service, true);
        var title = new TextBlock { DataContext = customer };
        title.SetBinding(TextBlock.TextProperty, new Binding(nameof(customer.StatusTitle)));
        int notifications = 0;
        string? parentStatus = null;
        customer.SessionUpdated += s => { notifications++; parentStatus = s.BookingStatus; };
        await customer.RefreshAsync();
        await Dispatcher.Yield(DispatcherPriority.DataBind);
        check(title.Text.Contains("Chờ Staff") && parentStatus == "PendingDeposit", "Customer bound UI and parent initially show pending state");
        service.Session = service.Session with { PaymentStatus = "Success", ActualReceived = 1000, ConfirmedBooking = true, BookingStatus = "Confirmed" };
        await customer.RefreshAsync();
        await Dispatcher.Yield(DispatcherPriority.DataBind);
        check(title.Text == "Thanh toán demo thành công" && parentStatus == "Confirmed" && notifications == 2 && !customer.CanShowQr,
            "Status check updates the open customer UI and parent without closing the window");
        await customer.RefreshAsync();
        check(notifications == 2, "Unchanged polling does not refresh the parent repeatedly");
        var heldPoll = new TaskCompletionSource<DemoPaymentSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.NextRead = heldPoll.Task;
        var beforeReads = service.Reads;
        var polling = customer.RefreshAsync();
        var checking = customer.RefreshAsync(waitForCurrent: true);
        check(!checking.IsCompleted && service.Reads == beforeReads + 1, "Manual status check waits for an active poll instead of silently skipping the click");
        heldPoll.SetResult(service.Session with { PaymentStatus = "Pending", ActualReceived = 0, ConfirmedBooking = false, BookingStatus = "PendingDeposit" });
        await polling; await checking;
        await Dispatcher.Yield(DispatcherPriority.DataBind);
        check(service.Reads == beforeReads + 2 && customer.LastRefreshSucceeded && customer.StatusCheckResult.Contains("Đặt cọc demo thành công") &&
            title.Text == "Thanh toán demo thành công" && customer.LastCheckedLabel.Contains("Đã kiểm tra lúc"),
            "After an old pending poll, the manual click performs its own fresh read and reports confirmed success");
        service.FailRead = true;
        await customer.RefreshAsync(waitForCurrent: true);
        check(!customer.LastRefreshSucceeded && customer.StatusCheckResult.Contains("Chưa thể cập nhật") &&
            !customer.StatusCheckResult.Contains("Đặt cọc demo thành công"), "A failed manual read never reports a cached success as a newly checked payment");
        service.FailRead = false;
        PaymentStatusNotice? notice = null;
        var notices = 0;
        customer.StatusCheckCompleted += n => { notice = n; notices++; };
        var pendingClick = new TaskCompletionSource<DemoPaymentSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.NextRead = pendingClick.Task;
        var checkTask = customer.CheckStatusAsync();
        check(customer.IsChecking && !customer.CanCheckStatus && customer.CheckStatusButtonText == "Đang kiểm tra…",
            "Manual status command exposes its busy state and button label through ViewModel properties");
        var manualReads = service.Reads;
        await customer.CheckStatusAsync(); await customer.RefreshAsync();
        check(service.Reads == manualReads && notices == 0, "Repeated clicks and automatic polling cannot start another read while a manual check is pending");
        pendingClick.SetResult(service.Session); await checkTask;
        check(!customer.IsChecking && customer.CanCheckStatus && customer.CheckStatusButtonText == "Kiểm tra trạng thái" &&
            notice is { Title: "Đặt cọc demo thành công", IsError: false } && notices == 1,
            "Successful manual command restores bound button state and emits one UI-independent success notice");
        service.FailRead = true;
        await customer.CheckStatusAsync();
        check(customer.CanCheckStatus && !customer.IsChecking && notice is { IsError: true } && notices == 2,
            "Failed manual command re-enables the button and emits an error notice without WPF types");
        service.FailRead = false;
        service.Session = service.Session with { PaymentStatus = "Pending", ActualReceived = 0, ConfirmedBooking = false, BookingStatus = "PendingDeposit", Deadline = DateTime.UtcNow.AddSeconds(-1) };
        await customer.RefreshAsync();
        await Dispatcher.Yield(DispatcherPriority.DataBind);
        check(title.Text == "Hết hạn đặt cọc" && customer.BookingStatusLabel == "Đơn thuê: Hết hạn đặt cọc" && !customer.CanShowQr,
            "Expired QR hold cannot still show waiting for deposit even if database status remains PendingDeposit");
        var beforeClose = notifications;
        var noticesBeforeClose = notices;
        var closingRead = new TaskCompletionSource<DemoPaymentSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.NextRead = closingRead.Task;
        var closingCheck = customer.CheckStatusAsync();
        customer.Dispose();
        closingRead.SetResult(service.Session);
        await closingCheck;
        check(notices == noticesBeforeClose && !customer.CanCheckStatus && !customer.IsChecking,
            "Closing during a manual check cancels the UI result and leaves the command disabled");
        await customer.RefreshAsync();
        check(notifications == beforeClose, "Closed payment window stops updating");
        CurrentUserService.Instance.SetUser(new User { UserId = 999, UserRole = "Staff" });
        failLoad = true;
        await vm.LoadAsync();
        check(!vm.HasLastConfirmation && vm.SelectedBooking == null && vm.Bookings.Cast<object>().Count() == 0,
            "Switching users clears the previous Staff receipt even when loading fails");
    }

    private sealed class DialogFixture(Func<string, bool> answer) : IConfirmationService
    {
        public bool Confirm(string message, string title) => answer(message);
    }
    private sealed class QueryFixture : IBookingQueryService
    {
        public Task<List<CRMS.Dtos.CustomerTrip>> GetMyBookingsAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task<long> ResolveBookingAsync(string lookup, CancellationToken token = default) => Task.FromResult(12L);
    }
    private sealed class PaymentFixture : IPaymentService
    {
        public int CashWrites, FinalWrites, RefundWrites, SummaryReads;
        public PaymentSummary Summary = new(12, "PAY-INTERFACE", "Returned", 4000000, 0, 2000000, 0, 2000000, 0, 0, true);
        public Task<PaymentSummary> GetPaymentSummary(long id) { SummaryReads++; return Task.FromResult(Summary); }
        public Task<decimal> GetOutstandingBalance(long id) => Task.FromResult(Summary.OutstandingBalance);
        public Task<PaymentReceipt> ConfirmCashDeposit(long id, decimal amount, Guid key) { CashWrites++; return Task.FromResult(new PaymentReceipt(1, false, false)); }
        public Task<PaymentReceipt> RecordFinalPayment(long id, decimal amount, Guid key)
        {
            FinalWrites++; Summary = Summary with { BalancesPaid = amount, Status = "Completed" };
            return Task.FromResult(new PaymentReceipt(2, false, false));
        }
        public Task CompletePaidBooking(long id) { Summary = Summary with { Status = "Completed" }; return Task.CompletedTask; }
        public Task<PaymentReceipt> RecordManualRefund(long id, decimal amount, string method, string reference, Guid key)
        {
            RefundWrites++; Summary = Summary with { RefundsPaid = amount };
            return Task.FromResult(new PaymentReceipt(3, false, false));
        }
    }
    private sealed class CashFixture(Func<Task<List<CashDepositItem>>> pending,
        Func<Task<List<CashDepositItem>>>? all = null) : ICashDepositService
    {
        public Task<List<CashDepositItem>> GetPendingDepositsAsync() => pending();
        public Task<List<CashDepositItem>> GetDepositsAsync(bool includeUnavailable) => includeUnavailable && all != null ? all() : pending();
    }
    private sealed class ReceiptFixture : IDemoPaymentService
    {
        public int Writes;
        public bool Fail;
        public bool FailRead;
        public int Reads;
        public Task<DemoPaymentSession>? NextRead;
        public DemoPaymentSession Session = new(11, "TEST-CANCEL", "PendingDeposit", 4000000, 2000000, 1000, 0,
            "DEMOFIXTURE", "970436", "TEST BANK", "0000000001", "TEST RECEIVER", "Pending", false, DateTime.UtcNow.AddHours(1), null);
        public Task<DemoPaymentSession> GetSessionAsync(long id, CancellationToken ct = default)
        {
            Reads++;
            if (NextRead is { } next) { NextRead = null; return next; }
            return FailRead ? Task.FromException<DemoPaymentSession>(new InvalidOperationException("Fixture connection error")) : Task.FromResult(Session);
        }
        public async Task<DemoPaymentReceipt> ConfirmReceivedAsync(long id, decimal amount, string reference, Guid key)
        {
            await Task.Yield();
            if (Fail) throw new InvalidOperationException("Fixture rejected");
            Writes++;
            return new(1, false, false);
        }
    }
}
