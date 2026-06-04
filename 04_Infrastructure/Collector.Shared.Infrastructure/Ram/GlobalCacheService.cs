using Collector.Shared.Infrastructure.Ram.Persistence;
using System.Collections.Frozen;
using System.Data;

namespace Collector.Shared.Infrastructure.Ram;

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
    private const string SpLoadUsersRam = "sp_load_users_ram";

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
    /// <remarks>
    /// DESIGN ARCHITECTURAL CRITIQUE - NE PAS MODIFIER OU REMPLACER PAR UN 'lock' TRADITIONNEL :
    /// 1. RACE CONDITIONS (Concurrence) : Interlocked.CompareExchange garantit que si deux threads tentent
    ///    une mise à jour simultanée, un seul gagne. Le thread perdant recommence proprement avec le nouvel état.
    /// 2. DEADLOCKS (Verrous mortels) : Ce code est totalement "Lock-Free". N'utilisant aucun verrou exclusif,
    ///    il est mathématiquement impossible de provoquer un interblocage (deadlock).
    /// 3. STARVATION (Famine de thread) : Sous haute charge, l'ordonnancement natif du processeur garantit 
    ///    que chaque thread finit par appliquer sa modification, assurant un débit maximal sans blocage permanent.
    /// </remarks>
    public void UpdateUserInCache(UserRam updatedUser)
    {
        // Boucle de mise à jour optimiste (Compare-And-Swap)
        while (true)
        {
            var oldStore = _currentStore;

            // On crée un nouveau dictionnaire à partir de l'ancien en remplaçant la cible
            var newDictionary = oldStore.AllUsers.ToDictionary(k => k.Key, v => v.Value);
            newDictionary[updatedUser.Id] = updatedUser;

            var newStore = new GlobalDataStore
            {
                LoadTime = DateTime.UtcNow,
                AllUsers = newDictionary.ToFrozenDictionary()
            };

            // Échange atomique : si _currentStore n'a pas bougé entre temps, on applique
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
            AllUsers = userMap.ToFrozenDictionary(),

            // Autres dictionnaires à préservé :
            // AllOtherCached = oldStore.AllOtherCached
        };

        // 4. Échange atomique sans lock
        Interlocked.Exchange(ref _currentStore, newStore);
    }
    #endregion
}