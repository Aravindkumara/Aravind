using Dapper;
using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Core.Interfaces;

namespace ServiceExcellence.Data.Repositories;

/// <summary>
/// Repository for the four fixed hierarchy tables. Table and column names come from a fixed map keyed by
/// <see cref="MasterType"/>, never from user input.
/// </summary>
public class MasterRepository : IMasterRepository
{
    private sealed record TableInfo(string Table, string? ParentColumn, string? ParentTable);

    private static readonly Dictionary<MasterType, TableInfo> Tables = new()
    {
        [MasterType.Division] = new("divisions", null, null),
        [MasterType.Category] = new("model_categories", "division_id", "divisions"),
        [MasterType.Model] = new("models", "category_id", "model_categories"),
        [MasterType.Variant] = new("variants", "model_id", "models")
    };

    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "t.code",
        ["name"] = "t.name",
        ["parent"] = "parent_name",
        ["isActive"] = "t.is_active",
        ["createdAt"] = "t.created_at"
    };

    private readonly IDbConnectionFactory _db;

    public MasterRepository(IDbConnectionFactory db) => _db = db;

    private static string SelectSql(TableInfo t) => t.ParentColumn == null
        ? "SELECT t.id, NULL::int AS parent_id, NULL AS parent_name, t.code, t.name, t.description, t.is_active, t.created_at"
        : $"SELECT t.id, t.{t.ParentColumn} AS parent_id, p.name AS parent_name, t.code, t.name, t.description, t.is_active, t.created_at";

    private static string FromSql(TableInfo t) => t.ParentColumn == null
        ? $" FROM {t.Table} t"
        : $" FROM {t.Table} t JOIN {t.ParentTable} p ON p.id = t.{t.ParentColumn}";

    public async Task<PagedResult<MasterItemDto>> SearchAsync(MasterType type, MasterQuery query)
    {
        var t = Tables[type];
        var p = new DynamicParameters();
        var where = FromSql(t) + " WHERE 1 = 1";
        if (SqlHelper.Like(query.Search) is { } like)
        {
            where += " AND (t.code ILIKE @like OR t.name ILIKE @like OR t.description ILIKE @like)";
            p.Add("like", like);
        }
        if (query.ParentId.HasValue && t.ParentColumn != null)
        {
            where += $" AND t.{t.ParentColumn} = @parentId";
            p.Add("parentId", query.ParentId);
        }
        if (query.IsActive.HasValue)
        {
            where += " AND t.is_active = @isActive";
            p.Add("isActive", query.IsActive);
        }

        await using var conn = await _db.OpenAsync();
        return await SqlHelper.PageAsync<MasterItemDto>(conn, SelectSql(t), where, SqlHelper.OrderBy(query, SortColumns, "t.code"), query, p);
    }

    public async Task<MasterItemDto?> GetByIdAsync(MasterType type, int id)
    {
        var t = Tables[type];
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<MasterItemDto>(SelectSql(t) + FromSql(t) + " WHERE t.id = @id", new { id });
    }

    public async Task<bool> CodeExistsAsync(MasterType type, string code, int? excludeId = null)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<bool>(
            $"SELECT EXISTS (SELECT 1 FROM {Tables[type].Table} WHERE lower(code) = lower(@code) AND (@excludeId::int IS NULL OR id <> @excludeId))",
            new { code, excludeId });
    }

    public async Task<int> CreateAsync(MasterType type, MasterSaveRequest r)
    {
        var t = Tables[type];
        var sql = t.ParentColumn == null
            ? $"INSERT INTO {t.Table} (code, name, description, is_active) VALUES (@Code, @Name, @Description, @IsActive) RETURNING id"
            : $"INSERT INTO {t.Table} ({t.ParentColumn}, code, name, description, is_active) VALUES (@ParentId, @Code, @Name, @Description, @IsActive) RETURNING id";

        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(sql, r);
    }

    public async Task<bool> UpdateAsync(MasterType type, int id, MasterSaveRequest r)
    {
        var t = Tables[type];
        var parentSet = t.ParentColumn == null ? "" : $"{t.ParentColumn} = @ParentId, ";
        var sql = $"UPDATE {t.Table} SET {parentSet}code = @Code, name = @Name, description = @Description, is_active = @IsActive, updated_at = now() WHERE id = @id";

        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync(sql, new { id, r.ParentId, r.Code, r.Name, r.Description, r.IsActive }) > 0;
    }

    public async Task<IReadOnlyList<LookupItem>> LookupAsync(MasterType type, int? parentId)
    {
        var t = Tables[type];
        var sql = $"SELECT id, code, name FROM {t.Table} WHERE is_active";
        if (t.ParentColumn != null && parentId.HasValue)
            sql += $" AND {t.ParentColumn} = @parentId";
        sql += " ORDER BY name";

        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync<LookupItem>(sql, new { parentId })).ToList();
    }

    public async Task<bool> ExistsAsync(MasterType type, int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<bool>($"SELECT EXISTS (SELECT 1 FROM {Tables[type].Table} WHERE id = @id)", new { id });
    }

    public async Task<ClassificationPath?> ResolvePathAsync(MasterType type, int id)
    {
        var sql = type switch
        {
            MasterType.Division =>
                "SELECT d.id AS division_id FROM divisions d WHERE d.id = @id",
            MasterType.Category =>
                "SELECT c.division_id, c.id AS category_id FROM model_categories c WHERE c.id = @id",
            MasterType.Model =>
                @"SELECT c.division_id, m.category_id, m.id AS model_id
                  FROM models m JOIN model_categories c ON c.id = m.category_id WHERE m.id = @id",
            MasterType.Variant =>
                @"SELECT c.division_id, m.category_id, v.model_id, v.id AS variant_id
                  FROM variants v JOIN models m ON m.id = v.model_id JOIN model_categories c ON c.id = m.category_id
                  WHERE v.id = @id",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<ClassificationPath>(sql, new { id });
    }
}
