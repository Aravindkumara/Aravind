using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceExcellence.Api.Infrastructure;
using ServiceExcellence.Api.Services;
using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Core.Interfaces;

namespace ServiceExcellence.Api.Controllers;

/// <summary>Divisions, model categories, models and variants. <c>type</c> is one of Division, Category, Model, Variant.</summary>
[ApiController]
[Route("api/masters/{type}")]
[Authorize]
public class MastersController : ControllerBase
{
    private readonly IMasterRepository _masters;
    private readonly IAuditService _audit;

    public MastersController(IMasterRepository masters, IAuditService audit)
    {
        _masters = masters;
        _audit = audit;
    }

    [HttpGet]
    public Task<PagedResult<MasterItemDto>> Search(MasterType type, [FromQuery] MasterQuery query) => _masters.SearchAsync(type, query);

    /// <summary>Active items for drop-downs, optionally filtered by parent.</summary>
    [HttpGet("lookup")]
    public Task<IReadOnlyList<LookupItem>> Lookup(MasterType type, int? parentId) => _masters.LookupAsync(type, parentId);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MasterItemDto>> Get(MasterType type, int id) =>
        await _masters.GetByIdAsync(type, id) ?? throw new NotFoundException($"{MasterTypes.Display(type)} not found.");

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<MasterItemDto>> Create(MasterType type, MasterSaveRequest request)
    {
        await ValidateAsync(type, request, null);
        var id = await _masters.CreateAsync(type, request);
        await _audit.LogAsync(AuditEntities.Master, id, "Created", $"{MasterTypes.Display(type)} {request.Code} - {request.Name} created");
        return CreatedAtAction(nameof(Get), new { type, id }, await _masters.GetByIdAsync(type, id));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(MasterType type, int id, MasterSaveRequest request)
    {
        _ = await _masters.GetByIdAsync(type, id) ?? throw new NotFoundException($"{MasterTypes.Display(type)} not found.");
        await ValidateAsync(type, request, id);
        await _masters.UpdateAsync(type, id, request);
        await _audit.LogAsync(AuditEntities.Master, id, "Updated", $"{MasterTypes.Display(type)} {request.Code} - {request.Name} updated");
        return NoContent();
    }

    private async Task ValidateAsync(MasterType type, MasterSaveRequest request, int? id)
    {
        request.Code = request.Code.Trim().ToUpperInvariant();
        if (await _masters.CodeExistsAsync(type, request.Code, id))
            throw new ValidationException($"{MasterTypes.Display(type)} code '{request.Code}' already exists.", nameof(request.Code));

        if (MasterTypes.Parent(type) is { } parentType)
        {
            if (request.ParentId is not { } parentId || !await _masters.ExistsAsync(parentType, parentId))
                throw new ValidationException($"Select a {MasterTypes.Display(parentType).ToLowerInvariant()}.", nameof(request.ParentId));
        }
        else
        {
            request.ParentId = null;
        }
    }
}
