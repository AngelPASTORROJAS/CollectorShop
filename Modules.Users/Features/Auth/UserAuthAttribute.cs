using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shared.Infrastructure.Security;

namespace Modules.Users.Features.Auth;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class UserAuthAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.Request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var sourceClaim = user.FindFirst(AuthConstants.ClaimSourceChannel)?.Value;
            if (sourceClaim == AuthConstants.ChannelFrontEnd)
            {
                return Task.CompletedTask;
            }
        }

        context.Result = new UnauthorizedResult();
        return Task.CompletedTask;
    }
}