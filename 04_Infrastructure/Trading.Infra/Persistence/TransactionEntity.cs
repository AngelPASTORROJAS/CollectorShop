using System.Data;
using Collector.Shared.Infrastructure;

namespace Trading.Infra.Persistence;

/// <summary>
/// Entité de reçu transactionnel, liée de manière isomorphe au contrat d'audit SQL
/// </summary>
public record TransactionEntity
{
    public long TransactionId { get; init; }
    public long BuyerId { get; init; }
    public long SellerId { get; init; }
    public long ItemId { get; init; }
    public decimal GrossAmount { get; init; }
    public decimal CommissionAmount { get; init; }
    public CurrencyType Currency { get; init; }
    public TransactionStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }

    public TransactionEntity(DataRow row)
    {
        TransactionId = row.GetLong("transaction_id") ?? 0L;
        BuyerId = row.GetLong("buyer_id") ?? 0L;
        SellerId = row.GetLong("seller_id") ?? 0L;
        ItemId = row.GetLong("item_id") ?? 0L;

        GrossAmount = row.IsNull("gross_amount") ? 0.00m : Convert.ToDecimal(row["gross_amount"]);
        CommissionAmount = row.IsNull("commission_amount") ? 0.00m : Convert.ToDecimal(row["commission_amount"]);

        // Parsing sécurisé des Enums textuels provenant de PostgreSQL
        Currency = Enum.TryParse<CurrencyType>(row.GetString("currency"), out var parsedCurrency) ? parsedCurrency : CurrencyType.EUR;
        Status = Enum.TryParse<TransactionStatus>(row.GetString("status"), out var parsedStatus) ? parsedStatus : TransactionStatus.PENDING;

        CreatedAt = row.GetDateTime("created_at") ?? DateTime.UtcNow;
    }
}