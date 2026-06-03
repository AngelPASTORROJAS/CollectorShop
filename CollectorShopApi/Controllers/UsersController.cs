using Collector.Shared.Infrastructure.Ram;
using Microsoft.AspNetCore.Mvc;
using Users.Infra.Persistence;

namespace CollectorShopApi.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly SqlUserRepository _userRepository;
    private readonly IGlobalCache _cache;

    // On injecte le cache global à la place ou en complément du Repository
    public UsersController(SqlUserRepository userRepository, IGlobalCache cache)
    {
        _userRepository = userRepository;
        _cache = cache;
    }
    

    [HttpGet("{id:guid}")]
    public IActionResult GetUser(Guid id)
    {
        // Appel de ta méthode ADO.NET sans fioriture
        var userDto = _userRepository.GetUserById(id);

        if (userDto == null)
        {
            return NotFound(new { Message = $"Utilisateur avec l'ID {id} introuvable." });
        }

        return Ok(userDto);
    }

    [HttpGet("cached/{id:long}")]
    public IActionResult GetUserFromCache(long id)
    {
        // Lecture directe et instantanée par clé bigint
        var userRam = _cache.GetUserById(id);

        if (userRam == null)
        {
            return NotFound(new { Message = $"Utilisateur avec l'ID {id} introuvable en RAM." });
        }

        return Ok(userRam);
    }
}