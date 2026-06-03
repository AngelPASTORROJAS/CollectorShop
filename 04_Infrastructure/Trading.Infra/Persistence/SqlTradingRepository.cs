using Npgsql;
using Collector.Shared.Infrastructure;

namespace Trading.Infra.Persistence;

public class SqlTradingRepository
{
    // Centralisation du contrat de la procédure stockée financière
    private const string SpCreateTransaction = "sp_create_transaction";

    /// <summary>
    /// Enregistre une transaction financière dans la base d'audit et applique la commission.
    /// </summary>
    public TransactionResultDto? CreateTransaction(Guid buyerId, Guid sellerId, Guid collectibleItemId, decimal rawPrice)
    {
        // 1. Calcul de la commission de 5% (Logique métier portée ou validée ici au niveau de l'appel)
        decimal commissionAmount = rawPrice * 0.05m;
        decimal finalPayout = rawPrice - commissionAmount;

        // 2. Préparation de la requête indexée sur "DbFinance" (Port 5434)
        var query = new PgSqlQuery(PgDbConnectionFactory.DbFinance, SpCreateTransaction)
        {
            Parameters = [
                new NpgsqlParameter("@p_buyer_id", buyerId),
                new NpgsqlParameter("@p_seller_id", sellerId),
                new NpgsqlParameter("@p_item_id", collectibleItemId),
                new NpgsqlParameter("@p_raw_price", rawPrice),
                new NpgsqlParameter("@p_commission", commissionAmount),
                new NpgsqlParameter("@p_payout", finalPayout)
            ]
        };

        // 3. Exécution
        var table = query.ExecuteAsDataTable();

        // 4. Guard Clause traditionnelle (Zéro magie)
        if (!(table != null && table.Rows.Count > 0))
            return null;

        // 5. Retour du reçu fiscal/financier généré par PostgreSQL
        return new TransactionResultDto(table.Rows[0]);
    }
}