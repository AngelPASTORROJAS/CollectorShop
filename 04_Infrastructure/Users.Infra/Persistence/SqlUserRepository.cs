using Npgsql;
using Collector.Shared.Infrastructure;
using System.Data;

namespace Users.Infra.Persistence;

public class SqlUserRepository
{
    private const string SpGetUserById = "sp_get_user_by_id";

    public UserDto? GetUserById(Guid userId)
    {
        // 1. Appel de la procédure stockée
        var query = new PgSqlQuery(PgDbConnectionFactory.DbUsers, SpGetUserById)
        {
            Parameters = [new NpgsqlParameter("@p_user_id", userId)]
        };

        // 2. Récupération directe sous forme de DataTable
        var table = query.ExecuteAsDataTable();

        if (!(table != null && table.Rows.Count > 0)) 
            return null;
        
        DataRow row = table.Rows[0];
        return new UserDto(row);
    }
}