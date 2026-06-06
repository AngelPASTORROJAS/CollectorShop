namespace Shared.Infrastructure.PostgreSql;

public static class StaticConnectionFactory
{
    public static PgDbConnectionFactory? Instance { get; private set; }

    public static void Initialize(PgDbConnectionFactory factory)
    {
        Instance = factory;
    }
}