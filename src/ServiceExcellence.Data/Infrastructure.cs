using System.Data;
using Dapper;
using Npgsql;
using ServiceExcellence.Core.Common;

namespace ServiceExcellence.Data;

public interface IDbConnectionFactory
{
    Task<NpgsqlConnection> OpenAsync();
}

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory, IDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    static NpgsqlConnectionFactory()
    {
        // Map snake_case columns (created_at) to PascalCase properties (CreatedAt).
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public NpgsqlConnectionFactory(string connectionString)
    {
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task<NpgsqlConnection> OpenAsync() => await _dataSource.OpenConnectionAsync();

    public void Dispose() => _dataSource.Dispose();
}

internal static class SqlHelper
{
    /// <summary>
    /// Builds an ORDER BY clause from a whitelist of sortable columns, so user input never reaches the SQL text.
    /// </summary>
    public static string OrderBy(PagedQuery query, IReadOnlyDictionary<string, string> sortColumns, string defaultSort)
    {
        var column = query.SortBy != null && sortColumns.TryGetValue(query.SortBy, out var mapped) ? mapped : defaultSort;
        return $" ORDER BY {column} {(query.Descending ? "DESC" : "ASC")}";
    }

    public static string? Like(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : "%" + EscapeLike(search.Trim()) + "%";

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>Runs a count query and a page query sharing the same FROM/WHERE clause.</summary>
    public static async Task<PagedResult<T>> PageAsync<T>(
        IDbConnection connection, string selectSql, string fromWhereSql, string orderBySql, PagedQuery query, DynamicParameters parameters)
    {
        parameters.Add("Limit", query.PageSize);
        parameters.Add("Offset", query.Offset);

        var sql = $"SELECT COUNT(*) {fromWhereSql};\n{selectSql} {fromWhereSql} {orderBySql} LIMIT @Limit OFFSET @Offset;";
        using var multi = await connection.QueryMultipleAsync(sql, parameters);
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<T>()).ToList();

        return new PagedResult<T> { Items = items, TotalCount = total, Page = query.Page, PageSize = query.PageSize };
    }
}
