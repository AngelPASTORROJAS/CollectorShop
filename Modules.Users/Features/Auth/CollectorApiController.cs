using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Ram;
using Shared.Infrastructure.Ram.Persistence;
using Shared.Infrastructure.Security;

namespace Modules.Users.Features.Auth;

public class CollectorApiController : ControllerBase
{
    protected long GetCurrentUserId => long.Parse(User.Claims.FirstOrDefault(c => c.Type == AuthConstants.ClaimUserId)?.Value ?? "0");

    /// <summary>
    /// Accès direct aux données de l'utilisateur connecté stockées en RAM
    /// </summary>
    protected UserRam? CurrentUserCache
    {
        get
        {
            long userId = GetCurrentUserId;
            if (userId == 0) return null;

            var cache = HttpContext.RequestServices.GetRequiredService<IGlobalCache>();
            return cache.GetUserById(userId);
        }
    }

    /// <summary>
    /// Raccourcis booléens pour valider les droits à la volée dans le code
    /// </summary>
    protected bool HasCanConfigReload => CurrentUserCache?.CanConfigReload == true;
    protected bool HasCanUserManageAll => CurrentUserCache?.CanUserManageAll == true;
    protected bool HasCanUserManageGroup => CurrentUserCache?.CanUserManageGroup == true;
}