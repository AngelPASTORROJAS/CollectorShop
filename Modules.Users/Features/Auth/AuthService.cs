using Microsoft.AspNetCore.Identity;
using Npgsql;
using Shared.Infrastructure.PostgreSql;
using Shared.Infrastructure.Ram;
using Shared.Infrastructure.Ram.Persistence;
using System.Data;

namespace Modules.Users.Features.Auth;

public class AuthService(IGlobalCache globalCache)
{
    private readonly PasswordHasher<string> _hasher = new();

    public async Task<string?> RegisterAsync(RegisterRequest request)
    {
        try
        {
            string hash = _hasher.HashPassword(request.Email, request.Password);

            using var query = PgSqlQuery.Users("api_register_user", [
                new("@p_business_name", request.BusinessName),
                new("@p_email", request.Email),
                new("@p_password_hash", hash)
            ]);
            var dt = await query.ExecuteAsDataTableAsync();

            if (dt == null || dt.Rows.Count == 0)
                return "Erreur lors de la création de l'utilisateur (aucun identifiant renvoyé).";

            long newId = Convert.ToInt64(dt.Rows[0][0]);

            var table = new DataTable();
            table.Columns.Add("id", typeof(long));
            table.Columns.Add("business_name", typeof(string));
            table.Columns.Add("email", typeof(string));
            table.Columns.Add("is_active", typeof(bool));
            table.Columns.Add("user_group", typeof(string));
            table.Columns.Add("can_config_reload", typeof(bool));
            table.Columns.Add("can_user_manage_all", typeof(bool));
            table.Columns.Add("can_user_manage_group", typeof(bool));

            var row = table.NewRow();
            row["id"] = newId;
            row["business_name"] = request.BusinessName;
            row["email"] = request.Email.ToLower();
            row["is_active"] = true;
            row["user_group"] = "User";
            row["can_config_reload"] = false;
            row["can_user_manage_all"] = false;
            row["can_user_manage_group"] = false;
            table.Rows.Add(row);

            var newRamUser = new UserRam(row);
            globalCache.UpdateUserInCache(newRamUser);

            return null;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("EMAIL_ALREADY_EXISTS"))
                return "Cet e-mail est déjà associé à un compte.";

            return $"Erreur lors de l'inscription : {ex.Message}";
        }
    }

    public async Task<(string? Error, AuthUserDto? User)> LoginAsync(LoginRequest request)
    {
        var pId = new NpgsqlParameter("@p_id", DbType.Int64) { Direction = ParameterDirection.Output };
        var pBusinessName = new NpgsqlParameter("@p_business_name", DbType.String, 100) { Direction = ParameterDirection.Output };
        var pHash = new NpgsqlParameter("@p_password_hash", DbType.String, 500) { Direction = ParameterDirection.Output };
        var pIsActive = new NpgsqlParameter("@p_is_active", DbType.Boolean) { Direction = ParameterDirection.Output };

        using var query = PgSqlQuery.Users("api_get_user_for_login", [
            new("@p_email", request.Email),
            pId, 
            pBusinessName, 
            pHash, 
            pIsActive
        ]);

        await query.ExecuteNonQueryAsync();

        if (pId.Value == DBNull.Value)
            return ("Identifiants invalides.", null);

        if (!Convert.ToBoolean(pIsActive.Value))
            return ("Ce compte a été suspendu.", null);

        string storedHash = pHash.Value!.ToString()!;

        var result = _hasher.VerifyHashedPassword(request.Email, storedHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return ("Identifiants invalides.", null);

        var userDto = new AuthUserDto(
            Convert.ToInt64(pId.Value)
        );

        return (null, userDto);
    }

}