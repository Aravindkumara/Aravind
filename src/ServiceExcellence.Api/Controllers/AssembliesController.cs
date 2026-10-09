using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceExcellence.Api.Infrastructure;
using ServiceExcellence.Api.Services;
using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Core.Interfaces;

namespace ServiceExcellence.Api.Controllers;

/// <summary>Assembly levels of a variant: Assembly (1) -> Sub-assembly (2) -> Component (3).</summary>
[ApiController]
[Route("api/assemblies")]
[Authorize]
public class AssembliesController : ControllerBase
{
    private readonly IAssemblyRepository _assemblies;
    private readonly IMasterRepository _masters;
    private readonly IAuditService _audit;

    public AssembliesController(IAssemblyRepository assemblies, IMasterRepository masters, IAuditService audit)
    {
        _assemblies = assemblies;
        _masters = masters;
        _audit = audit;
    }

    [HttpGet]
    public Task<PagedResult<AssemblyDto>> Search([FromQuery] AssemblyQuery query) => _assemblies.SearchAsync(query);

    /// <summary>All assemblies of a variant ordered as a tree, with their full path.</summary>
    [HttpGet("tree")]
    public Task<IReadOnlyList<AssemblyDto>> Tree([FromQuery] int variantId, [FromQuery] bool activeOnly = true) =>
        _assemblies.GetTreeAsync(variantId, activeOnly);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AssemblyDto>> Get(int id) =>
        await _assemblies.GetByIdAsync(id) ?? throw new NotFoundException("Assembly not found.");

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<AssemblyDto>> Create(AssemblySaveRequest request)
    {
        var level = await ValidateAsync(request, null);
        var id = await _assemblies.CreateAsync(request, level);
        await _audit.LogAsync(AuditEntities.Assembly, id, "Created", $"{AssemblyLevels.Display(level)} {request.Code} - {request.Name} created");
        return CreatedAtAction(nameof(Get), new { id }, await _assemblies.GetByIdAsync(id));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(int id, AssemblySaveRequest request)
    {
        var existing = await _assemblies.GetByIdAsync(id) ?? throw new NotFoundException("Assembly not found.");
        var level = await ValidateAsync(request, id);

        if ((existing.ParentId != request.ParentId || existing.VariantId != request.VariantId) && await _assemblies.HasChildrenAsync(id))
            throw new ValidationException("This assembly has child items, so its variant and parent cannot be changed.", nameof(request.ParentId));

        await _assemblies.UpdateAsync(id, request, level);
        await _audit.LogAsync(AuditEntities.Assembly, id, "Updated", $"{AssemblyLevels.Display(level)} {request.Code} - {request.Name} updated");
        return NoContent();
    }

    /// <summary>Validates the request and returns the level derived from the parent.</summary>
    private async Task<short> ValidateAsync(AssemblySaveRequest request, int? id)
    {
        if (request.VariantId is not { } variantId || !await _masters.ExistsAsync(MasterType.Variant, variantId))
            throw new ValidationException("Select a variant.", nameof(request.VariantId));

        request.Code = request.Code.Trim().ToUpperInvariant();
        if (await _assemblies.CodeExistsAsync(variantId, request.Code, id))
            throw new ValidationException($"Code '{request.Code}' already exists for this variant.", nameof(request.Code));

        if (request.ParentId is not { } parentId)
            return 1;

        if (parentId == id)
            throw new ValidationException("An assembly cannot be its own parent.", nameof(request.ParentId));

        var parent = await _assemblies.GetByIdAsync(parentId);
        if (parent == null || parent.VariantId != variantId)
            throw new ValidationException("The parent assembly must belong to the same variant.", nameof(request.ParentId));
        if (parent.Level >= 3)
            throw new ValidationException("Components are the lowest level and cannot have children.", nameof(request.ParentId));

        return (short)(parent.Level + 1);
    }
}
