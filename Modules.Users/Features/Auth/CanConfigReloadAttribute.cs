using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Ram;
using Shared.Infrastructure.Security;

namespace Modules.Users.Features.Auth;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class CanConfigReloadAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.Request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        var principal = context.HttpContext.User;

        if (principal.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = principal.FindFirst(AuthConstants.ClaimUserId)?.Value;
            if (long.TryParse(userIdClaim, out long userId))
            {
                var cache = context.HttpContext.RequestServices.GetRequiredService<IGlobalCache>();

                if (cache.GetUserById(userId)?.CanConfigReload == true)
                {
                    return Task.CompletedTask;
                }
            }
        }

        context.Result = new ForbidResult();
        return Task.CompletedTask;
    }
}