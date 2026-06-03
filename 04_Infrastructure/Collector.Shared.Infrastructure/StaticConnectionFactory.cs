namespace Collector.Shared.Infrastructure;

public static class StaticConnectionFactory
{
    public static IDbConnectionFactory? Instance { get; private set; }

    public static void Initialize(IDbConnectionFactory factory)
    {
        Instance = factory;
    }
}