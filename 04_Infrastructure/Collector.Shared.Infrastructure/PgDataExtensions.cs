using System.Data;

namespace Collector.Shared.Infrastructure;

public static class PgDataExtensions
{
    public static string? GetString(this DataRow row, string fieldName)
        => row.IsNull(fieldName) ? null : (string)row[fieldName];

    public static Guid? GetGuid(this DataRow row, string fieldName)
        => row.IsNull(fieldName) ? null : (Guid)row[fieldName];

    public static DateTime? GetDateTime(this DataRow row, string fieldName)
        => row.IsNull(fieldName) ? null : (DateTime)row[fieldName];

    public static int? GetInt(this DataRow row, string fieldName)
        => row.IsNull(fieldName) ? null : (int)row[fieldName];

    public static bool GetBool(this DataRow row, string fieldName)
        => !row.IsNull(fieldName) && (bool)row[fieldName];
}