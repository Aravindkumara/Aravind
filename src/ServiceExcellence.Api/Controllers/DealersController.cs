using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceExcellence.Api.Infrastructure;
using ServiceExcellence.Api.Services;
using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Core.Interfaces;

namespace ServiceExcellence.Api.Controllers;

[ApiController]
[Route("api/dealers")]
[Authorize(Roles = Roles.Admin)]
public class DealersController : ControllerBase
{
    private readonly IDealerRepository _dealers;
    private readonly IAuditService _audit;

    public DealersController(IDealerRepository dealers, IAuditService audit)
    {
        _dealers = dealers;
        _audit = audit;
    }

    [HttpGet]
    public Task<PagedResult<DealerDto>> Search([FromQuery] PagedQuery query) => _dealers.SearchAsync(query);

    [HttpGet("lookup")]
    public Task<IReadOnlyList<LookupItem>> Lookup() => _dealers.LookupAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DealerDto>> Get(int id) =>
        await _dealers.GetByIdAsync(id) ?? throw new NotFoundException("Dealer not found.");

    [HttpPost]
    public async Task<ActionResult<DealerDto>> Create(DealerSaveRequest request)
    {
        request.Code = request.Code.Trim().ToUpperInvariant();
        if (await _dealers.CodeExistsAsync(request.Code))
            throw new ValidationException($"Dealer code '{request.Code}' already exists.", nameof(request.Code));

        var id = await _dealers.CreateAsync(request);
        await _audit.LogAsync(AuditEntities.Dealer, id, "Created", $"Dealer {request.Code} - {request.Name} created");
        return CreatedAtAction(nameof(Get), new { id }, await _dealers.GetByIdAsync(id));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, DealerSaveRequest request)
    {
        _ = await _dealers.GetByIdAsync(id) ?? throw new NotFoundException("Dealer not found.");
        request.Code = request.Code.Trim().ToUpperInvariant();
        if (await _dealers.CodeExistsAsync(request.Code, id))
            throw new ValidationException($"Dealer code '{request.Code}' already exists.", nameof(request.Code));

        await _dealers.UpdateAsync(id, request);
        await _audit.LogAsync(AuditEntities.Dealer, id, "Updated", $"Dealer {request.Code} - {request.Name} updated");
        return NoContent();
    }
}
