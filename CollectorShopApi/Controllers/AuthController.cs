using Microsoft.AspNetCore.Mvc;
using Modules.Users.Features.Auth;
using Modules.Users.Persistence;

namespace CollectorShopApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService, SessionTokenManager tokenManager, UsersRepository userRepository) : CollectorApiController
{
    private readonly UsersRepository _userRepository = userRepository;

    // Durée de validité du jeton et du cookie : 7 jours en secondes
    private const int OneWeekInSeconds = 7 * 24 * 3600;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var error = await authService.RegisterAsync(request);
        if (error is not null)
            return BadRequest(new { Message = error });

        return Ok(new { Message = "Inscription réussie !" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var (error, user) = await authService.LoginAsync(request);
        if (error is not null)
            return Unauthorized(new { Message = error });

        // Préparation des métadonnées du badge d'accès (Claims)
        var claims = new List<KeyValuePair<string, string>>
        {
            new("U", user!.Id.ToString()),
            new("T", "G"), // 'G' pour GUI / Utilisateur standard
        };

        // 1. Génération du JWT signé cryptographiquement
        string token = tokenManager.GenerateJwtToken(OneWeekInSeconds, claims);

        // 2. Sécurisation automatique dans le cookie HTTP-Only (Anti-XSS)
        tokenManager.SetCookie(HttpContext, token, OneWeekInSeconds);

        // 3. le navigateur joint automatiquement le cookie.
        return Ok();
        // 3. On renvoie aussi le token l'user en JSON pour le state du Mobile ou application tierces (un autre get et post seront associer) avec un type "T" différent
        // return Ok(new { Token = token });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // Destruction instantanée du cookie de session
        tokenManager.ExpireCookie(HttpContext);
        return Ok(new { Message = "Déconnexion réussie." });
    }


    [HttpGet("me")]
    public IActionResult GetMyUser()
    {
        var userDto = _userRepository.GetUserById(GetUserId);

        if (userDto == null)
        {
            return Unauthorized();
        }

        return Ok(userDto);
    }
}