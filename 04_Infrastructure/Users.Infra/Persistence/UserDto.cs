using Collector.Shared.Infrastructure;
using System.Data;
using System.Text;

namespace Users.Infra.Persistence;

// <summary>
/// DTO Auto-mappé : Lié de manière isomorphe au contrat de la procédure stockée
/// </summary>
public record UserDto
{
    public Guid Id { get; init; }
    public string Username { get; init; }
    public string Email { get; init; }
    public bool IsActive { get; init; }

    // Le constructeur prend le DataRow et peuple les propriétés "init"
    public UserDto(DataRow row)
    {
        Id = row.GetGuid("id") ?? Guid.Empty;
        Username = row.GetString("username") ?? "";
        Email = row.GetString("email") ?? "";
        IsActive = row.GetBool("is_active");
    }
}
