using Shared.Infrastructure.PostgreSql;
using Npgsql;

namespace Modules.Collector.Persistence;

public class ChatRepository
{
    public async Task<List<ChatMessageDto>> GetMessagesForItemAsync(long itemId)
    {
        using var query = PgSqlQuery.Collector("api_get_messages_for_item", [new("p_item_id", itemId)]);
        return await query.ExecuteAsListAsync(row => new ChatMessageDto(row));
    }

    public async Task<long> SendMessageAsync(long itemId, long senderId, long receiverId, string content)
    {
        var parameters = new List<NpgsqlParameter>
        {
            new("p_item_id", itemId),
            new("p_sender_id", senderId),
            new("p_receiver_id", receiverId),
            new("p_content", content)
        };

        using var query = PgSqlQuery.Collector("api_send_message", parameters);
        var dt = await query.ExecuteAsDataTableAsync();
        
        if (dt != null && dt.Rows.Count > 0)
        {
            return (long)dt.Rows[0][0];
        }
        return -1;
    }
}
