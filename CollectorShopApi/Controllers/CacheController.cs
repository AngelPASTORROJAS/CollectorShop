using Microsoft.AspNetCore.Mvc;
using Modules.Users.Features.Auth;
using Shared.Infrastructure.Ram;

namespace CollectorShopApi.Controllers;

[ApiController]
[Route("api/infra/cache")]
[CanConfigReload]
public class CacheController(IGlobalCache cache) : CollectorApiController
{
    [HttpPost("refresh/users")]
    public IActionResult RefreshUsers()
    {
        cache.RefreshUsers();
        return Ok();
    }

    [HttpPost("refresh/all")]
    public IActionResult RefreshAll()
    {
        cache.RefreshAll();
        return Ok();
    }
}