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

    public async Task<CollectibleItemDto?> GetItemByIdAsync(long id)
    {
        var parameters = new List<NpgsqlParameter> { new("p_item_id", id) };
        using var query = PgSqlQuery.Collector("api_get_item_by_id", parameters);

        return await query.ExecuteAsSingleObjectAsync(row => new CollectibleItemDto(row));
    }

    public async Task<ItemOwnerInfo?> GetItemOwnerAsync(long id)
    {
        var parameters = new List<NpgsqlParameter> { new("p_item_id", id) };
        using var query = PgSqlQuery.Collector("api_get_item_owner", parameters);

        return await query.ExecuteAsSingleObjectAsync(row => new ItemOwnerInfo(Convert.ToInt64(row["owner_id"])));
    }

    public async Task<bool> DeleteItemAsync(long id, long deletedByUserId)
    {
        var parameters = new List<NpgsqlParameter>
        {
            new("p_item_id", id),
            new("p_deleted_by_id", deletedByUserId)
        };

        using var query = PgSqlQuery.Collector("api_soft_delete_item", parameters);
        var dt = await query.ExecuteAsDataTableAsync();

        if (dt != null && dt.Rows.Count > 0)
        {
            return Convert.ToBoolean(dt.Rows[0][0]);
        }
        return false;
    }
}