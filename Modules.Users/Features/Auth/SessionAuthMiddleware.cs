using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Modules.Users.Features.Auth;

public class SessionAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SessionTokenManager tokenManager)
    {
        string? token = tokenManager.GetToken(context);

        if (!string.IsNullOrEmpty(token))
        {
            var identity = tokenManager.DecodeJwtToken(token);

            if (identity != null)
            {
                context.User = new ClaimsPrincipal(identity);
            }
        }

        await next(context);
    }
}