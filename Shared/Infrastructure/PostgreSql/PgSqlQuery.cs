using Npgsql;
using System.Data;

namespace Shared.Infrastructure.PostgreSql;

/* Pour des meilleurs performance en postgre si on fais des transactions on passe par des procédures stockés sinon par des fonctions. */
public class PgSqlQuery : IDisposable
{
    private readonly NpgsqlTransaction? _transaction;

    public string TargetDatabase { get; set; } = PgDbConnectionFactory.DbCollector;
    public string SqlText { get; set; }
    public List<NpgsqlParameter> Parameters { get; set; } = [];
    public int CommandTimeout { get; set; } = -1;
    public bool AutoThrowException { get; set; } = true;
    public Exception? LastException { get; private set; }
    public bool IsFunction { get; set; } = true;


    // Constructeur de base principal
    public PgSqlQuery(string targetDatabase, string sqlText, List<NpgsqlParameter>? parameters = null)
    {
        TargetDatabase = targetDatabase;
        SqlText = sqlText;
        if (parameters != null) Parameters = parameters;
    }

    // Constructeur pour le mode Transactionnel
    public PgSqlQuery(string targetDatabase, NpgsqlTransaction transaction, string sqlText, List<NpgsqlParameter>? parameters = null)
    {
        IsFunction = false;
        TargetDatabase = targetDatabase;
        _transaction = transaction;
        SqlText = sqlText;
        if (parameters != null) Parameters = parameters;
    }

    #region Factory Methods
    public static PgSqlQuery Collector(string sqlText, List<NpgsqlParameter>? parameters = null)
        => new(PgDbConnectionFactory.DbCollector, sqlText, parameters);

    public static PgSqlQuery Finance(string sqlText, List<NpgsqlParameter>? parameters = null)
        => new(PgDbConnectionFactory.DbFinance, sqlText, parameters);

    public static PgSqlQuery Users(string sqlText, List<NpgsqlParameter>? parameters = null)
        => new(PgDbConnectionFactory.DbUsers, sqlText, parameters);

    public static PgSqlQuery TransactionCollector(NpgsqlTransaction transaction, string sqlText, List<NpgsqlParameter>? parameters = null)
        => new(PgDbConnectionFactory.DbCollector, transaction, sqlText, parameters);

    public static PgSqlQuery TransactionFinance(NpgsqlTransaction transaction, string sqlText, List<NpgsqlParameter>? parameters = null)
        => new(PgDbConnectionFactory.DbFinance, transaction, sqlText, parameters);

    public static PgSqlQuery TransactionUsers(NpgsqlTransaction transaction, string sqlText, List<NpgsqlParameter>? parameters = null)
        => new(PgDbConnectionFactory.DbUsers, transaction, sqlText, parameters);
    #endregion

    #region Features
    /// <summary>
    /// Exécute et mappe la première ligne du DataTable vers un DTO unique (ex: GetById)
    /// </summary>
    public async Task<T?> ExecuteAsSingleObjectAsync<T>(Func<DataRow, T> mapper) where T : class
    {
        var dt = await ExecuteAsDataTableAsync();
        if (dt == null || dt.Rows.Count == 0) return null;
        return mapper(dt.Rows[0]);
    }

    /// <summary>
    /// Exécute et mappe toutes les lignes du DataTable vers une liste de DTOs
    /// </summary>
    public async Task<List<T>> ExecuteAsListAsync<T>(Func<DataRow, T> mapper)
    {
        var list = new List<T>();
        var dt = await ExecuteAsDataTableAsync();
        if (dt == null) return list;

        foreach (DataRow row in dt.Rows)
        {
            list.Add(mapper(row));
        }
        return list;
    }
    #endregion


    private NpgsqlCommand GetFunctionCommand(NpgsqlConnection explicitConnection)
    {
        var command = explicitConnection.CreateCommand();
        command.CommandType = CommandType.Text;

        if (_transaction != null) command.Transaction = _transaction;
        if (CommandTimeout != -1) command.CommandTimeout = CommandTimeout;

        foreach (var param in Parameters)
            command.Parameters.Add(param);

        var inputParams = Parameters.Where(p => p.Direction == ParameterDirection.Input || p.Direction == ParameterDirection.InputOutput).ToList();
        var placeholders = string.Join(", ", inputParams.Select(p => p.ParameterName.StartsWith("@") ? p.ParameterName : $"@{p.ParameterName}"));

        command.CommandText = $"SELECT * FROM {SqlText.Trim()}({placeholders});";

        return command;
    }

