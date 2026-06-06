using Shared.Infrastructure.Ram.Persistence;

namespace Modules.Users.Persistence;

/// <summary>
/// DTO Auto-mappé : Lié de manière isomorphe au contrat de la procédure stockée
/// </summary>
public record UserDto
{
    public long Id { get; init; }
    public string Username { get; init; }
    public string Email { get; init; }
    public bool IsActive { get; init; }

    public UserDto(UserRam userRam)
    {
        Id = userRam.Id;
        Username = userRam.Username;
        Email = userRam.Email;
        IsActive = userRam.IsActive;
    }
}
