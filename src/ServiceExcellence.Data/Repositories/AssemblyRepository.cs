using Dapper;
using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Core.Interfaces;

namespace ServiceExcellence.Data.Repositories;

public class AssemblyRepository : IAssemblyRepository
{
    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "a.code",
        ["name"] = "a.name",
        ["level"] = "a.level",
        ["variant"] = "v.name",
        ["path"] = "path",
        ["isActive"] = "a.is_active"
    };

    // Recursive CTE producing each assembly with its full path and a sort key that orders rows as a tree.
    private const string TreeCte = @"
        WITH RECURSIVE tree AS (
            SELECT id, name::text AS path, lpad(code, 40)::text AS sort_key
            FROM assemblies WHERE parent_id IS NULL
            UNION ALL
            SELECT a.id, tree.path || ' / ' || a.name, tree.sort_key || '/' || lpad(a.code, 40)
            FROM assemblies a JOIN tree ON a.parent_id = tree.id
        )";

    private const string SelectSql = @"
        SELECT a.id, a.variant_id, v.name AS variant_name, a.parent_id, p.name AS parent_name, a.level,
               a.code, a.name, a.description, a.is_active, tree.path";

    private const string FromSql = @"
        FROM assemblies a
        JOIN tree ON tree.id = a.id
        JOIN variants v ON v.id = a.variant_id
        LEFT JOIN assemblies p ON p.id = a.parent_id";

    private readonly IDbConnectionFactory _db;

    public AssemblyRepository(IDbConnectionFactory db) => _db = db;

    public async Task<PagedResult<AssemblyDto>> SearchAsync(AssemblyQuery query)
    {
        var p = new DynamicParameters();
        var where = FromSql + " WHERE 1 = 1";
        if (SqlHelper.Like(query.Search) is { } like)
        {
            where += " AND (a.code ILIKE @like OR a.name ILIKE @like OR tree.path ILIKE @like)";
            p.Add("like", like);
        }
        if (query.VariantId.HasValue)
        {
            where += " AND a.variant_id = @variantId";
            p.Add("variantId", query.VariantId);
        }
        if (query.Level.HasValue)
        {
            where += " AND a.level = @level";
            p.Add("level", query.Level);
        }

        // Default ordering keeps the tree shape: by variant, then hierarchical sort key.
        var orderBy = query.SortBy != null && SortColumns.ContainsKey(query.SortBy)
            ? SqlHelper.OrderBy(query, SortColumns, "tree.sort_key")
            : " ORDER BY v.name, tree.sort_key";

        p.Add("Limit", query.PageSize);
        p.Add("Offset", query.Offset);
        var sql = $"{TreeCte} SELECT COUNT(*) {where};\n{TreeCte} {SelectSql} {where} {orderBy} LIMIT @Limit OFFSET @Offset;";

        await using var conn = await _db.OpenAsync();
        using var multi = await conn.QueryMultipleAsync(sql, p);
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<AssemblyDto>()).ToList();
        return new PagedResult<AssemblyDto> { Items = items, TotalCount = total, Page = query.Page, PageSize = query.PageSize };
    }

    public async Task<AssemblyDto?> GetByIdAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<AssemblyDto>($"{TreeCte} {SelectSql} {FromSql} WHERE a.id = @id", new { id });
    }

    public async Task<bool> CodeExistsAsync(int variantId, string code, int? excludeId = null)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<bool>(
            @"SELECT EXISTS (SELECT 1 FROM assemblies WHERE variant_id = @variantId AND lower(code) = lower(@code)
                             AND (@excludeId::int IS NULL OR id <> @excludeId))",
            new { variantId, code, excludeId });
    }

    public async Task<int> CreateAsync(AssemblySaveRequest r, short level)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO assemblies (variant_id, parent_id, level, code, name, description, is_active)
              VALUES (@VariantId, @ParentId, @level, @Code, @Name, @Description, @IsActive) RETURNING id",
            new { r.VariantId, r.ParentId, level, r.Code, r.Name, r.Description, r.IsActive });
    }

    public async Task<bool> UpdateAsync(int id, AssemblySaveRequest r, short level)
    {
        await using var conn = await _db.OpenAsync();
        var rows = await conn.ExecuteAsync(
            @"UPDATE assemblies SET variant_id = @VariantId, parent_id = @ParentId, level = @level, code = @Code, name = @Name,
                     description = @Description, is_active = @IsActive, updated_at = now()
              WHERE id = @id",
            new { id, r.VariantId, r.ParentId, level, r.Code, r.Name, r.Description, r.IsActive });
        return rows > 0;
    }

    public async Task<bool> HasChildrenAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM assemblies WHERE parent_id = @id)", new { id });
    }

    public async Task<IReadOnlyList<AssemblyDto>> GetTreeAsync(int variantId, bool activeOnly)
    {
        var sql = $"{TreeCte} {SelectSql} {FromSql} WHERE a.variant_id = @variantId";
        if (activeOnly) sql += " AND a.is_active";
        sql += " ORDER BY tree.sort_key";

        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync<AssemblyDto>(sql, new { variantId })).ToList();
    }

    public async Task<ClassificationPath?> ResolvePathAsync(int assemblyId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<ClassificationPath>(
            @"SELECT c.division_id, m.category_id, v.model_id, a.variant_id, a.id AS assembly_id
              FROM assemblies a
              JOIN variants v ON v.id = a.variant_id
              JOIN models m ON m.id = v.model_id
              JOIN model_categories c ON c.id = m.category_id
              WHERE a.id = @assemblyId", new { assemblyId });
    }
}
