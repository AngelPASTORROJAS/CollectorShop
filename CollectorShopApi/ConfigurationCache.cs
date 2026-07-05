using Microsoft.Extensions.Primitives;

namespace CollectorShopApi;

/// <summary>
/// Représente la réponse structurée contenant la version de l'application et ses fonctionnalités actives.
/// </summary>
public record ConfigurationResponse(string Version, IReadOnlyDictionary<string, object> Features);

/// <summary>
/// Gère le cache en mémoire et le rechargement dynamique à chaud de la configuration applicative.
/// </summary>
public class ConfigurationCache : IDisposable
{
    private readonly IConfiguration _config;
    private readonly IDisposable? _changeTokenSubscription;
    private ConfigurationResponse _cachedResponse = null!;

    private static readonly string VERSION_PROPERTY = "Version";
    private static readonly string FEATURES_PROPERTY = "Features";
    private static readonly string VERSION_DEFAULT = "0.0.0";
    private static readonly Dictionary<string, object> FEATURES_DEFAULT = [];

    public ConfigurationCache(IConfiguration config)
    {
        _config = config;
        UpdateCache();
        _changeTokenSubscription = ChangeToken.OnChange(() => _config.GetReloadToken(), UpdateCache);
    }

    /// <summary>
    /// Récupère la configuration actuellement stockée dans le cache.
    /// </summary>
    public ConfigurationResponse GetCurrent() => _cachedResponse;

    private void UpdateCache()
    {
        string version = _config.GetValue<string>(VERSION_PROPERTY) ?? VERSION_DEFAULT;
        var featuresSection = _config.GetSection(FEATURES_PROPERTY);

        IReadOnlyDictionary<string, object> features = FEATURES_DEFAULT;

        if (featuresSection.GetChildren().Any())
        {
            var parsed = ParseConfigurationSection(featuresSection);
            features = parsed as IReadOnlyDictionary<string, object> ?? FEATURES_DEFAULT;
        }
        _cachedResponse = new ConfigurationResponse(version, features);
    }

    /// <summary>
    /// Analyse récursivement une section de configuration .NET pour reconstruire un arbre d'objets typés (JSON).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ATTENTION - TOLÉRANCE AUX CHAÎNES NUMÉRIQUES (Effet de bord documenté) :</b><br/>
    /// La méthode utilise <see cref="int.TryParse(string?, out int)"/> sur les valeurs simples. Par conséquent, 
    /// toute chaîne de caractères purement numérique dans le fichier JSON source (ex: <c>"6"</c> ou <c>"4"</c>) 
    /// sera <b>automatiquement convertie en véritable entier (<c>int</c>)</b> dans l'objet de sortie.
    /// </para>
    /// <para>
    /// <b>Exemples de comportements induits :</b><br/>
    /// • Tableaux mixtes (ex: <c>["4", "z"]</c>) ➔ Sortiront typés en <c>[4, "z"]</c> (l'entier est converti, la string reste une string).<br/>
    /// • Propriétés d'objets (ex: <c>"props": "6"</c>) ➔ Sortiront sous forme de nombre <c>"props": 6</c>.<br/>
    /// </para>
    /// <para>
    /// Ce comportement élimine les erreurs de saisie du fichier de configuration mais impose au Front-End (TypeScript) 
    /// d'attendre des types numériques natifs pour ces champs.
    /// </para>
    /// </remarks>
    /// <param name="section">La section racine <see cref="IConfigurationSection"/> à analyser (ex: "Features").</param>
    /// <returns>
    /// Un <see cref="object"/> pouvant être un type primitif (<see cref="bool"/>, <see cref="int"/>, <see cref="string"/>), 
    /// une <see cref="List{Object}"/> pour les tableaux, ou un <see cref="Dictionary{String, Object}"/> pour les sous-objets.
    /// </returns>
    private static object ParseConfigurationSection(IConfigurationSection section)
    {
        var children = section.GetChildren().ToList();

        // Cas 1 : C'est une valeur simple (string, bool, int)
        if (children.Count == 0)
        {
            string? value = section.Value;
            if (bool.TryParse(value, out bool boolVal)) return boolVal;
            if (int.TryParse(value, out int intVal)) return intVal;
            return value ?? "";
        }

        // Cas 2 : C'est un tableau JSON (les clés de .NET sont purement numériques : "0", "1", "2")
        if (children.All(c => int.TryParse(c.Key, out _)))
        {
            return children
                .OrderBy(c => int.Parse(c.Key))
                .Select(ParseConfigurationSection)
                .ToList();
        }

        // Cas 3 : C'est un objet/dictionnaire classique
        return children.ToDictionary(
            c => c.Key,
            c => ParseConfigurationSection(c)
        );
    }

    public void Dispose()
    {
        _changeTokenSubscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}