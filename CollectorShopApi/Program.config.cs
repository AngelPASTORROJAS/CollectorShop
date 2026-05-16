using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Primitives;
using System.IO.Compression;

namespace CollectorShopApi;

#region ConfigurationCache
public record ConfigurationResponse(string Version, IReadOnlyDictionary<string, object> Features);

public interface IConfigurationCache
{
    ConfigurationResponse GetCurrent();
}

public class ConfigurationCache : IConfigurationCache, IDisposable
{
    private readonly IConfiguration _config;
    private readonly IDisposable? _changeTokenSubscription;
    private ConfigurationResponse _cachedResponse = null!;

    private static readonly string VERSION_PROPERTY = "Version";
    private static readonly string FEATURES_PROPERTY = "Features";
    private static readonly string VERSION_DEFAULT = "0.0.0";
    private static readonly Dictionary<string, object> FEATURES_DEFAULT = [];

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

            // La conversion automatique se produit ici :
            if (int.TryParse(value, out int intVal)) return intVal;
            return value ?? "";
        }

        // Cas 2 : C'est un tableau JSON (les clés de .NET sont purement numériques : "0", "1", "2")
        if (children.All(c => int.TryParse(c.Key, out _)))
        {
            return children
                .OrderBy(c => int.Parse(c.Key)) // On s'assure de garder le bon ordre du tableau
                .Select(ParseConfigurationSection)
                .ToList();
        }

        // Cas 3 : C'est un objet/dictionnaire classique
        return children.ToDictionary(
            c => c.Key,
            c => ParseConfigurationSection(c)
        );
    }

    public ConfigurationCache(IConfiguration config)
    {
        _config = config;
        UpdateCache();
        _changeTokenSubscription = ChangeToken.OnChange(() => _config.GetReloadToken(), UpdateCache);
    }

    private void UpdateCache()
    {
        string version = _config.GetValue<string>(VERSION_PROPERTY) ?? VERSION_DEFAULT;
        var featuresSection = _config.GetSection(FEATURES_PROPERTY);

        IReadOnlyDictionary<string, object> features = FEATURES_DEFAULT;

        if (featuresSection.GetChildren().Any())
        {
            features = ParseConfigurationSection(featuresSection) as IReadOnlyDictionary<string, object> ?? FEATURES_DEFAULT;
        }
        _cachedResponse = new ConfigurationResponse(version, features);
    }

    public ConfigurationResponse GetCurrent() => _cachedResponse;

    public void Dispose()
    {
        _changeTokenSubscription?.Dispose();
    }
}
#endregion


#region StartUp
public static class ApplicationSetup
{
    private static readonly string ALLOWED_POLICY_NAME = "all";
    private static readonly string[] ALLOWED_URLS = ["http://localhost:5000", "https://localhost:5001"];
    private static readonly string[] ALLOWED_HEADERS = ["Content-Type", "Authorization"];
    private static readonly string[] ALLOWED_METHODS = ["GET"];
    private static readonly IEnumerable<string> CROMPRESSED_MIME_TYPES = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
    
    public static WebApplicationBuilder ConfigureServices(this WebApplicationBuilder builder)
    {
        // On retire la politique camelCase par défaut, pas de "mapping mental" => "Isomorphe"
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = null);

        builder.Services.AddControllers();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddSingleton<IConfigurationCache, ConfigurationCache>();

        builder.Services.AddCors(options => options.AddPolicy(ALLOWED_POLICY_NAME, policy => policy.WithOrigins(ALLOWED_URLS).WithHeaders(ALLOWED_HEADERS).WithMethods(ALLOWED_METHODS)));

        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = CROMPRESSED_MIME_TYPES;
        });

        builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

        return builder;
    }


    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        // 1. Redirection HTTPS en premier
        app.UseHttpsRedirection();

        // 2. Compression de la réponse (doit être placée haut dans le pipeline)
        app.UseResponseCompression();

        // 3. Fichiers statiques (wwwroot)
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // 4. CORS (doit être configuré avant l'authentification et les endpoints)
        app.UseCors(ALLOWED_POLICY_NAME);

        // 5. OpenAPI (Swagger)
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // 6. Sécurité (Authentification puis Autorisation)
        app.UseAuthentication();
        app.UseAuthorization();

        // 7. Routage des contrôleurs
        app.MapControllers();

        // 8. Configuration dynamique pour le front
        app.MapGet("/configuration", ([FromServices] IConfigurationCache cache) => Results.Ok(cache.GetCurrent()));

        return app;
    }
}
#endregion
