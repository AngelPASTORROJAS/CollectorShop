using Shared.Infrastructure.PostgreSql;
using Shared.Infrastructure.Ram.Persistence;
using System.Collections.Frozen;
using System.Data;

namespace Shared.Infrastructure.Ram;

public interface IGlobalCache
{
    DateTime GetLoadTime();
    void RefreshAll();

    #region Users
    void RefreshUsers();
    UserRam? GetUserById(long userId);
    void UpdateUserInCache(UserRam updatedUser);
    #endregion
}

public class GlobalCacheService : IGlobalCache
{
    private GlobalDataStore _currentStore = new();
    public DateTime GetLoadTime() => _currentStore.LoadTime;
    
    /// <summary>
    /// Recharge l'ensemble des référentiels du cache global
    /// </summary>
    public void RefreshAll()
    {
        RefreshUsers();
    }

    #region Users
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
    /// Met à jour un utilisateur dans le cache en RAM via une boucle atomique Compare-And-Swap (CAS) non bloquante.
    /// </summary>
    public void UpdateUserInCache(UserRam updatedUser)
    {
        while (true)
        {
            var oldStore = _currentStore;

            // Sécurité : Si l'utilisateur en RAM est déjà identique à la mise à jour, on évite des allocations inutiles
            if (oldStore.AllUsers.TryGetValue(updatedUser.Id, out var existing) && existing == updatedUser)
                break;

            // On crée un nouveau dictionnaire à partir de l'état le plus récent du Store
            var newDictionary = oldStore.AllUsers.ToDictionary(k => k.Key, v => v.Value);
            newDictionary[updatedUser.Id] = updatedUser;

            var newStore = new GlobalDataStore
            {
                LoadTime = DateTime.UtcNow,
                AllUsers = newDictionary.ToFrozenDictionary(),
            };

            // Échange atomique : si aucun autre thread n'a modifié _currentStore (Users OU autre dictionnaire), on applique
            if (Interlocked.CompareExchange(ref _currentStore, newStore, oldStore) == oldStore)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Recharge uniquement la brique Utilisateurs sans altérer le reste du cache
    /// </summary>
    public void RefreshUsers()
    {
        // 1. Extraction brute via le standard PgSqlQuery et DataTable
        using var query = PgSqlQuery.Users("api_load_users_ram");
        query.AutoThrowException = true;

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

        var frozenUsers = userMap.ToFrozenDictionary();

        // 2. Boucle CAS pour s'assurer qu'on ne perd pas une mise à jour d'un AUTRE dictionnaire faite en parallèle
        while (true)
        {
            var oldStore = _currentStore;

            var newStore = new GlobalDataStore
            {
                LoadTime = DateTime.UtcNow,
                AllUsers = frozenUsers,
            };

            if (Interlocked.CompareExchange(ref _currentStore, newStore, oldStore) == oldStore)
            {
                break;
            }
        }
    }
    #endregion
}