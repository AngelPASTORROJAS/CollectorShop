namespace Modules.Users.Features.Auth;

public record RegisterRequest(string BusinessName, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record AuthUserDto(long Id);