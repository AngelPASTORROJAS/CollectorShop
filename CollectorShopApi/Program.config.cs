using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.IO.Compression;
using Scalar.AspNetCore;
using Shared.Infrastructure.PostgreSql;
using Shared.Infrastructure.Ram;
using Modules.Finance.Persistence;
using Modules.Collector.Persistence;
using Modules.Users.Persistence;

namespace CollectorShopApi;

#region ConfigurationCache
public record ConfigurationResponse(string Version, IReadOnlyDictionary<string, object> Features);

public class ConfigurationCache : IDisposable
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
    // --- Configuration du Domaine ---
    private static readonly string[] ALLOWED_ORIGINS = [
        "http://localhost:5001", "https://localhost:5002",  // Public (Utilisateurs / Acheteurs / Vendeurs)
        "http://localhost:5003", "https://localhost:5004"   // Privé (Admin)
        ];
    private static readonly string[] ALLOWED_HEADERS = ["Content-Type", "Authorization"];
    private static readonly string[] ALLOWED_METHODS = ["GET"]; // Uniquement du GET (Ex: catalogue public)

    private static readonly IEnumerable<string> COMPRESSED_MIME_TYPES = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
    
    public static WebApplicationBuilder ConfigureServices(this WebApplicationBuilder builder)
    {

        builder.Services.AddSingleton<ConfigurationCache>();
        builder.Services.AddSingleton<PgDbConnectionFactory>();

        // ENREGISTREMENT DU CACHE GLOBAL EN RAM 
        builder.Services.AddSingleton<IGlobalCache, GlobalCacheService>();

        // Repositories ADO.NET (En mode Transient ou Scoped, au choix, ici Transient car ils n'ont pas d'état)
        builder.Services.AddTransient<UsersRepository>();
        builder.Services.AddTransient<FinanceRepository>();
        builder.Services.AddTransient<CollectorRepository>();

        builder.Services.AddCors(options => {
            options.AddDefaultPolicy(policy => policy
                .WithOrigins(ALLOWED_ORIGINS)
                .WithHeaders(ALLOWED_HEADERS)
                .WithMethods(ALLOWED_METHODS));
        });

        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = COMPRESSED_MIME_TYPES;
        });

        builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
 
        // On retire la politique camelCase par défaut, pas de "mapping mental" => "Isomorphe"
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = null);
        
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi();

        return builder;
    }


    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        // 1. Redirection HTTPS en tête (Garantit le chiffrement forcé des flux)
        app.UseHttpsRedirection();

        // 2. Compression (Doit intercepter le flux le plus haut possible)
        app.UseResponseCompression();

        // 3. Gestion des fichiers statiques locaux (wwwroot) => public, privé
        // 1. Pour le site Grand Public (Ex: accessible directement sur la racine http://localhost:5000/)
        app.UseFileServer(new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "public")),
            RequestPath = "" // Racine
        });

        // 2. Pour le Backoffice Admin (Ex: accessible uniquement sur http://localhost:5000/admin)
        app.UseFileServer(new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "admin")),
            RequestPath = "/admin" // Route dédiée
        });

        // 4. CORS (Doit être exécuté impérativement AVANT le routage et l'authentification)
        app.UseCors();

        // 5. OpenAPI (Swagger)
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        // 5. Couches de sécurité applicatives (Qui tu es, puis ce que tu as le droit de faire)        app.UseAuthentication();
        app.UseAuthentication();        // Qui tu es
        app.UseAuthorization();         // tes droits

        // 6. Mapping des routes
        app.MapControllers();

        // Endpoint technique de configuration dynamique
        app.MapGet("/configuration", ([FromServices] ConfigurationCache cache) => Results.Ok(cache.GetCurrent()));

        // Route pour recharger UNIQUEMENT la brique utilisateur à chaud (Ex: après modif SQL)
        app.MapPost("/infra/cache/refresh/users", ([FromServices] IGlobalCache cache) =>
        {
            cache.RefreshUsers();
            return Results.Ok(new { Message = "Cache des utilisateurs rechargé avec succès.", Time = cache.GetLoadTime() });
        });

        // Route pour recharger l'INTEGRALITE du système
        app.MapPost("/infra/cache/refresh/all", ([FromServices] IGlobalCache cache) =>
        {
            cache.RefreshAll();
            return Results.Ok(new { Message = "L'intégralité du cache global a été synchronisée.", Time = cache.GetLoadTime() });
        });

        // 1. Initialisation de la factory statique pour ADO.NET
        var factory = app.Services.GetRequiredService<PgDbConnectionFactory>();
        StaticConnectionFactory.Initialize(factory);

        // 2. PRE-CHARGEMENT DES DONNÉES CRITIQUES EN RAM AU DÉMARRAGE
        var globalCache = app.Services.GetRequiredService<IGlobalCache>();
        try
        {
            globalCache.RefreshAll();
            Console.WriteLine($"[Cache] Données globales chargées avec succès en RAM à {globalCache.GetLoadTime()}");
        }
        catch (Exception ex)
        {
            // En production, on peut logguer l'erreur ou bloquer le démarrage si les données sont indispensables
            Console.WriteLine($"[Cache Erreur] Impossible de pré-charger les données au démarrage : {ex.Message}");
        }

        return app;
    }
}
#endregion
