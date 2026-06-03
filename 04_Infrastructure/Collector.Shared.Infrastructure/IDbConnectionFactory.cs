using System.Data;

namespace Collector.Shared.Infrastructure;

public interface IDbConnectionFactory
{
    /// <summary>
    /// Crée et ouvre une connexion vers la base de données spécifiée.
    /// </summary>
    /// <param name="targetDatabase">Le nom logique de la base ("Users", "Collector", "Finance")</param>
    IDbConnection 
        CreateOpenConnection(string targetDatabase);
}
