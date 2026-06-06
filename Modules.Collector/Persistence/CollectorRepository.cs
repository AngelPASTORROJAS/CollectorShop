using Shared.Infrastructure.PostgreSql;

namespace Modules.Collector.Persistence;

public class CollectorRepository
{
    public async Task<CollectibleItemDto?> GetItemByIdAsync(Guid itemId)
    {
        return await PgSqlQuery.Collector("sp_get_collectible_item_by_id", [ new ("@p_item_id", itemId) ])
            .ExecuteAsSingleObjectAsync(row => new CollectibleItemDto(row));
    }
}