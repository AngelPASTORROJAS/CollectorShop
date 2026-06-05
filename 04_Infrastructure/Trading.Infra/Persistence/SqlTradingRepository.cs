using Npgsql;
using System.Data;
using Collector.Shared.Infrastructure;

namespace Trading.Infra.Persistence;

public class SqlTradingRepository
{
    // Centralisation des noms des procédures stockées de DbFinance
    private const string SpProcessTrade = "sp_process_trade";
    private const string SpGetTransactionWithLedger = "sp_get_transaction_with_ledger";

    /// <summary>
    /// Exécute de manière atomique un achat, prélève la commission et alimente le Ledger.
    /// </summary>
    public long ProcessTrade(long buyerId, long sellerId, long itemId, decimal grossAmount, CurrencyType currency = CurrencyType.EUR)
    {
        // 1. Utilisation du nouveau ExecuteNonQuery car c'est une commande d'écriture
        var query = new PgSqlQuery(PgDbConnectionFactory.DbFinance, SpProcessTrade)
        {
            Parameters = [
                new NpgsqlParameter("@p_buyer_id", buyerId),
                new NpgsqlParameter("@p_seller_id", sellerId),
                new NpgsqlParameter("@p_item_id", itemId),
                new NpgsqlParameter("@p_gross_amount", grossAmount),
                new NpgsqlParameter("@p_currency", currency.ToString()) // Passage de l'enum en string pour l'encodage PG
            ]
        };

        // 2. Appel de ton enveloppe performante (Zéro allocation de DataTable)
        // Note : Si ton PgSqlQuery retourne le scalaire de la fonction, utilise-le, sinon passe par ExecuteAsDataTable.
        // Ici on suppose que le ExecuteNonQuery retourne le nombre de lignes, alors on va plutôt lire l'ID généré via DataTable :
        var table = query.ExecuteAsDataTable();

        if (table != null && table.Rows.Count > 0)
        {
            return Convert.ToInt64(table.Rows[0][0]);
        }

        throw new InvalidOperationException("Échec de l'enregistrement de la transaction financière.");
    }

    /// <summary>
    /// Récupère une transaction et l'ensemble de ses lignes comptables associées pour l'audit et la BI.
    /// </summary>
    public List<TransactionLedgerEntity> GetTransactionWithLedger(long transactionId)
    {
        var list = new List<TransactionLedgerEntity>();

        var query = new PgSqlQuery(PgDbConnectionFactory.DbFinance, SpGetTransactionWithLedger)
        {
            Parameters = [
                new NpgsqlParameter("@p_transaction_id", transactionId)
            ]
        };

        var table = query.ExecuteAsDataTable();

        if (table == null || table.Rows.Count == 0)
            return list;

        foreach (DataRow row in table.Rows)
        {
            list.Add(new TransactionLedgerEntity(row));
        }

        return list;
    }

    /// <summary>
    /// Récupère l'historique léger des transactions d'un utilisateur
    /// </summary>
    public List<TransactionEntity> GetTransactionHistoryByUserId(long userId)
    {
        var list = new List<TransactionEntity>();

        var query = new PgSqlQuery(PgDbConnectionFactory.DbFinance, "sp_get_user_transactions_history")
        {
            Parameters = [
                new NpgsqlParameter("@p_user_id", userId)
            ]
        };

        var table = query.ExecuteAsDataTable();

        if (table == null || table.Rows.Count == 0)
            return list;

        foreach (DataRow row in table.Rows)
        {
            list.Add(new TransactionEntity(row));
        }

        return list;
    }

    /// <summary>
    /// Récupère le détail d'une transaction remappé sous forme d'objet structuré Parent-Enfants pour les écrans de détails.
    /// </summary>
    public TransactionDetailsEntity? GetTransactionDetails(long transactionId)
    {
        var query = new PgSqlQuery(PgDbConnectionFactory.DbFinance, SpGetTransactionWithLedger)
        {
            Parameters = [
                new NpgsqlParameter("@p_transaction_id", transactionId)
            ]
        };

        var table = query.ExecuteAsDataTable();

        if (table == null || table.Rows.Count == 0) return null;

        var details = new TransactionDetailsEntity
        {
            Transaction = new TransactionEntity(table.Rows[0])
        };

        foreach (DataRow row in table.Rows)
        {
            if (row.IsNull("ledger_id")) continue;

            var accountType = Enum.TryParse<LedgerAccountType>(row.GetString("account_type"), out var parsedAccount)
                ? parsedAccount
                : LedgerAccountType.BUYER_HOLD;

            details.LedgerLines.Add(new LedgerLineDetails
            {
                LedgerId = row.GetLong("ledger_id") ?? 0L,
                AccountType = accountType,
                DebitAmount = row.IsNull("debit_amount") ? 0.00m : Convert.ToDecimal(row["debit_amount"]),
                CreditAmount = row.IsNull("credit_amount") ? 0.00m : Convert.ToDecimal(row["credit_amount"]),
                Description = row.GetString("ledger_description") ?? "",
                RecordedAt = row.GetDateTime("recorded_at") ?? DateTime.UtcNow
            });
        }

        return details;
    }
}