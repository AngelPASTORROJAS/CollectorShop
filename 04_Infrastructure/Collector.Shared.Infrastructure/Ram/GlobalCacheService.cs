using Collector.Shared.Infrastructure.Ram.Persistence;
using System.Collections.Frozen;
using System.Data;

namespace Collector.Shared.Infrastructure.Ram;

public interface IGlobalCache
{
    DateTime GetLoadTime();
    UserRam? GetUserById(long userId);
    void RefreshAll();     // Recharge tout le cache
    void RefreshUsers();   // Recharge uniquement les utilisateurs à chaud
}

public class GlobalCacheService : IGlobalCache
{
    // Référence volatile vers notre magasin en RAM (Bascule atomique)
    private GlobalDataStore _currentStore = new();

    private const string SpLoadUsersRam = "sp_load_users_ram";
    public DateTime GetLoadTime() => _currentStore.LoadTime;

    /// <summary>
    /// Récupération instantanée en RAM d'un utilisateur par son ID (Lock-Free)
    /// </summary>
    public UserRam? GetUserById(long userId)
    {
        // Extraction locale de la référence courante (Thread-Safe intrinsèque)
        var localStore = _currentStore;

        return localStore.AllUsers.TryGetValue(userId, out var user) ? user : null;
    }

    /// <summary>
    /// Recharge uniquement la brique Utilisateurs sans altérer le reste du cache
    /// </summary>
    public void RefreshUsers()
    {
        // 1. Extraction brute via le standard PgSqlQuery et DataTable
        var query = new PgSqlQuery(PgDbConnectionFactory.DbUsers, SpLoadUsersRam)
        {
            AutoThrowException = true
        };

        var table = query.ExecuteAsDataTable();
        var userMap = new Dictionary<long, UserRam>();

        if (table != null && table.Rows.Count > 0)
        {
            foreach (DataRow row in table.Rows)
            {
                var userRam = new UserRam(row);
                userMap.TryAdd(userRam.Id, userRam);
            }
        }

        // 2. Récupération de la référence actuelle du Store pour préserver les AUTRES dictionnaires
        var oldStore = _currentStore;

        // 3. Construction d'une nouvelle instance du Store combinant le neuf et l'ancien
        var newStore = new GlobalDataStore
        {
            LoadTime = DateTime.UtcNow,
            AllUsers = userMap.ToFrozenDictionary(), // La mise à jour

            // Si tu as d'autres dictionnaires demain, tu les préserves comme ceci :
            // AllRoles = oldStore.AllRoles 
        };

        // 4. Échange atomique sans lock
        Interlocked.Exchange(ref _currentStore, newStore);
    }

    /// <summary>
    /// Recharge l'ensemble des référentiels du cache global
    /// </summary>
    public void RefreshAll()
    {
        // Pour l'instant seul Users existe, on l'appelle directement
        RefreshUsers();

        // Demain, tu ajouteras les autres rechargements à la suite :
        // RefreshRolesInternal();
        // RefreshProductsInternal();
    }
}