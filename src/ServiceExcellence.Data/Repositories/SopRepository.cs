using Dapper;
using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Core.Interfaces;

namespace ServiceExcellence.Data.Repositories;

public class SopRepository : ISopRepository
{
    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "s.sop_code",
        ["title"] = "s.title",
        ["version"] = "s.version",
        ["activity"] = "s.activity_type",
        ["status"] = "s.status",
        ["division"] = "d.name",
        ["appliesTo"] = "COALESCE(a.name, v.name, m.name, c.name, d.name)",
        ["time"] = "s.standard_time_minutes",
        ["createdAt"] = "s.created_at",
        ["updatedAt"] = "COALESCE(s.updated_at, s.created_at)",
        ["publishedAt"] = "s.published_at"
    };

    private const string ListSelect = @"
        SELECT s.id, s.sop_code, s.version, s.title, s.activity_type, s.status,
               d.name AS division_name, c.name AS category_name, m.name AS model_name, v.name AS variant_name, a.name AS assembly_name,
               s.standard_time_minutes, cu.full_name AS created_by_name, s.created_at, s.updated_at, s.published_at";

    private const string ListFrom = @"
        FROM sops s
        JOIN divisions d ON d.id = s.division_id
        LEFT JOIN model_categories c ON c.id = s.category_id
        LEFT JOIN models m ON m.id = s.model_id
        LEFT JOIN variants v ON v.id = s.variant_id
        LEFT JOIN assemblies a ON a.id = s.assembly_id
        LEFT JOIN users cu ON cu.id = s.created_by";

    private const string StepColumns =
        "id, sop_id, step_no, title, instruction, tools_required, specification, caution, estimated_minutes";

    private const string AttachmentSelect = @"
        SELECT at.id, at.step_id, at.file_name, at.stored_name, at.content_type, at.size_bytes,
               u.full_name AS uploaded_by_name, at.uploaded_at
        FROM sop_step_attachments at LEFT JOIN users u ON u.id = at.uploaded_by";

    private readonly IDbConnectionFactory _db;

    public SopRepository(IDbConnectionFactory db) => _db = db;

    private sealed class StepPosition
    {
        public int SopId { get; set; }
        public int StepNo { get; set; }
    }

    public async Task<PagedResult<SopListItemDto>> SearchAsync(SopQuery query, bool publishedOnly)
    {
        var p = new DynamicParameters();
        var where = ListFrom + " WHERE 1 = 1";
        if (SqlHelper.Like(query.Search) is { } like)
        {
            where += " AND (s.sop_code ILIKE @like OR s.title ILIKE @like OR s.purpose ILIKE @like)";
            p.Add("like", like);
        }
        if (publishedOnly)
        {
            where += " AND s.status = @status";
            p.Add("status", SopStatus.Published);
        }
        else if (!string.IsNullOrWhiteSpace(query.Status))
        {
            where += " AND s.status = @status";
            p.Add("status", query.Status);
        }
        if (!string.IsNullOrWhiteSpace(query.ActivityType))
        {
            where += " AND s.activity_type = @activityType";
            p.Add("activityType", query.ActivityType);
        }
        AddExact(ref where, p, "s.division_id", "divisionId", query.DivisionId);
        AddExact(ref where, p, "s.category_id", "categoryId", query.CategoryId);
        AddExact(ref where, p, "s.model_id", "modelId", query.ModelId);
        AddExact(ref where, p, "s.variant_id", "variantId", query.VariantId);

        await using var conn = await _db.OpenAsync();
        return await SqlHelper.PageAsync<SopListItemDto>(conn, ListSelect, where,
            SqlHelper.OrderBy(query, SortColumns, "COALESCE(s.updated_at, s.created_at)"), query, p);
    }

    private static void AddExact(ref string where, DynamicParameters p, string column, string name, int? value)
    {
        if (!value.HasValue) return;
        where += $" AND {column} = @{name}";
        p.Add(name, value);
    }

    public async Task<PagedResult<SopListItemDto>> FindApplicableAsync(ClassificationPath path, SopFinderQuery query)
    {
        var p = new DynamicParameters();
        p.Add("status", SopStatus.Published);
        p.Add("divisionId", path.DivisionId);

        // An SOP applies when every level it is defined at matches the selected product. Levels left empty on
        // the SOP are inherited (it applies to everything below). Levels below the selection are not constrained.
        var where = ListFrom + " WHERE s.status = @status AND s.division_id = @divisionId";
        AddInherited(ref where, p, "s.category_id", "categoryId", path.CategoryId);
        AddInherited(ref where, p, "s.model_id", "modelId", path.ModelId);
        AddInherited(ref where, p, "s.variant_id", "variantId", path.VariantId);

        if (path.AssemblyId.HasValue)
        {
            // For a selected assembly include SOPs on the assembly itself, its parents and its children.
            where += @" AND (s.assembly_id IS NULL OR s.assembly_id IN (
                            WITH RECURSIVE up AS (
                                SELECT id, parent_id FROM assemblies WHERE id = @assemblyId
                                UNION ALL
                                SELECT x.id, x.parent_id FROM assemblies x JOIN up ON x.id = up.parent_id),
                            down AS (
                                SELECT id FROM assemblies WHERE id = @assemblyId
                                UNION ALL
                                SELECT x.id FROM assemblies x JOIN down ON x.parent_id = down.id)
                            SELECT id FROM up UNION SELECT id FROM down))";
            p.Add("assemblyId", path.AssemblyId);
        }

        if (SqlHelper.Like(query.Search) is { } like)
        {
            where += " AND (s.sop_code ILIKE @like OR s.title ILIKE @like OR s.purpose ILIKE @like)";
            p.Add("like", like);
        }
        if (!string.IsNullOrWhiteSpace(query.ActivityType))
        {
            where += " AND s.activity_type = @activityType";
            p.Add("activityType", query.ActivityType);
        }

        await using var conn = await _db.OpenAsync();
        return await SqlHelper.PageAsync<SopListItemDto>(conn, ListSelect, where,
            SqlHelper.OrderBy(query, SortColumns, "s.sop_code"), query, p);
    }

    private static void AddInherited(ref string where, DynamicParameters p, string column, string name, int? value)
    {
        if (!value.HasValue) return;
        where += $" AND ({column} IS NULL OR {column} = @{name})";
        p.Add(name, value);
    }

    public async Task<SopDetailDto?> GetByIdAsync(int id)
    {
        var sql = $@"
            {ListSelect}, s.purpose, s.division_id, s.category_id, s.model_id, s.variant_id, s.assembly_id,
               s.skill_level, s.safety_notes, s.review_remarks, s.previous_version_id, s.created_by,
               uu.full_name AS updated_by_name, s.submitted_at, au.full_name AS approved_by_name, s.approved_at
            {ListFrom}
            LEFT JOIN users uu ON uu.id = s.updated_by
            LEFT JOIN users au ON au.id = s.approved_by
            WHERE s.id = @id;

            SELECT {StepColumns} FROM sop_steps WHERE sop_id = @id ORDER BY step_no, id;

            SELECT id, sop_id, resource_type, part_number, description, quantity, uom
            FROM sop_resources WHERE sop_id = @id ORDER BY resource_type, id;

            {AttachmentSelect}
            JOIN sop_steps st ON st.id = at.step_id
            WHERE st.sop_id = @id ORDER BY at.id;

            WITH RECURSIVE up AS (
                SELECT a.id, a.parent_id, a.name::text AS path FROM assemblies a
                WHERE a.id = (SELECT assembly_id FROM sops WHERE id = @id)
                UNION ALL
                SELECT x.id, x.parent_id, x.name || ' / ' || up.path FROM assemblies x JOIN up ON x.id = up.parent_id)
            SELECT path FROM up WHERE parent_id IS NULL;";

        await using var conn = await _db.OpenAsync();
        using var multi = await conn.QueryMultipleAsync(sql, new { id });
        var sop = await multi.ReadSingleOrDefaultAsync<SopDetailDto>();
        if (sop == null) return null;

        sop.Steps = (await multi.ReadAsync<SopStepDto>()).ToList();
        sop.Resources = (await multi.ReadAsync<SopResourceDto>()).ToList();
        var attachments = (await multi.ReadAsync<AttachmentDto>()).ToLookup(a => a.StepId);
        foreach (var step in sop.Steps)
            step.Attachments = attachments[step.Id].ToList();
        sop.AssemblyPath = await multi.ReadSingleOrDefaultAsync<string>();
        return sop;
    }

    public async Task<SopListItemDto?> GetSummaryAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<SopListItemDto>($"{ListSelect} {ListFrom} WHERE s.id = @id", new { id });
    }

    public async Task<bool> CodeExistsAsync(string sopCode, int? excludeId = null)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<bool>(
            @"SELECT EXISTS (SELECT 1 FROM sops WHERE lower(sop_code) = lower(@sopCode)
                             AND (@excludeId::int IS NULL OR id <> @excludeId))",
            new { sopCode, excludeId });
    }

    public async Task<bool> HasOpenVersionAsync(string sopCode)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM sops WHERE sop_code = @sopCode AND status = ANY(@open))",
            new { sopCode, open = new[] { SopStatus.Draft, SopStatus.UnderReview, SopStatus.Approved, SopStatus.Rejected } });
    }

    public async Task<int> CreateAsync(SopSaveRequest r, ClassificationPath path, int userId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO sops (sop_code, version, title, activity_type, purpose, division_id, category_id, model_id, variant_id,
                                assembly_id, standard_time_minutes, skill_level, safety_notes, status, created_by)
              VALUES (@SopCode, 1, @Title, @ActivityType, @Purpose, @DivisionId, @CategoryId, @ModelId, @VariantId,
                      @AssemblyId, @StandardTimeMinutes, @SkillLevel, @SafetyNotes, @status, @userId)
              RETURNING id",
            new
            {
                r.SopCode, r.Title, r.ActivityType, r.Purpose,
                path.DivisionId, path.CategoryId, path.ModelId, path.VariantId, path.AssemblyId,
                r.StandardTimeMinutes, r.SkillLevel, r.SafetyNotes, status = SopStatus.Draft, userId
            });
    }

    public async Task<bool> UpdateAsync(int id, SopSaveRequest r, ClassificationPath path, int userId)
    {
        await using var conn = await _db.OpenAsync();
        var rows = await conn.ExecuteAsync(
            @"UPDATE sops SET title = @Title, activity_type = @ActivityType, purpose = @Purpose,
                     division_id = @DivisionId, category_id = @CategoryId, model_id = @ModelId, variant_id = @VariantId,
                     assembly_id = @AssemblyId, standard_time_minutes = @StandardTimeMinutes, skill_level = @SkillLevel,
                     safety_notes = @SafetyNotes, updated_by = @userId, updated_at = now()
              WHERE id = @id",
            new
            {
                id, r.Title, r.ActivityType, r.Purpose,
                path.DivisionId, path.CategoryId, path.ModelId, path.VariantId, path.AssemblyId,
                r.StandardTimeMinutes, r.SkillLevel, r.SafetyNotes, userId
            });
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        // A later version may point at this one; detach it so the delete is allowed.
        await conn.ExecuteAsync("UPDATE sops SET previous_version_id = NULL WHERE previous_version_id = @id", new { id }, tx);
        var rows = await conn.ExecuteAsync("DELETE FROM sops WHERE id = @id", new { id }, tx);
        await tx.CommitAsync();
        return rows > 0;
    }

    public async Task<bool> ChangeStatusAsync(int id, string[] fromStatuses, string toStatus, int userId, string? remarks)
    {
        await using var conn = await _db.OpenAsync();
        var rows = await conn.ExecuteAsync(
            @"UPDATE sops SET
                     status = @toStatus,
                     review_remarks = COALESCE(@remarks, review_remarks),
                     submitted_at = CASE WHEN @toStatus = 'UnderReview' THEN now() ELSE submitted_at END,
                     approved_by  = CASE WHEN @toStatus = 'Approved' THEN @userId ELSE approved_by END,
                     approved_at  = CASE WHEN @toStatus = 'Approved' THEN now() ELSE approved_at END,
                     updated_at = now()
              WHERE id = @id AND status = ANY(@fromStatuses)",
            new { id, fromStatuses, toStatus, userId, remarks });
        return rows > 0;
    }

    public async Task<bool> PublishAsync(int id, int userId)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var rows = await conn.ExecuteAsync(
            "UPDATE sops SET status = @published, published_at = now(), updated_at = now() WHERE id = @id AND status = @approved",
            new { id, published = SopStatus.Published, approved = SopStatus.Approved }, tx);
        if (rows == 0)
        {
            await tx.RollbackAsync();
            return false;
        }

        await conn.ExecuteAsync(
            @"UPDATE sops SET status = @obsolete, updated_by = @userId, updated_at = now()
              WHERE sop_code = (SELECT sop_code FROM sops WHERE id = @id) AND id <> @id AND status = @published",
            new { id, userId, obsolete = SopStatus.Obsolete, published = SopStatus.Published }, tx);

        await tx.CommitAsync();
        return true;
    }

    public async Task<int> CreateNewVersionAsync(int id, int userId)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var newId = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO sops (sop_code, version, title, activity_type, purpose, division_id, category_id, model_id, variant_id,
                                assembly_id, standard_time_minutes, skill_level, safety_notes, status, previous_version_id, created_by)
              SELECT s.sop_code, (SELECT MAX(version) + 1 FROM sops WHERE sop_code = s.sop_code), s.title, s.activity_type,
                     s.purpose, s.division_id, s.category_id, s.model_id, s.variant_id, s.assembly_id,
                     s.standard_time_minutes, s.skill_level, s.safety_notes, @draft, s.id, @userId
              FROM sops s WHERE s.id = @id
              RETURNING id",
            new { id, userId, draft = SopStatus.Draft }, tx);

        await conn.ExecuteAsync(
            @"INSERT INTO sop_steps (sop_id, step_no, title, instruction, tools_required, specification, caution, estimated_minutes)
              SELECT @newId, step_no, title, instruction, tools_required, specification, caution, estimated_minutes
              FROM sop_steps WHERE sop_id = @id;

              INSERT INTO sop_resources (sop_id, resource_type, part_number, description, quantity, uom)
              SELECT @newId, resource_type, part_number, description, quantity, uom
              FROM sop_resources WHERE sop_id = @id;

              -- Attachments are shared by file name; steps are matched on their (unique) step number.
              INSERT INTO sop_step_attachments (step_id, file_name, stored_name, content_type, size_bytes, uploaded_by, uploaded_at)
              SELECT ns.id, at.file_name, at.stored_name, at.content_type, at.size_bytes, at.uploaded_by, at.uploaded_at
              FROM sop_step_attachments at
              JOIN sop_steps os ON os.id = at.step_id AND os.sop_id = @id
              JOIN sop_steps ns ON ns.sop_id = @newId AND ns.step_no = os.step_no;",
            new { id, newId }, tx);

        await tx.CommitAsync();
        return newId;
    }

    // -----------------------------------------------------------------------
    // Steps
    // -----------------------------------------------------------------------

    public async Task<SopStepDto?> GetStepAsync(int stepId)
    {
        await using var conn = await _db.OpenAsync();
        var step = await conn.QuerySingleOrDefaultAsync<SopStepDto>($"SELECT {StepColumns} FROM sop_steps WHERE id = @stepId", new { stepId });
        if (step != null)
            step.Attachments = (await conn.QueryAsync<AttachmentDto>(AttachmentSelect + " WHERE at.step_id = @stepId ORDER BY at.id", new { stepId })).ToList();
        return step;
    }

    public async Task<int> AddStepAsync(int sopId, SopStepSaveRequest r)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        // Lock the SOP row so concurrent inserts cannot produce duplicate step numbers.
        await conn.ExecuteAsync("SELECT id FROM sops WHERE id = @sopId FOR UPDATE", new { sopId }, tx);
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sop_steps WHERE sop_id = @sopId", new { sopId }, tx);

        var stepNo = r.StepNo <= 0 || r.StepNo > count ? count + 1 : r.StepNo;
        if (stepNo <= count)
        {
            await conn.ExecuteAsync("UPDATE sop_steps SET step_no = step_no + 1 WHERE sop_id = @sopId AND step_no >= @stepNo",
                new { sopId, stepNo }, tx);
        }

        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO sop_steps (sop_id, step_no, title, instruction, tools_required, specification, caution, estimated_minutes)
              VALUES (@sopId, @stepNo, @Title, @Instruction, @ToolsRequired, @Specification, @Caution, @EstimatedMinutes)
              RETURNING id",
            new { sopId, stepNo, r.Title, r.Instruction, r.ToolsRequired, r.Specification, r.Caution, r.EstimatedMinutes }, tx);

        await tx.CommitAsync();
        return id;
    }

    public async Task<bool> UpdateStepAsync(int stepId, SopStepSaveRequest r)
    {
        await using var conn = await _db.OpenAsync();
        var rows = await conn.ExecuteAsync(
            @"UPDATE sop_steps SET title = @Title, instruction = @Instruction, tools_required = @ToolsRequired,
                     specification = @Specification, caution = @Caution, estimated_minutes = @EstimatedMinutes
              WHERE id = @stepId",
            new { stepId, r.Title, r.Instruction, r.ToolsRequired, r.Specification, r.Caution, r.EstimatedMinutes });
        return rows > 0;
    }

    public async Task<bool> DeleteStepAsync(int stepId)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var deleted = await conn.QuerySingleOrDefaultAsync<StepPosition>(
            "DELETE FROM sop_steps WHERE id = @stepId RETURNING sop_id, step_no", new { stepId }, tx);
        if (deleted == null)
        {
            await tx.RollbackAsync();
            return false;
        }

        await conn.ExecuteAsync("UPDATE sop_steps SET step_no = step_no - 1 WHERE sop_id = @SopId AND step_no > @StepNo",
            new { deleted.SopId, deleted.StepNo }, tx);
        await tx.CommitAsync();
        return true;
    }

    public async Task<bool> MoveStepAsync(int stepId, int direction)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var step = await conn.QuerySingleOrDefaultAsync<StepPosition>(
            "SELECT sop_id, step_no FROM sop_steps WHERE id = @stepId FOR UPDATE", new { stepId }, tx);
        if (step == null)
        {
            await tx.RollbackAsync();
            return false;
        }

        var target = step.StepNo + Math.Sign(direction);
        var swapped = await conn.ExecuteAsync(
            "UPDATE sop_steps SET step_no = @from WHERE sop_id = @sopId AND step_no = @target",
            new { sopId = step.SopId, from = step.StepNo, target }, tx);
        if (swapped == 0)
        {
            // Already first/last: nothing to move.
            await tx.RollbackAsync();
            return false;
        }

        await conn.ExecuteAsync("UPDATE sop_steps SET step_no = @target WHERE id = @stepId", new { stepId, target }, tx);
        await tx.CommitAsync();
        return true;
    }

    // -----------------------------------------------------------------------
    // Resources
    // -----------------------------------------------------------------------

    public async Task<SopResourceDto?> GetResourceAsync(int resourceId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<SopResourceDto>(
            "SELECT id, sop_id, resource_type, part_number, description, quantity, uom FROM sop_resources WHERE id = @resourceId",
            new { resourceId });
    }

    public async Task<int> AddResourceAsync(int sopId, SopResourceSaveRequest r)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO sop_resources (sop_id, resource_type, part_number, description, quantity, uom)
              VALUES (@sopId, @ResourceType, @PartNumber, @Description, @Quantity, @Uom) RETURNING id",
            new { sopId, r.ResourceType, r.PartNumber, r.Description, r.Quantity, r.Uom });
    }

    public async Task<bool> DeleteResourceAsync(int resourceId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync("DELETE FROM sop_resources WHERE id = @resourceId", new { resourceId }) > 0;
    }

    // -----------------------------------------------------------------------
    // Attachments
    // -----------------------------------------------------------------------

    public async Task<AttachmentDto?> GetAttachmentAsync(int attachmentId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<AttachmentDto>(AttachmentSelect + " WHERE at.id = @attachmentId", new { attachmentId });
    }

    public async Task<int> AddAttachmentAsync(AttachmentDto a, int userId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO sop_step_attachments (step_id, file_name, stored_name, content_type, size_bytes, uploaded_by)
              VALUES (@StepId, @FileName, @StoredName, @ContentType, @SizeBytes, @userId) RETURNING id",
            new { a.StepId, a.FileName, a.StoredName, a.ContentType, a.SizeBytes, userId });
    }

    public async Task<bool> DeleteAttachmentAsync(int attachmentId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync("DELETE FROM sop_step_attachments WHERE id = @attachmentId", new { attachmentId }) > 0;
    }

    public async Task<int> CountAttachmentReferencesAsync(string storedName)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sop_step_attachments WHERE stored_name = @storedName", new { storedName });
    }

    public async Task TouchAsync(int sopId, int userId)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecuteAsync("UPDATE sops SET updated_by = @userId, updated_at = now() WHERE id = @sopId", new { sopId, userId });
    }

    // -----------------------------------------------------------------------
    // Dashboard
    // -----------------------------------------------------------------------

    public async Task<DashboardDto> GetDashboardAsync(bool publishedOnly)
    {
        var sql = $@"
            SELECT (SELECT COUNT(*) FROM divisions WHERE is_active) AS divisions,
                   (SELECT COUNT(*) FROM models WHERE is_active) AS models,
                   (SELECT COUNT(*) FROM variants WHERE is_active) AS variants,
                   (SELECT COUNT(*) FROM dealers WHERE is_active) AS dealers;

            SELECT status AS key, COUNT(*)::int AS value FROM sops
            {(publishedOnly ? "WHERE status = 'Published'" : "")} GROUP BY status;

            SELECT activity_type AS key, COUNT(*)::int AS value FROM sops WHERE status = 'Published' GROUP BY activity_type;

            {ListSelect} {ListFrom}
            {(publishedOnly ? "WHERE s.status = 'Published'" : "")}
            ORDER BY COALESCE(s.updated_at, s.created_at) DESC LIMIT 8;";

        await using var conn = await _db.OpenAsync();
        using var multi = await conn.QueryMultipleAsync(sql);
        var dashboard = await multi.ReadSingleAsync<DashboardDto>();
        dashboard.SopsByStatus = (await multi.ReadAsync<(string Key, int Value)>()).ToDictionary(x => x.Key, x => x.Value);
        dashboard.PublishedByActivity = (await multi.ReadAsync<(string Key, int Value)>()).ToDictionary(x => x.Key, x => x.Value);
        dashboard.RecentSops = (await multi.ReadAsync<SopListItemDto>()).ToList();
        return dashboard;
    }
}
