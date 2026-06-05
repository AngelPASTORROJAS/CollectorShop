namespace Trading.Infra.Persistence;

public enum TransactionStatus
{
    PENDING,
    COMPLETED,
    REFUNDED
}

public enum LedgerAccountType
{
    SYSTEM_REVENUE,
    SELLER_PAYOUT,
    BUYER_HOLD
}

public enum CurrencyType
{
    EUR,
    USD,
    GBP,
    CHF
}
