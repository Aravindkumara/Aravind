using System.Security.Claims;
using ServiceExcellence.Api.Infrastructure;
using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Core.Interfaces;

namespace ServiceExcellence.Api.Services;

/// <summary>Business rules for SOPs: classification, editing, the approval workflow, attachments and audit.</summary>
public class SopService
{
    private readonly ISopRepository _sops;
    private readonly IMasterRepository _masters;
    private readonly IAssemblyRepository _assemblies;
    private readonly IFileStorage _files;
    private readonly IAuditService _audit;

    public SopService(ISopRepository sops, IMasterRepository masters, IAssemblyRepository assemblies, IFileStorage files, IAuditService audit)
    {
        _sops = sops;
        _masters = masters;
        _assemblies = assemblies;
        _files = files;
        _audit = audit;
    }

    // -----------------------------------------------------------------------
    // Classification
    // -----------------------------------------------------------------------

    /// <summary>
    /// Resolves the full classification path from the most specific level supplied and checks that any other
    /// supplied levels agree with it (e.g. the chosen model really belongs to the chosen category).
    /// </summary>
    public async Task<ClassificationPath> ResolveClassificationAsync(int? divisionId, int? categoryId, int? modelId, int? variantId, int? assemblyId)
    {
        ClassificationPath? path;
        string field;
        if (assemblyId.HasValue) { path = await _assemblies.ResolvePathAsync(assemblyId.Value); field = "AssemblyId"; }
        else if (variantId.HasValue) { path = await _masters.ResolvePathAsync(MasterType.Variant, variantId.Value); field = "VariantId"; }
        else if (modelId.HasValue) { path = await _masters.ResolvePathAsync(MasterType.Model, modelId.Value); field = "ModelId"; }
        else if (categoryId.HasValue) { path = await _masters.ResolvePathAsync(MasterType.Category, categoryId.Value); field = "CategoryId"; }
        else if (divisionId.HasValue) { path = await _masters.ResolvePathAsync(MasterType.Division, divisionId.Value); field = "DivisionId"; }
        else throw new ValidationException("Select at least a division.", "DivisionId");

        if (path == null)
            throw new ValidationException("The selected classification does not exist.", field);

        if ((divisionId.HasValue && divisionId != path.DivisionId) ||
            (categoryId.HasValue && categoryId != path.CategoryId) ||
            (modelId.HasValue && modelId != path.ModelId) ||
            (variantId.HasValue && variantId != path.VariantId))
        {
            throw new ValidationException("The selected division, category, model, variant and assembly do not belong together.", field);
        }

        return path;
    }

    // -----------------------------------------------------------------------
    // Read
    // -----------------------------------------------------------------------

    public async Task<SopDetailDto> GetAsync(int id, ClaimsPrincipal user)
    {
        var sop = await _sops.GetByIdAsync(id);
        // Dealers only ever see published SOPs; anything else is reported as not found.
        if (sop == null || (user.IsDealer() && sop.Status != SopStatus.Published))
            throw new NotFoundException("SOP not found.");
        return sop;
    }

    public async Task<PagedResult<SopListItemDto>> FindApplicableAsync(SopFinderQuery query)
    {
        var path = await ResolveClassificationAsync(query.DivisionId, query.CategoryId, query.ModelId, query.VariantId, query.AssemblyId);
        return await _sops.FindApplicableAsync(path, query);
    }

    // -----------------------------------------------------------------------
    // Create / update / delete
    // -----------------------------------------------------------------------

    public async Task<int> CreateAsync(SopSaveRequest request, int userId)
    {
        ValidateActivity(request.ActivityType);
        request.SopCode = request.SopCode.Trim().ToUpperInvariant();
        if (await _sops.CodeExistsAsync(request.SopCode))
            throw new ValidationException($"SOP code '{request.SopCode}' already exists.", nameof(request.SopCode));

        var path = await ResolveClassificationAsync(request.DivisionId, request.CategoryId, request.ModelId, request.VariantId, request.AssemblyId);
        var id = await _sops.CreateAsync(request, path, userId);
        await _audit.LogAsync(AuditEntities.Sop, id, "Created", $"{request.SopCode} v1 created: {request.Title}");
        return id;
    }

