using Microsoft.AspNetCore.Mvc;

namespace Modules.Users.Features.Auth;

public class CollectorApiController : ControllerBase
{

    protected long GetUserId
    {
        get
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == "U");
            if (claim is null)
            {
                return 0;
            } 
            return long.Parse(claim.Value);
        }
    }
}
