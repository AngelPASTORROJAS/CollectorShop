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

    public ChatMessageDto(System.Data.DataRow row)
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
