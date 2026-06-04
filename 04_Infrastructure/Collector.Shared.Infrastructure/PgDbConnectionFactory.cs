using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;

namespace Collector.Shared.Infrastructure;

public class PgDbConnectionFactory : IDbConnectionFactory
{
    // Centralisation des bases de données de l'infrastructure
    public const string DbUsers = "users";
    public const string DbCollector = "collector";
    public const string DbFinance = "finance";

    private readonly IConfiguration _configuration;

    public PgDbConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IDbConnection CreateOpenConnection(string targetDatabase)
    {
        // 1. Résolution de l'hôte et du port spécifiques à la topologie Docker-Compose
        (string host, string port, string dbName) = targetDatabase.ToLower() switch
        {
            DbUsers => ("collectorshop-users-postgres", "5432", "users_db"),
            DbCollector => ("collectorshop-collector-postgres", "5433", "collector_db"),
            DbFinance => ("collectorshop-finance-postgres", "5434", "finance_db"),
            _ => throw new ArgumentException($"[Infrastructure Error] Base de données cible inconnue : {targetDatabase}")
        };

        // 2. Récupération sécurisée des secrets applicatifs (valeurs par défaut si manquantes)
        string user = _configuration["DbSettings:User"] ?? "myuser";
        string password = _configuration["DbSettings:Password"] ?? "mypassword";

        // 3. Construction dynamique de la chaîne de connexion PostgreSQL (Isomorphe)
        string connectionString = $"Host={host};Port={port};Database={dbName};Username={user};Password={password};Maximum Pool Size=50;";

        // 4. Instanciation, ouverture et renvoi de la connexion
        var connection = new NpgsqlConnection(connectionString);

        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        return connection;
    }
}
