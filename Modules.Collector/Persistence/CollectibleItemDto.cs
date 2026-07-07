using Shared.Infrastructure.PostgreSql;
using System.Data;

namespace Modules.Collector.Persistence;

public record CollectibleItemDto
{
    public long Id { get; init; }
    public string CategoryCode { get; init; }
    public long OwnerId { get; init; }
    public string Title { get; init; }
    public decimal Price { get; init; }
    public string MetadataJson { get; init; }

    public CollectibleItemDto(DataRow row)
    {
        Id = (long)row["id"];
        CategoryCode = row["category_code"]?.ToString() ?? string.Empty;
        OwnerId = (long)row["owner_id"];
        Title = row["title"]?.ToString() ?? string.Empty;
        Price = row.IsNull("price") ? 0.00m : Convert.ToDecimal(row["price"]);
        MetadataJson = row["metadata_json"]?.ToString() ?? "{}";
    }
}