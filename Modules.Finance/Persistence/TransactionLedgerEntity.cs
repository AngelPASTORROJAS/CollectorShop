using System.Data;
using Shared.Kernel;
using Shared.Infrastructure.PostgreSql;

namespace Modules.Finance.Persistence;

public record TransactionLedgerEntity
{
    public TransactionEntity Transaction { get; init; }

    // Propriétés spécifiques à la ligne du Grand Livre (Ledger)
    public long? LedgerId { get; init; }
    public LedgerAccountType? AccountType { get; init; }
    public decimal DebitAmount { get; init; }
    public decimal CreditAmount { get; init; }
    public string LedgerDescription { get; init; }
    public DateTime? RecordedAt { get; init; }

    public TransactionLedgerEntity(DataRow row)
    {
        // On réutilise l'entité de base pour la partie commune
        Transaction = new TransactionEntity(row);

        LedgerId = row.GetLong("ledger_id");
        DebitAmount = row.IsNull("debit_amount") ? 0.00m : Convert.ToDecimal(row["debit_amount"]);
        CreditAmount = row.IsNull("credit_amount") ? 0.00m : Convert.ToDecimal(row["credit_amount"]);
        LedgerDescription = row.GetString("ledger_description") ?? "";
        RecordedAt = row.GetDateTime("recorded_at");

        if (Enum.TryParse<LedgerAccountType>(row.GetString("account_type"), out var parsedAccount))
        {
            AccountType = parsedAccount;
        }
    }
}