    public async Task UpdateAsync(int id, SopSaveRequest request, int userId)
    {
        var sop = await GetEditableAsync(id);
        ValidateActivity(request.ActivityType);
        var path = await ResolveClassificationAsync(request.DivisionId, request.CategoryId, request.ModelId, request.VariantId, request.AssemblyId);

        // The SOP code identifies all versions of a procedure and cannot be changed after creation.
        await _sops.UpdateAsync(id, request, path, userId);
        await _audit.LogAsync(AuditEntities.Sop, id, "Updated", $"{sop.SopCode} v{sop.Version} header updated");
    }

    public async Task DeleteAsync(int id)
    {
        var sop = await _sops.GetByIdAsync(id) ?? throw new NotFoundException("SOP not found.");
        if (sop.Status != SopStatus.Draft)
            throw new ConflictException("Only draft SOPs can be deleted.");

        var storedNames = sop.Steps.SelectMany(s => s.Attachments).Select(a => a.StoredName).Distinct().ToList();
        await _sops.DeleteAsync(id);
        await DeleteUnreferencedFilesAsync(storedNames);
        await _audit.LogAsync(AuditEntities.Sop, id, "Deleted", $"{sop.SopCode} v{sop.Version} deleted");
    }

    private static void ValidateActivity(string activityType)
    {
        if (!ActivityTypes.All.ContainsKey(activityType))
            throw new ValidationException("Select a valid service activity.", nameof(SopSaveRequest.ActivityType));
    }

    private async Task<SopDetailDto> GetEditableAsync(int id)
    {
        var sop = await _sops.GetByIdAsync(id) ?? throw new NotFoundException("SOP not found.");
        if (!SopStatus.IsEditable(sop.Status))
            throw new ConflictException($"The SOP is {SopStatus.Display(sop.Status)} and can no longer be edited. Create a new version instead.");
        return sop;
    }

    // -----------------------------------------------------------------------
    // Workflow
    // -----------------------------------------------------------------------

    public async Task SubmitAsync(int id, int userId)
    {
        var sop = await GetEditableAsync(id);
        if (sop.Steps.Count == 0)
            throw new ValidationException("Add at least one step before submitting the SOP for review.");

        await ChangeStatusAsync(sop, new[] { SopStatus.Draft, SopStatus.Rejected }, SopStatus.UnderReview, userId, null, "Submitted");
    }

    public async Task ApproveAsync(int id, ClaimsPrincipal user, string? remarks)
    {
        var sop = await GetForReviewAsync(id, user);
        await ChangeStatusAsync(sop, new[] { SopStatus.UnderReview }, SopStatus.Approved, user.GetUserId(), remarks, "Approved");
    }

    public async Task RejectAsync(int id, ClaimsPrincipal user, string? remarks)
    {
        if (string.IsNullOrWhiteSpace(remarks))
            throw new ValidationException("Enter remarks explaining why the SOP is rejected.", nameof(WorkflowActionRequest.Remarks));

        var sop = await GetForReviewAsync(id, user);
        await ChangeStatusAsync(sop, new[] { SopStatus.UnderReview }, SopStatus.Rejected, user.GetUserId(), remarks, "Rejected");
    }

    public async Task PublishAsync(int id, int userId)
    {
        var sop = await _sops.GetSummaryAsync(id) ?? throw new NotFoundException("SOP not found.");
        if (!await _sops.PublishAsync(id, userId))
            throw new ConflictException($"Only approved SOPs can be published (current status: {SopStatus.Display(sop.Status)}).");

        await _audit.LogAsync(AuditEntities.Sop, id, "Published",
            $"{sop.SopCode} v{sop.Version} published; earlier published versions marked obsolete");
    }

    public async Task MarkObsoleteAsync(int id, int userId, string? remarks)
    {
        var sop = await _sops.GetSummaryAsync(id) ?? throw new NotFoundException("SOP not found.");
        await ChangeStatusAsync(sop, new[] { SopStatus.Published }, SopStatus.Obsolete, userId, remarks, "Obsoleted");
    }

    public async Task<int> CreateNewVersionAsync(int id, int userId)
    {
        var sop = await _sops.GetSummaryAsync(id) ?? throw new NotFoundException("SOP not found.");
        if (sop.Status is not (SopStatus.Published or SopStatus.Obsolete))
            throw new ConflictException("A new version can only be created from a published or obsolete SOP.");
        if (await _sops.HasOpenVersionAsync(sop.SopCode))
            throw new ConflictException($"{sop.SopCode} already has a version in progress (draft, review or approved). Finish that one first.");

        var newId = await _sops.CreateNewVersionAsync(id, userId);
        var created = await _sops.GetSummaryAsync(newId);
        await _audit.LogAsync(AuditEntities.Sop, newId, "VersionCreated",
            $"{sop.SopCode} v{created?.Version} created from v{sop.Version}");
        return newId;
    }

