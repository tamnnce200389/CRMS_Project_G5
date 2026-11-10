namespace CRMS.Services;

public interface ICashDepositService
{
    Task<List<CashDepositItem>> GetPendingDepositsAsync();
    Task<List<CashDepositItem>> GetDepositsAsync(bool includeUnavailable);
}
