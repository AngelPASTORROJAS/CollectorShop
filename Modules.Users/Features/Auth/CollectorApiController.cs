using Microsoft.AspNetCore.Mvc;

namespace Modules.Users.Features.Auth;

public class CollectorApiController : ControllerBase
{
    protected long GetUserId => long.Parse(User.Claims.FirstOrDefault(c => c.Type == "U")?.Value ?? "0");
}