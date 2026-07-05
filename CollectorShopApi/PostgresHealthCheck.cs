using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shared.Infrastructure.PostgreSql;

namespace CollectorShopApi;

public class PostgresHealthCheck(PgDbConnectionFactory factory) : IHealthCheck
{
    private readonly PgDbConnectionFactory _factory = factory;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        string[] databasesToTest = [
            PgDbConnectionFactory.DbUsers,
            PgDbConnectionFactory.DbCollector,
            PgDbConnectionFactory.DbFinance
        ];

        try
        {
            // Mode ultra-rapide parallèle
            Parallel.ForEach(databasesToTest, db =>
            {
                using var conn = _factory.CreateOpenConnection(db);
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    throw new Exception($"La base '{db}' est fermée.");
                }
            });

            return Task.FromResult(HealthCheckResult.Healthy(
                "Les 3 bases PostgreSQL (Users, Collector, Finance) sont opérationnelles."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Panne détectée sur le cluster de données : {ex.Message}"));
        }
    }
}