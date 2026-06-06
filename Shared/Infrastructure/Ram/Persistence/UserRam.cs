using Shared.Infrastructure.PostgreSql;
using System.Data;

namespace Shared.Infrastructure.Ram.Persistence;

public record UserRam
{
    public long Id { get; init; }
    public string Username { get; init; }
    public string Email { get; init; }
    public string? UserGroup { get; init; }
    public bool IsActive { get; init; }

    #region Droits
    public bool CanConfigReload { get; init; }
    public bool CanUserManageAll { get; init; }
    public bool CanUserManageGroup { get; init; }
    #endregion

    public UserRam(DataRow row)
    {
        Id = row.GetLong("id") ?? 0L;
        Username = row.GetString("business_name") ?? "";
        Email = row.GetString("email") ?? "";
        IsActive = row.GetBool("is_active");
        UserGroup = row.GetString("user_group");

        #region Droits
        // Extraction directe des flags agrégés par le sp_load_users_ram
        CanConfigReload = row.GetBool("can_config_reload");
        CanUserManageAll = row.GetBool("can_user_manage_all");
        CanUserManageGroup = row.GetBool("can_user_manage_group");
        #endregion
    }
}
