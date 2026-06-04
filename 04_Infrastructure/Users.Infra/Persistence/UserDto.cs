using Collector.Shared.Infrastructure;
using Collector.Shared.Infrastructure.Ram.Persistence;
using System.Data;
using System.Text;

namespace Users.Infra.Persistence;

// <summary>
/// DTO Auto-mappé : Lié de manière isomorphe au contrat de la procédure stockée
/// </summary>
public record UserDto
{
    public long Id { get; init; }
    public string Username { get; init; }
    public string Email { get; init; }
    public bool IsActive { get; init; }

    // Le constructeur prend le DataRow et peuple les propriétés "init"
    public UserDto(UserRam userRam)
    {
        Id = userRam.Id;
        Username = userRam.Username;
        Email = userRam.Email;
        IsActive = userRam.IsActive;
    }
}