    /// <summary>Reviewers may not approve or reject SOPs they authored themselves (administrators excepted).</summary>
    private async Task<SopListItemDto> GetForReviewAsync(int id, ClaimsPrincipal user)
    {
        var sop = await _sops.GetByIdAsync(id) ?? throw new NotFoundException("SOP not found.");
        if (!user.IsAdmin() && sop.CreatedBy == user.GetUserId())
            throw new ForbiddenException("You cannot review an SOP you created.");
        return sop;
    }

    private async Task ChangeStatusAsync(SopListItemDto sop, string[] from, string to, int userId, string? remarks, string action)
    {
        if (!await _sops.ChangeStatusAsync(sop.Id, from, to, userId, string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim()))
            throw new ConflictException($"The SOP cannot be {action.ToLowerInvariant()} while it is {SopStatus.Display(sop.Status)}.");

        var details = $"{sop.SopCode} v{sop.Version}: {SopStatus.Display(sop.Status)} -> {SopStatus.Display(to)}";
        if (!string.IsNullOrWhiteSpace(remarks)) details += $". Remarks: {remarks.Trim()}";
        await _audit.LogAsync(AuditEntities.Sop, sop.Id, action, details);
    }

    // -----------------------------------------------------------------------
    // Steps
    // -----------------------------------------------------------------------

    public async Task<int> AddStepAsync(int sopId, SopStepSaveRequest request, int userId)
    {
        var sop = await GetEditableAsync(sopId);
        var stepId = await _sops.AddStepAsync(sopId, request);
        await _sops.TouchAsync(sopId, userId);
        await _audit.LogAsync(AuditEntities.Sop, sopId, "StepAdded", $"{sop.SopCode} v{sop.Version}: step '{request.Title}' added");
        return stepId;
    }

    public async Task UpdateStepAsync(int sopId, int stepId, SopStepSaveRequest request, int userId)
    {
        var (sop, step) = await GetEditableStepAsync(sopId, stepId);
        await _sops.UpdateStepAsync(stepId, request);
        await _sops.TouchAsync(sopId, userId);
        await _audit.LogAsync(AuditEntities.Sop, sopId, "StepUpdated", $"{sop.SopCode} v{sop.Version}: step {step.StepNo} '{request.Title}' updated");
    }

    public async Task DeleteStepAsync(int sopId, int stepId, int userId)
    {
        var (sop, step) = await GetEditableStepAsync(sopId, stepId);
        await _sops.DeleteStepAsync(stepId);
        await DeleteUnreferencedFilesAsync(step.Attachments.Select(a => a.StoredName));
        await _sops.TouchAsync(sopId, userId);
        await _audit.LogAsync(AuditEntities.Sop, sopId, "StepDeleted", $"{sop.SopCode} v{sop.Version}: step {step.StepNo} '{step.Title}' deleted");
    }

    public async Task MoveStepAsync(int sopId, int stepId, int direction, int userId)
    {
        var (sop, step) = await GetEditableStepAsync(sopId, stepId);
        if (await _sops.MoveStepAsync(stepId, direction))
        {
            await _sops.TouchAsync(sopId, userId);
            await _audit.LogAsync(AuditEntities.Sop, sopId, "StepMoved",
                $"{sop.SopCode} v{sop.Version}: step '{step.Title}' moved {(direction < 0 ? "up" : "down")}");
        }
    }

    public async Task<SopStepDto> GetStepAsync(int sopId, int stepId, ClaimsPrincipal user)
    {
        await GetAsync(sopId, user);
        var step = await _sops.GetStepAsync(stepId);
        if (step == null || step.SopId != sopId) throw new NotFoundException("Step not found.");
        return step;
    }

    private async Task<(SopDetailDto Sop, SopStepDto Step)> GetEditableStepAsync(int sopId, int stepId)
    {
        var sop = await GetEditableAsync(sopId);
        var step = sop.Steps.FirstOrDefault(s => s.Id == stepId) ?? throw new NotFoundException("Step not found.");
        return (sop, step);
    }

    // -----------------------------------------------------------------------
    // Resources
    // -----------------------------------------------------------------------

