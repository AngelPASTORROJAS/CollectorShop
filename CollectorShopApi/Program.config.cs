using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.FileProviders;
using System.IO.Compression;
using Scalar.AspNetCore;
using Shared.Infrastructure.PostgreSql;
using Shared.Infrastructure.Ram;
using Modules.Finance.Persistence;
using Modules.Collector.Persistence;
using Modules.Users.Persistence;
using Modules.Users.Features.Auth;

namespace CollectorShopApi;

#region StartUp
public static class ApplicationSetup
{
    // --- Configuration du Domaine ---
    private static readonly string[] ALLOWED_ORIGINS = [
        "http://localhost:5001", "https://localhost:5002",  // Public (Utilisateurs / Acheteurs / Vendeurs)
        "http://localhost:5003", "https://localhost:5004"   // Privé (Admin)
        ];
    private static readonly string[] ALLOWED_HEADERS = ["Content-Type", "Authorization"];
    private static readonly string[] ALLOWED_METHODS = ["GET", "POST", "PUT", "DELETE"];

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
        builder.Services.AddTransient<ChatRepository>();

        builder.Services.AddSingleton<SessionTokenManager>();
        builder.Services.AddTransient<AuthService>();

        builder.Services.AddCors(options => {
            options.AddDefaultPolicy(policy => policy
                .WithOrigins(ALLOWED_ORIGINS)
                .WithHeaders(ALLOWED_HEADERS)
                .WithMethods(ALLOWED_METHODS)
                .AllowCredentials());
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

        builder.Services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApiDocument(options => {
            options.Title = "Collector Shop API";
            options.OperationProcessors.Add(new ForceJsonMediaTypeProcessor());
        });

        builder.Services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("PostgreSQL-Cluster");
        return builder;
    }


    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            
            // 1. Redirection HTTPS en tête (Garantit le chiffrement forcé des flux)
            app.UseHttpsRedirection();
        }


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

        // 5. Scalar
        if (app.Environment.IsDevelopment())
        {
            app.UseOpenApi();
            app.MapScalarApiReference(options =>
            {
                options.OpenApiRoutePattern = "/swagger/v1/swagger.json";
            });
        }

        // 5. Couches de sécurité applicatives (Qui tu es, puis ce que tu as le droit de faire)
        app.UseMiddleware<SessionAuthMiddleware>(); // Remplace l'authentification native, remplit le User
        app.UseAuthorization();         // tes droits

        // 6. Mapping des routes
        app.MapControllers();

        // Endpoint technique de configuration dynamique
        app.MapGet("/configuration", ([FromServices] ConfigurationCache cache) => Results.Ok(cache.GetCurrent()));

        app.MapHealthChecks("/health");

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
