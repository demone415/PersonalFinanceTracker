namespace FinanceTracker.Domain.Enums;

public enum ReceiptFetchStatus
{
    Pending = 0,
    Fetched = 1,
    Failed = 2,
    RetryLimit = 3,
}
