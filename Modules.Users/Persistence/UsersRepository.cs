using Shared.Infrastructure.Ram;
using Shared.Infrastructure.Ram.Persistence;
using Shared.Infrastructure.PostgreSql;

namespace Modules.Users.Persistence;

public class UsersRepository(IGlobalCache globalCache)
{
    public UserDto? GetUserById(long userId)
    {
        var data = globalCache.GetUserById(userId);
        if (data == null) return null;

        return new UserDto(data);
    }

    private string? ProceedToEdit(long targetUserId, string newEmail)
    {
        // 1. Récupération de l'état actuel en RAM
        UserRam? userInRam = globalCache.GetUserById(targetUserId);
        if (userInRam is null) return "User not found";

        try
        {
            // 2. Écriture physique en Base de données (Léger via ExecuteNonQuery)
            var query = PgSqlQuery.Users("sp_edit_user_email", [new("@p_user_id", targetUserId), new("@p_user_email", newEmail)]);
            query.ExecuteNonQuery();

            // 3. Mutation par copie immuable de l'objet RAM avec le nouvel email
            UserRam updatedUser = userInRam with { Email = newEmail };

            // 4. Injection de la copie dans le dictionnaire de la RAM
            globalCache.UpdateUserInCache(updatedUser);

            return null; // Null signifie "Pas d'erreur, succès"
        }
        catch (Exception ex)
        {
            // En cas de crash DB (ex: email déjà pris), on capture l'erreur proprement
            return ex.Message;
        }
    }
    public string? EditUser(long currentAdminId, long targetUserId, string newEmail)
    {
        // 1. Récupération de l'admin qui fait la requête depuis la RAM
        var admin = globalCache.GetUserById(currentAdminId);
        if (admin == null || !admin.IsActive) throw new UnauthorizedAccessException();

        // 2. Si c'est un Super Admin global, il a tous les droits
        if (admin.CanUserManageAll)
        {
            return ProceedToEdit(targetUserId, newEmail);
        }

        // 3. Si c'est un Admin de groupe (Client B2B)
        if (admin.CanUserManageGroup)
        {
            var targetUser = globalCache.GetUserById(targetUserId);

            // Sécurité Multi-Tenant : Même groupe requis
            if (targetUser != null && targetUser.UserGroup == admin.UserGroup && admin.UserGroup != null)
            {
                return ProceedToEdit(targetUserId, newEmail);
            }
        }

        // 4. Si aucune condition n'est remplie -> Droit insuffisant
        throw new InvalidOperationException("Vous n'avez pas l'autorisation de modifier cet utilisateur.");
    }
}
