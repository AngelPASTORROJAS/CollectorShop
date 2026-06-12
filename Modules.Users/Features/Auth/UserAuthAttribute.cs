using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Modules.Users.Features.Auth;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class UserAuthAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // On laisse passer les requêtes d'en-tête de sécurité CORS du navigateur
        if (context.HttpContext.Request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            return;

        // Récupération du gestionnaire via le conteneur IoC de la requête
        var tokenManager = context.HttpContext.RequestServices.GetRequiredService<SessionTokenManager>();

        string? token = tokenManager.GetToken(context.HttpContext);

        if (!string.IsNullOrEmpty(token))
        {
            var identity = tokenManager.DecodeJwtToken(token);
            if (identity is not null)
            {
                var typeClaim = identity.Claims.FirstOrDefault(c => c.Type == "T")?.Value;
                if (typeClaim == "G") // 'G' = GUI / Utilisateur Web Standard validé
                {
                    context.HttpContext.User.AddIdentity(identity);
                    return; // Accès accordé !
                }
            }
        }

        // Si pas de token ou token corrompu/expiré -> 401 Unauthorized direct
        context.Result = new UnauthorizedResult();
    }
}