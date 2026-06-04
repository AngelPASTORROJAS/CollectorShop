using System.Data;
using Npgsql;

namespace Collector.Shared.Infrastructure;

public class PgSqlQuery : IDisposable
{
    private readonly IDbConnectionFactory? _connectionFactory;
    private readonly NpgsqlTransaction? _transaction;

    public string TargetDatabase { get; set; } = "Collector"; // Base par défaut
    public string SqlText { get; set; }
    public List<NpgsqlParameter> Parameters { get; set; } = [];
    public int CommandTimeout { get; set; } = -1;
    public bool AutoThrowException { get; set; } = true;
    public Exception? LastException { get; private set; }

    // Constructeur 1 : Requête simple (on précise la base cible)
    public PgSqlQuery(string targetDatabase, string sqlText, List<NpgsqlParameter>? parameters = null)
    {
        // Récupération de la factory via l'injection (on verra son utilisation)
        TargetDatabase = targetDatabase;
        SqlText = sqlText;
        if (parameters != null) Parameters = parameters;
    }

    // Constructeur 2 : Mode Transactionnel (partage la connexion de la transaction)
    public PgSqlQuery(NpgsqlTransaction transaction, string sqlText, List<NpgsqlParameter>? parameters = null)
    {
        _transaction = transaction;
        SqlText = sqlText;
        if (parameters != null) Parameters = parameters;
    }

    private NpgsqlCommand GetCommand(NpgsqlConnection explicitConnection)
    {
        var command = explicitConnection.CreateCommand();
        command.CommandText = SqlText;

        if (_transaction != null)
            command.Transaction = _transaction;

        // Détection automatique du type (si pas d'espace, c'est une procédure/fonction stockée)
        if (!SqlText.Trim().Contains(' '))
            command.CommandType = CommandType.StoredProcedure;

        foreach (var param in Parameters)
            command.Parameters.Add(param);

        if (CommandTimeout != -1)
            command.CommandTimeout = CommandTimeout;

        return command;
    }

    /// <summary>
    /// Exécute la requête et charge le flux asymétrique directement dans un DataTable (méthode déconnectée)
    /// </summary>
    public DataTable? ExecuteAsDataTable()
    {
        NpgsqlConnection? connection = null;
        NpgsqlCommand? command = null;
        var dataTable = new DataTable();

        try
        {
            // Si on est dans une transaction, on réutilise sa connexion, sinon on en ouvre une dédiée via la Factory
            if (_transaction != null)
            {
                connection = _transaction.Connection;
            }
            else
            {
                if (StaticConnectionFactory.Instance == null)
                    throw new InvalidOperationException("StaticConnectionFactory non initialisé.");

                connection = (NpgsqlConnection)StaticConnectionFactory.Instance.CreateOpenConnection(TargetDatabase);
            }

            command = GetCommand(connection);

            using var reader = command.ExecuteReader();
            dataTable.Load(reader); // Remplace avantageusement le DataAdapter sous PostgreSQL
        }
        catch (Exception ex)
        {
            LastException = ex;
            dataTable = null;
            if (AutoThrowException) throw;
        }
        finally
        {
            command?.Dispose();
            // On ne ferme la connexion QUE si on n'est pas dans une transaction globale
            if (_transaction == null && connection != null)
            {
                connection.Close();
                connection.Dispose();
            }
        }

        return dataTable;
    }

    /// <summary>
    /// Version Asynchrone pour les performances de l'API sous haute charge
    /// </summary>
    public async Task<DataTable?> ExecuteAsDataTableAsync()
    {
        NpgsqlConnection? connection = null;
        NpgsqlCommand? command = null;
        var dataTable = new DataTable();

        try
        {
            if (_transaction != null)
            {
                connection = _transaction.Connection;
            }
            else
            {
                if (StaticConnectionFactory.Instance == null)
                    throw new InvalidOperationException("StaticConnectionFactory non initialisé.");

                connection = (NpgsqlConnection)StaticConnectionFactory.Instance.CreateOpenConnection(TargetDatabase);
            }

            command = GetCommand(connection);

            using var reader = await command.ExecuteReaderAsync();
            dataTable.Load(reader);
        }
        catch (Exception ex)
        {
            LastException = ex;
            dataTable = null;
            if (AutoThrowException) throw;
        }
        finally
        {
            command?.Dispose();
            if (_transaction == null && connection != null)
            {
                await connection.CloseAsync();
                await connection.DisposeAsync();
            }
        }

        return dataTable;
    }

    /// <summary>
    /// Exécute une commande d'écriture (INSERT, UPDATE, DELETE, Stored Proc de mise à jour)
    /// sans allouer de DataTable. Renvoie le nombre de lignes affectées.
    /// </summary>
    public int ExecuteNonQuery()
    {
        NpgsqlConnection? connection = null;
        NpgsqlCommand? command = null;

        try
        {
            if (_transaction != null)
            {
                connection = _transaction.Connection;
            }
            else
            {
                if (StaticConnectionFactory.Instance == null)
                    throw new InvalidOperationException("StaticConnectionFactory non initialisé.");

                connection = (NpgsqlConnection)StaticConnectionFactory.Instance.CreateOpenConnection(TargetDatabase);
            }

            command = GetCommand(connection);
            return command.ExecuteNonQuery(); // Léger, rapide, zéro allocation inutile
        }
        catch (Exception ex)
        {
            LastException = ex;
            if (AutoThrowException) throw;
            return -1;
        }
        finally
        {
            command?.Dispose();
            if (_transaction == null && connection != null)
            {
                connection.Close();
                connection.Dispose();
            }
        }
    }

    /// <summary>
    /// Version asynchrone performante de ExecuteNonQuery
    /// </summary>
    public async Task<int> ExecuteNonQueryAsync()
    {
        NpgsqlConnection? connection = null;
        NpgsqlCommand? command = null;

        try
        {
            if (_transaction != null)
            {
                connection = _transaction.Connection;
            }
            else
            {
                if (StaticConnectionFactory.Instance == null)
                    throw new InvalidOperationException("StaticConnectionFactory non initialisé.");

                connection = (NpgsqlConnection)StaticConnectionFactory.Instance.CreateOpenConnection(TargetDatabase);
            }

            command = GetCommand(connection);
            return await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            LastException = ex;
            if (AutoThrowException) throw;
            return -1;
        }
        finally
        {
            command?.Dispose();
            if (_transaction == null && connection != null)
            {
                await connection.CloseAsync();
                await connection.DisposeAsync();
            }
        }
    }

    public void Dispose() { }
}