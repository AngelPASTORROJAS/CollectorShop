using System.Data;

namespace Collector.Shared.Infrastructure.Ram.Persistence;

public class UserRam
{
    public long Id { get; init; }
    public string Username { get; init; }
    public string Email { get; init; }
    public bool IsActive { get; init; }

    // Hydratation traditionnelle à partir du DataRow
    public UserRam(DataRow row)
    {
        Id = row.GetLong("id") ?? 0L;
        Username = row.GetString("username") ?? "";
        Email = row.GetString("email") ?? "";
        IsActive = row.GetBool("is_active");
    }
}
