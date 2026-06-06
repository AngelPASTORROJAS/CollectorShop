using System.Collections.Frozen;
using Shared.Infrastructure.Ram.Persistence;

namespace Shared.Infrastructure.Ram;

/// <summary>
/// Conteneur immuable des données globales chargées en RAM.
/// </summary>
public class GlobalDataStore
{
    public DateTime LoadTime { get; init; } = DateTime.UtcNow;

    // Le FrozenDictionary offre des performances de lecture théoriques maximales 
    // car le framework élimine les overheads de synchronisation d'écriture.
    public FrozenDictionary<long, UserRam> AllUsers { get; init; } 
        = FrozenDictionary<long, UserRam>.Empty;

    // Nous pourrons ajouter ici les autres dictionnaires (Ex: Droits, Collectibles, etc.)
    // public FrozenDictionary<long, RoleRam> AllRoles { get; init; }
}