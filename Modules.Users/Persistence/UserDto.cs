using Shared.Infrastructure.Ram.Persistence;

namespace Modules.Users.Persistence;

/// <summary>
/// DTO Auto-mappé : Lié de manière isomorphe au contrat de la procédure stockée
/// </summary>
public record UserDto
{
    public long Id { get; init; }
    public string BusinessName { get; init; }
    public string Email { get; init; }
    public bool IsActive { get; init; }

    public UserDto(UserRam userRam)
    {
        Id = userRam.Id;
        BusinessName = userRam.BusinessName;
        Email = userRam.Email;
        IsActive = userRam.IsActive;
    }
}
