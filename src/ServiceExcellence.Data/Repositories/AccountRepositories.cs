using Dapper;
using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Core.Interfaces;

namespace ServiceExcellence.Data.Repositories;

public class UserRepository : IUserRepository
{
    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["username"] = "u.username",
        ["fullName"] = "u.full_name",
        ["role"] = "u.role",
        ["dealer"] = "d.name",
        ["isActive"] = "u.is_active",
        ["lastLogin"] = "u.last_login_at"
    };

    private readonly IDbConnectionFactory _db;

    public UserRepository(IDbConnectionFactory db) => _db = db;

    public async Task<UserCredentials?> GetCredentialsByUsernameAsync(string username)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<UserCredentials>(
            "SELECT id, username, password_hash, full_name, role, dealer_id, is_active FROM users WHERE lower(username) = lower(@username)",
            new { username });
    }

    public async Task<UserCredentials?> GetCredentialsByIdAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<UserCredentials>(
            "SELECT id, username, password_hash, full_name, role, dealer_id, is_active FROM users WHERE id = @id", new { id });
    }

    public async Task<PagedResult<UserDto>> SearchAsync(UserQuery query)
    {
        const string select = @"SELECT u.id, u.username, u.full_name, u.email, u.role, u.dealer_id, d.name AS dealer_name,
                                       u.is_active, u.last_login_at, u.created_at";
        var p = new DynamicParameters();
        var where = " FROM users u LEFT JOIN dealers d ON d.id = u.dealer_id WHERE 1 = 1";
        if (SqlHelper.Like(query.Search) is { } like)
        {
            where += " AND (u.username ILIKE @like OR u.full_name ILIKE @like OR u.email ILIKE @like OR d.name ILIKE @like)";
            p.Add("like", like);
        }
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            where += " AND u.role = @role";
            p.Add("role", query.Role);
        }

        await using var conn = await _db.OpenAsync();
        return await SqlHelper.PageAsync<UserDto>(conn, select, where, SqlHelper.OrderBy(query, SortColumns, "u.username"), query, p);
    }

    public async Task<UserDto?> GetByIdAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<UserDto>(
            @"SELECT u.id, u.username, u.full_name, u.email, u.role, u.dealer_id, d.name AS dealer_name,
                     u.is_active, u.last_login_at, u.created_at
              FROM users u LEFT JOIN dealers d ON d.id = u.dealer_id WHERE u.id = @id", new { id });
    }

    public async Task<bool> UsernameExistsAsync(string username, int? excludeId = null)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM users WHERE lower(username) = lower(@username) AND (@excludeId::int IS NULL OR id <> @excludeId))",
            new { username, excludeId });
    }

    public async Task<int> CreateAsync(UserSaveRequest r, string passwordHash)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO users (username, password_hash, full_name, email, role, dealer_id, is_active)
              VALUES (@Username, @passwordHash, @FullName, @Email, @Role, @DealerId, @IsActive) RETURNING id",
            new { r.Username, passwordHash, r.FullName, r.Email, r.Role, r.DealerId, r.IsActive });
    }

    public async Task<bool> UpdateAsync(int id, UserSaveRequest r, string? passwordHash)
    {
        await using var conn = await _db.OpenAsync();
        var rows = await conn.ExecuteAsync(
            @"UPDATE users SET username = @Username, full_name = @FullName, email = @Email, role = @Role,
                     dealer_id = @DealerId, is_active = @IsActive,
                     password_hash = COALESCE(@passwordHash, password_hash), updated_at = now()
              WHERE id = @id",
            new { id, r.Username, r.FullName, r.Email, r.Role, r.DealerId, r.IsActive, passwordHash });
        return rows > 0;
    }

    public async Task UpdatePasswordAsync(int id, string passwordHash)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecuteAsync("UPDATE users SET password_hash = @passwordHash, updated_at = now() WHERE id = @id", new { id, passwordHash });
    }

    public async Task SetLastLoginAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecuteAsync("UPDATE users SET last_login_at = now() WHERE id = @id", new { id });
    }
}