    private NpgsqlCommand GetProcedureCommand(NpgsqlConnection explicitConnection)
    {
        var command = explicitConnection.CreateCommand();
        command.CommandText = SqlText.Trim();

        if (_transaction != null) command.Transaction = _transaction;
        if (CommandTimeout != -1) command.CommandTimeout = CommandTimeout;

        foreach (var param in Parameters)
            command.Parameters.Add(param);

        if (!command.CommandText.Contains(' '))
            command.CommandType = CommandType.StoredProcedure;

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
            if (_transaction != null)
            {
                connection = _transaction.Connection ?? throw new InvalidOperationException("La transaction ne possède plus de connexion active.");
            }
            else
            {
                if (StaticConnectionFactory.Instance == null)
                    throw new InvalidOperationException("StaticConnectionFactory non initialisé.");
                connection = (NpgsqlConnection)StaticConnectionFactory.Instance.CreateOpenConnection(TargetDatabase);
            }

            command = IsFunction ? GetFunctionCommand(connection) : GetProcedureCommand(connection);

            var reader = command.ExecuteReader();
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
                connection.Close();
                connection.Dispose();
            }
        }
        return dataTable;
    }

    public async Task<DataTable?> ExecuteAsDataTableAsync()
    {
        NpgsqlConnection? connection = null;
        NpgsqlCommand? command = null;
        var dataTable = new DataTable();

        try
        {
            if (_transaction != null)
            {
                connection = _transaction.Connection ?? throw new InvalidOperationException("La transaction ne possède plus de connexion active.");
            }
            else
            {
                if (StaticConnectionFactory.Instance == null)
                    throw new InvalidOperationException("StaticConnectionFactory non initialisé.");
                connection = (NpgsqlConnection)StaticConnectionFactory.Instance.CreateOpenConnection(TargetDatabase);
            }

            command = IsFunction ? GetFunctionCommand(connection) : GetProcedureCommand(connection);
            
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
                connection = _transaction.Connection ?? throw new InvalidOperationException("La transaction ne possède plus de connexion active.");
            }
            else
            {
                if (StaticConnectionFactory.Instance == null)
                    throw new InvalidOperationException("StaticConnectionFactory non initialisé.");
                connection = (NpgsqlConnection)StaticConnectionFactory.Instance.CreateOpenConnection(TargetDatabase);
            }

            command = IsFunction ? GetFunctionCommand(connection) : GetProcedureCommand(connection);
            return command.ExecuteNonQuery();
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

    public async Task<int> ExecuteNonQueryAsync()
    {
        NpgsqlConnection? connection = null;
        NpgsqlCommand? command = null;

        try
        {
            if (_transaction != null)
            {
                connection = _transaction.Connection ?? throw new InvalidOperationException("La transaction ne possède plus de connexion active.");
            }
            else
            {
                if (StaticConnectionFactory.Instance == null)
                    throw new InvalidOperationException("StaticConnectionFactory non initialisé.");
                connection = (NpgsqlConnection)StaticConnectionFactory.Instance.CreateOpenConnection(TargetDatabase);
            }

            command = IsFunction ? GetFunctionCommand(connection) : GetProcedureCommand(connection);
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

    public void Dispose()
    {
        // 1. Nettoyage explicite des paramètres Npgsql
        if (Parameters != null)
        {
            foreach (var param in Parameters)
            {
                // Si le paramètre possède une valeur jetable (comme un flux ou un grand objet)
                if (param.Value is IDisposable disposableValue)
                {
                    disposableValue.Dispose();
                }
            }
            Parameters.Clear();
        }

        // 2. Indique au Garbage Collector qu'il n'a pas besoin d'appeler le destructeur finaliseur
        GC.SuppressFinalize(this);
    }
}