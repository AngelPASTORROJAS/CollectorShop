using Microsoft.AspNetCore.Mvc;
using Modules.Collector.Persistence;
using Modules.Users.Features.Auth;

namespace CollectorShopApi.Controllers;

[ApiController]
[Route("api/items")]
public class ItemsController(CollectorRepository collectorRepository) : CollectorApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAllItems()
    {
        var items = await collectorRepository.GetAllItemsAsync();
        return Ok(items);
    }

    [HttpPost]
    [UserAuth] 
    public async Task<IActionResult> CreateItem([FromBody] ItemCreateDto request)
    {
        long userId = GetCurrentUserId;
        if (userId <= 0) return Unauthorized();

        try
        {
            long newItemId = await collectorRepository.CreateItemAsync(request, userId);
            if (newItemId > 0)
            {
                return Ok(new { Message = "Article créé avec succès", ItemId = newItemId });
            }
            return BadRequest(new { Message = "Erreur lors de la création de l'article." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }
}
