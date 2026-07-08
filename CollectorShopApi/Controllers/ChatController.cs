using Microsoft.AspNetCore.Mvc;
using Modules.Collector.Persistence;
using Modules.Users.Features.Auth;

namespace CollectorShopApi.Controllers;

[ApiController]
[Route("api/chat")]
[UserAuth]
public class ChatController(ChatRepository chatRepository) : CollectorApiController
{
    [HttpGet("{itemId}")]
    public async Task<IActionResult> GetMessages(long itemId)
    {
        long userId = GetCurrentUserId;
        if (userId <= 0) return Unauthorized();

        var messages = await chatRepository.GetMessagesForItemAsync(itemId);
        return Ok(messages);
    }

    [HttpPost("{itemId}")]
    public async Task<IActionResult> SendMessage(long itemId, [FromBody] ChatSendMessageDto request)
    {
        long senderId = GetCurrentUserId;
        if (senderId <= 0) return Unauthorized();

        try
        {
            long messageId = await chatRepository.SendMessageAsync(itemId, senderId, request.ReceiverId, request.Content);
            if (messageId > 0)
            {
                return Ok(new { Message = "Message envoyé", MessageId = messageId });
            }
            return BadRequest(new { Message = "Erreur lors de l'envoi." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }
}
