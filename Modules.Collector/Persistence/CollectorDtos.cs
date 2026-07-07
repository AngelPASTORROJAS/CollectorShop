namespace Modules.Collector.Persistence;

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
