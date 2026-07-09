using System.Data;

namespace Modules.Collector.Persistence;

public record CollectibleItemDto
{
    public long Id { get; init; }
    public string CategoryCode { get; init; }
    public long OwnerId { get; init; }
    public string Title { get; init; }
    public string Description { get; init; }
    public decimal Price { get; init; }
    public string Status { get; init; }
    public string MetadataJson { get; init; }

    public CollectibleItemDto(DataRow row)
    {
        Id = row.Table.Columns.Contains("id") ? Convert.ToInt64(row["id"]) : 0;
        CategoryCode = row.Table.Columns.Contains("category_code") ? row["category_code"]?.ToString() ?? string.Empty : string.Empty;
        OwnerId = row.Table.Columns.Contains("owner_id") ? Convert.ToInt64(row["owner_id"]) : 0;
        Title = row.Table.Columns.Contains("title") ? row["title"]?.ToString() ?? string.Empty : string.Empty;

        Description = row.Table.Columns.Contains("description") ? row["description"]?.ToString() ?? string.Empty : string.Empty;

        Price = row.Table.Columns.Contains("price") && !row.IsNull("price")
            ? Convert.ToDecimal(row["price"])
            : 0.00m;

        Status = row.Table.Columns.Contains("status") ? row["status"]?.ToString() ?? "AVAILABLE" : "AVAILABLE";
        MetadataJson = row.Table.Columns.Contains("metadata_json") ? row["metadata_json"]?.ToString() ?? "{}" : "{}";
    }
}

public class ItemCreateDto
{
    public string CategoryCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string MetadataJson { get; set; } = "{}";
}


public class ChatMessageDto
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public long SenderId { get; set; }
    public long ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ChatMessageDto() {}

    public ChatMessageDto(DataRow row)
    {
        Id = (long)row["id"];
        ItemId = (long)row["item_id"];
        SenderId = (long)row["sender_id"];
        ReceiverId = (long)row["receiver_id"];
        Content = (string)row["content"];
        CreatedAt = (DateTime)row["created_at"];
    }
}

public class ChatSendMessageDto
{
    public long ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
}

public record ItemOwnerInfo(long OwnerId);