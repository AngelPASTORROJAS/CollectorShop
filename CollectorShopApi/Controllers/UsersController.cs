using Microsoft.AspNetCore.Mvc;
using Users.Infra.Persistence;

namespace CollectorShopApi.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly SqlUserRepository _userRepository;

    // L'injection de dépendances fournit automatiquement le Repository configuré
    public UsersController(SqlUserRepository userRepository)
    {
        _userRepository = userRepository;
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
}