public class DealerRepository : IDealerRepository
{
    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "code",
        ["name"] = "name",
        ["city"] = "city",
        ["state"] = "state",
        ["isActive"] = "is_active"
    };

    private const string Columns = "id, code, name, city, state, contact_phone, email, is_active, created_at";

    private readonly IDbConnectionFactory _db;

    public DealerRepository(IDbConnectionFactory db) => _db = db;

    public async Task<PagedResult<DealerDto>> SearchAsync(PagedQuery query)
    {
        var p = new DynamicParameters();
        var where = " FROM dealers WHERE 1 = 1";
        if (SqlHelper.Like(query.Search) is { } like)
        {
            where += " AND (code ILIKE @like OR name ILIKE @like OR city ILIKE @like OR state ILIKE @like)";
            p.Add("like", like);
        }

        await using var conn = await _db.OpenAsync();
        return await SqlHelper.PageAsync<DealerDto>(conn, "SELECT " + Columns, where, SqlHelper.OrderBy(query, SortColumns, "code"), query, p);
    }

    public async Task<DealerDto?> GetByIdAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<DealerDto>($"SELECT {Columns} FROM dealers WHERE id = @id", new { id });
    }

    public async Task<bool> CodeExistsAsync(string code, int? excludeId = null)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM dealers WHERE lower(code) = lower(@code) AND (@excludeId::int IS NULL OR id <> @excludeId))",
            new { code, excludeId });
    }

    public async Task<int> CreateAsync(DealerSaveRequest r)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO dealers (code, name, city, state, contact_phone, email, is_active)
              VALUES (@Code, @Name, @City, @State, @ContactPhone, @Email, @IsActive) RETURNING id", r);
    }

    public async Task<bool> UpdateAsync(int id, DealerSaveRequest r)
    {
        await using var conn = await _db.OpenAsync();
        var rows = await conn.ExecuteAsync(
            @"UPDATE dealers SET code = @Code, name = @Name, city = @City, state = @State, contact_phone = @ContactPhone,
                     email = @Email, is_active = @IsActive, updated_at = now()
              WHERE id = @id",
            new { id, r.Code, r.Name, r.City, r.State, r.ContactPhone, r.Email, r.IsActive });
        return rows > 0;
    }

    public async Task<IReadOnlyList<LookupItem>> LookupAsync()
    {
        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync<LookupItem>("SELECT id, code, name FROM dealers WHERE is_active ORDER BY name")).ToList();
    }
}

public class AuditRepository : IAuditRepository
{
    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["createdAt"] = "created_at",
        ["entityType"] = "entity_type",
        ["action"] = "action",
        ["username"] = "username"
    };

    private readonly IDbConnectionFactory _db;

    public AuditRepository(IDbConnectionFactory db) => _db = db;

    public async Task LogAsync(string entityType, int entityId, string action, string? details, int? userId, string? username)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecuteAsync(
            @"INSERT INTO audit_logs (entity_type, entity_id, action, details, user_id, username)
              VALUES (@entityType, @entityId, @action, @details, @userId, @username)",
            new { entityType, entityId, action, details, userId, username });
    }

    public async Task<PagedResult<AuditLogDto>> SearchAsync(AuditQuery query)
    {
        var p = new DynamicParameters();
        var where = " FROM audit_logs WHERE 1 = 1";
        if (SqlHelper.Like(query.Search) is { } like)
        {
            where += " AND (details ILIKE @like OR username ILIKE @like OR action ILIKE @like)";
            p.Add("like", like);
        }
        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            where += " AND entity_type = @entityType";
            p.Add("entityType", query.EntityType);
        }
        if (query.EntityId.HasValue)
        {
            where += " AND entity_id = @entityId";
            p.Add("entityId", query.EntityId);
        }
        if (query.From.HasValue)
        {
            where += " AND created_at >= @from";
            p.Add("from", query.From.Value.Date);
        }
        if (query.To.HasValue)
        {
            where += " AND created_at < @to";
            p.Add("to", query.To.Value.Date.AddDays(1));
        }

        // Default to newest first unless the caller chose a direction.
        query.SortDir ??= "desc";
        await using var conn = await _db.OpenAsync();
        return await SqlHelper.PageAsync<AuditLogDto>(conn,
            "SELECT id, entity_type, entity_id, action, details, user_id, username, created_at",
            where, SqlHelper.OrderBy(query, SortColumns, "created_at") + ", id DESC", query, p);
    }
}
