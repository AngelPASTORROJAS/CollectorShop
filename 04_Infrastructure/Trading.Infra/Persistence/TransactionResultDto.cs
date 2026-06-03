using System.Data;
using Collector.Shared.Infrastructure;

namespace Trading.Infra.Persistence;

/// <summary>
/// DTO de reçu transactionnel, lié de manière isomorphe au contrat d'audit SQL
/// </summary>
public record TransactionResultDto
{
    public Guid TransactionId { get; init; }
    public string Status { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal CommissionCollected { get; init; }
    public DateTime CreatedAt { get; init; }

    public TransactionResultDto(DataRow row)
    {
        TransactionId = row.GetGuid("transaction_id") ?? Guid.Empty;
        Status = row.GetString("status") ?? "PENDING";
        TotalAmount = row.IsNull("total_amount") ? 0.00m : Convert.ToDecimal(row["total_amount"]);
        CommissionCollected = row.IsNull("commission_collected") ? 0.00m : Convert.ToDecimal(row["commission_collected"]);
        CreatedAt = row.GetDateTime("created_at") ?? DateTime.UtcNow;
    }
}