using Npgsql;
using Collector.Shared.Infrastructure;

namespace Collectors.Infra.Persistence;

public class SqlCollectorRepository
{
    // Centralisation du nom de la procédure stockée spécifique au catalogue
    private const string SpGetCollectibleItemById = "sp_get_collectible_item_by_id";

    public CollectibleItemDto? GetItemById(Guid itemId)
    {
        // 1. On pointe cette fois-ci sur "DbCollector" (qui résout le port 5433 en tâche de fond)
        var query = PgSqlQuery.Collector(SpGetCollectibleItemById, [ new ("@p_item_id", itemId) ]);

        // 2. Récupération des données du catalogue
        var table = query.ExecuteAsDataTable();

        // 3. Notre Guard Clause traditionnelle et explicite (Zéro magie)
        if (!(table != null && table.Rows.Count > 0))
            return null;

        // 4. Mapping instantané via le constructeur dédié
        return new CollectibleItemDto(table.Rows[0]);
    }
}