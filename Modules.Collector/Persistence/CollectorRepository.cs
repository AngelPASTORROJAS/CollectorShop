using Shared.Infrastructure.PostgreSql;
using Npgsql;

namespace Modules.Collector.Persistence;

public class CollectorRepository
{
    public async Task<List<CollectibleItemDto>> GetAllItemsAsync()
    {
        using var query = PgSqlQuery.Collector("api_load_catalog_ram");
        return await query.ExecuteAsListAsync(row => new CollectibleItemDto(row));
    }

    public async Task<long> CreateItemAsync(ItemCreateDto dto, long ownerId)
    {
        var parameters = new List<NpgsqlParameter>
        {
            new("p_category_code", dto.CategoryCode),
            new("p_owner_id", ownerId),
            new("p_title", dto.Title),
            new("p_description", dto.Description),
            new("p_price", dto.Price),
            new("p_metadata_json", dto.MetadataJson)
        };

        using var query = PgSqlQuery.Collector("api_create_item", parameters);
        var dt = await query.ExecuteAsDataTableAsync();
        
        if (dt != null && dt.Rows.Count > 0)
        {
            return (long)dt.Rows[0][0];
        }
        return -1;
    }
}