    public async Task<int> AddResourceAsync(int sopId, SopResourceSaveRequest request, int userId)
    {
        if (!ResourceTypes.All.Contains(request.ResourceType))
            throw new ValidationException("Select Part, Tool or Consumable.", nameof(request.ResourceType));

        var sop = await GetEditableAsync(sopId);
        var id = await _sops.AddResourceAsync(sopId, request);
        await _sops.TouchAsync(sopId, userId);
        await _audit.LogAsync(AuditEntities.Sop, sopId, "ResourceAdded",
            $"{sop.SopCode} v{sop.Version}: {request.ResourceType} '{request.Description}' x {request.Quantity} added");
        return id;
    }

    public async Task DeleteResourceAsync(int sopId, int resourceId, int userId)
    {
        var sop = await GetEditableAsync(sopId);
        var resource = sop.Resources.FirstOrDefault(r => r.Id == resourceId) ?? throw new NotFoundException("Resource not found.");
        await _sops.DeleteResourceAsync(resourceId);
        await _sops.TouchAsync(sopId, userId);
        await _audit.LogAsync(AuditEntities.Sop, sopId, "ResourceDeleted",
            $"{sop.SopCode} v{sop.Version}: {resource.ResourceType} '{resource.Description}' removed");
    }

    // -----------------------------------------------------------------------
    // Attachments
    // -----------------------------------------------------------------------

    public async Task<int> AddAttachmentAsync(int sopId, int stepId, IFormFile file, int userId)
    {
        var (sop, step) = await GetEditableStepAsync(sopId, stepId);

        if (file.Length == 0)
            throw new ValidationException("The file is empty.", "file");
        if (file.Length > _files.MaxFileSizeBytes)
            throw new ValidationException($"The file is larger than {_files.MaxFileSizeBytes / (1024 * 1024)} MB.", "file");

        var fileName = Path.GetFileName(file.FileName);
        var contentType = _files.GetContentType(fileName)
            ?? throw new ValidationException("Only JPG, PNG, GIF, WEBP images and PDF documents can be attached.", "file");

        await using var stream = file.OpenReadStream();
        var storedName = await _files.SaveAsync(stream, fileName);

        var id = await _sops.AddAttachmentAsync(new AttachmentDto
        {
            StepId = stepId,
            FileName = fileName,
            StoredName = storedName,
            ContentType = contentType,
            SizeBytes = file.Length
        }, userId);
        await _sops.TouchAsync(sopId, userId);
        await _audit.LogAsync(AuditEntities.Sop, sopId, "AttachmentAdded",
            $"{sop.SopCode} v{sop.Version}: '{fileName}' attached to step {step.StepNo}");
        return id;
    }

    public async Task DeleteAttachmentAsync(int sopId, int attachmentId, int userId)
    {
        var sop = await GetEditableAsync(sopId);
        var attachment = sop.Steps.SelectMany(s => s.Attachments).FirstOrDefault(a => a.Id == attachmentId)
            ?? throw new NotFoundException("Attachment not found.");

        await _sops.DeleteAttachmentAsync(attachmentId);
        await DeleteUnreferencedFilesAsync(new[] { attachment.StoredName });
        await _sops.TouchAsync(sopId, userId);
        await _audit.LogAsync(AuditEntities.Sop, sopId, "AttachmentDeleted", $"{sop.SopCode} v{sop.Version}: '{attachment.FileName}' removed");
    }

    public async Task<(AttachmentDto Attachment, Stream Content)> OpenAttachmentAsync(int sopId, int attachmentId, ClaimsPrincipal user)
    {
        var sop = await GetAsync(sopId, user);
        var attachment = sop.Steps.SelectMany(s => s.Attachments).FirstOrDefault(a => a.Id == attachmentId)
            ?? throw new NotFoundException("Attachment not found.");
        var stream = _files.OpenRead(attachment.StoredName) ?? throw new NotFoundException("The attachment file is missing.");
        return (attachment, stream);
    }

    /// <summary>Files are shared between SOP versions, so a file is only removed once no attachment row uses it.</summary>
    private async Task DeleteUnreferencedFilesAsync(IEnumerable<string> storedNames)
    {
        foreach (var name in storedNames.Distinct())
        {
            if (await _sops.CountAttachmentReferencesAsync(name) == 0)
                _files.Delete(name);
        }
    }
}
