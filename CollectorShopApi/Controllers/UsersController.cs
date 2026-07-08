using Microsoft.AspNetCore.Mvc;
using Modules.Users.Features.Auth;
using Modules.Users.Persistence;

namespace CollectorShopApi.Controllers;

[ApiController]
[UserAuth]
[Route("api/users")]
public class UsersController(UsersRepository userRepository) : ControllerBase
{
    private readonly UsersRepository _userRepository = userRepository;

    [HttpGet("{id:long}")]
    public IActionResult GetUser(long id)
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