using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Modules.Users.Features.Auth;

public class SessionTokenManager
{
    private readonly string _cookieName;
    private readonly string _jwtAudience;
    private readonly byte[] _jwtSigningKey;

    public SessionTokenManager(IConfiguration configuration)
    {
        // Lecture des configurations (avec valeurs de secours pour le dev local)
        _cookieName = configuration["Security:CookieName"] ?? "CollectorDefaultSK_v1";
        _jwtAudience = configuration["Security:JwtAudience"] ?? "CollectorAudience";
        
        string secret = configuration["Security:JwtSecret"] ?? "";
        if (string.IsNullOrEmpty(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException("La clé JWT 'Security:JwtSecret' est absente ou trop faible pour la production !");
        }

        _jwtSigningKey = Encoding.ASCII.GetBytes(secret);
    }

    public string GenerateJwtToken(int validitySec, List<KeyValuePair<string, string>> claims)
    {
        var identity = new ClaimsIdentity();
        foreach (var kv in claims)
        {
            identity.AddClaim(new Claim(kv.Key, kv.Value));
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = identity,
            Audience = _jwtAudience,
            Expires = DateTime.UtcNow.AddSeconds(validitySec),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_jwtSigningKey),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public ClaimsIdentity? DecodeJwtToken(string tokenStr)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tvp = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAudience = _jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(_jwtSigningKey)
        };

        try
        {
            var principal = tokenHandler.ValidateToken(tokenStr, tvp, out var token);
            if (token is not null && token.ValidTo < DateTime.UtcNow) return null;

            if (principal?.Identity is ClaimsIdentity identity)
            {
                return new ClaimsIdentity(identity.Claims, "CustomAuth");
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public string? GetToken(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeaderStr))
        {
            string? authHeader = authHeaderStr.ToString();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader["Bearer ".Length..].Trim();
            }
        }

        context.Request.Cookies.TryGetValue(_cookieName, out string? cookie);
        return cookie;
    }

    public void SetCookie(HttpContext context, string token, int validitySec)
    {
        var opts = new CookieOptions
        {
            Secure = true,      // Uniquement via HTTPS en prod
            HttpOnly = true,    // Bloque l'accès JavaScript (Anti-XSS total)
            SameSite = SameSiteMode.Strict, // Anti-CSRF
            MaxAge = TimeSpan.FromSeconds(validitySec)
        };
        context.Response.Cookies.Append(_cookieName, token, opts);
    }

    public void ExpireCookie(HttpContext context)
    {
        var opts = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(-1),
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Strict
        };
        context.Response.Cookies.Append(_cookieName, "", opts);
    }
}