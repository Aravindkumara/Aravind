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
[Route("api/sops")]
[Authorize]
public class SopsController : ControllerBase
{
    private readonly ISopRepository _sops;
    private readonly IAuditRepository _audit;
    private readonly SopService _service;
    private readonly SopPdfGenerator _pdf;

    public SopsController(ISopRepository sops, IAuditRepository audit, SopService service, SopPdfGenerator pdf)
    {
        _sops = sops;
        _audit = audit;
        _service = service;
        _pdf = pdf;
    }

    /// <summary>Searches SOPs. Dealers only see published SOPs.</summary>
    [HttpGet]
    public Task<PagedResult<SopListItemDto>> Search([FromQuery] SopQuery query) => _sops.SearchAsync(query, User.IsDealer());

    /// <summary>Published SOPs applicable to a product (division down to assembly), including inherited ones.</summary>
    [HttpGet("finder")]
    public Task<PagedResult<SopListItemDto>> Finder([FromQuery] SopFinderQuery query) => _service.FindApplicableAsync(query);

    [HttpGet("{id:int}")]
    public Task<SopDetailDto> Get(int id) => _service.GetAsync(id, User);

    [HttpGet("{id:int}/history")]
    [Authorize(Roles = Roles.Internal)]
    public Task<PagedResult<AuditLogDto>> History(int id, [FromQuery] PagedQuery query) =>
        _audit.SearchAsync(new AuditQuery
        {
            EntityType = AuditEntities.Sop,
            EntityId = id,
            Page = query.Page,
            PageSize = query.PageSize,
            SortDir = "desc"
        });

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id)
    {
        var sop = await _service.GetAsync(id, User);
        return File(_pdf.Generate(sop), "application/pdf", $"{sop.SopCode}_v{sop.Version}.pdf");
    }

    [HttpPost]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<ActionResult<SopDetailDto>> Create(SopSaveRequest request)
    {
        var id = await _service.CreateAsync(request, User.GetUserId());
        return CreatedAtAction(nameof(Get), new { id }, await _sops.GetByIdAsync(id));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> Update(int id, SopSaveRequest request)
    {
        await _service.UpdateAsync(id, request, User.GetUserId());
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // -------------------------------------------------------------------
    // Workflow
    // -------------------------------------------------------------------

    [HttpPost("{id:int}/submit")]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> Submit(int id)
    {
        await _service.SubmitAsync(id, User.GetUserId());
        return NoContent();
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = Roles.SopApprovers)]
    public async Task<IActionResult> Approve(int id, WorkflowActionRequest request)
    {
        await _service.ApproveAsync(id, User, request.Remarks);
        return NoContent();
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = Roles.SopApprovers)]
    public async Task<IActionResult> Reject(int id, WorkflowActionRequest request)
    {
        await _service.RejectAsync(id, User, request.Remarks);
        return NoContent();
    }

    [HttpPost("{id:int}/publish")]
    [Authorize(Roles = Roles.SopApprovers)]
    public async Task<IActionResult> Publish(int id)
    {
        await _service.PublishAsync(id, User.GetUserId());
        return NoContent();
    }

    [HttpPost("{id:int}/obsolete")]
    [Authorize(Roles = Roles.SopApprovers)]
    public async Task<IActionResult> Obsolete(int id, WorkflowActionRequest request)
    {
        await _service.MarkObsoleteAsync(id, User.GetUserId(), request.Remarks);
        return NoContent();
    }

    /// <summary>Creates a new draft version from a published SOP and returns it.</summary>
    [HttpPost("{id:int}/new-version")]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<ActionResult<SopDetailDto>> NewVersion(int id)
    {
        var newId = await _service.CreateNewVersionAsync(id, User.GetUserId());
        return CreatedAtAction(nameof(Get), new { id = newId }, await _sops.GetByIdAsync(newId));
    }

    // -------------------------------------------------------------------
    // Steps
    // -------------------------------------------------------------------

    [HttpGet("{id:int}/steps/{stepId:int}")]
    public Task<SopStepDto> GetStep(int id, int stepId) => _service.GetStepAsync(id, stepId, User);

    [HttpPost("{id:int}/steps")]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<ActionResult<SopStepDto>> AddStep(int id, SopStepSaveRequest request)
    {
        var stepId = await _service.AddStepAsync(id, request, User.GetUserId());
        return CreatedAtAction(nameof(GetStep), new { id, stepId }, await _sops.GetStepAsync(stepId));
    }

    [HttpPut("{id:int}/steps/{stepId:int}")]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> UpdateStep(int id, int stepId, SopStepSaveRequest request)
    {
        await _service.UpdateStepAsync(id, stepId, request, User.GetUserId());
        return NoContent();
    }

    [HttpDelete("{id:int}/steps/{stepId:int}")]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> DeleteStep(int id, int stepId)
    {
        await _service.DeleteStepAsync(id, stepId, User.GetUserId());
        return NoContent();
    }

    /// <summary>Moves a step one position up (direction = -1) or down (direction = 1).</summary>
    [HttpPost("{id:int}/steps/{stepId:int}/move")]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> MoveStep(int id, int stepId, [FromQuery] int direction)
    {
        await _service.MoveStepAsync(id, stepId, direction, User.GetUserId());
        return NoContent();
    }

    // -------------------------------------------------------------------
    // Attachments
    // -------------------------------------------------------------------

    [HttpPost("{id:int}/steps/{stepId:int}/attachments")]
    [Authorize(Roles = Roles.SopEditors)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(int id, int stepId, IFormFile file)
    {
        var attachmentId = await _service.AddAttachmentAsync(id, stepId, file, User.GetUserId());
        return Created($"/api/sops/{id}/attachments/{attachmentId}", new { id = attachmentId });
    }

    [HttpGet("{id:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> DownloadAttachment(int id, int attachmentId)
    {
        var (attachment, content) = await _service.OpenAttachmentAsync(id, attachmentId, User);
        return File(content, attachment.ContentType, attachment.FileName);
    }

    [HttpDelete("{id:int}/attachments/{attachmentId:int}")]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> DeleteAttachment(int id, int attachmentId)
    {
        await _service.DeleteAttachmentAsync(id, attachmentId, User.GetUserId());
        return NoContent();
    }

    // -------------------------------------------------------------------
    // Resources (parts, tools, consumables)
    // -------------------------------------------------------------------

    [HttpPost("{id:int}/resources")]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<ActionResult<SopResourceDto>> AddResource(int id, SopResourceSaveRequest request)
    {
        var resourceId = await _service.AddResourceAsync(id, request, User.GetUserId());
        return Created($"/api/sops/{id}/resources/{resourceId}", await _sops.GetResourceAsync(resourceId));
    }

    [HttpDelete("{id:int}/resources/{resourceId:int}")]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> DeleteResource(int id, int resourceId)
    {
        await _service.DeleteResourceAsync(id, resourceId, User.GetUserId());
        return NoContent();
    }
}
