using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Web.Infrastructure;
using ServiceExcellence.Web.ViewModels;

namespace ServiceExcellence.Web.Controllers;

[Authorize]
public class SopsController : Controller
{
    private readonly ApiClient _api;

    public SopsController(ApiClient api) => _api = api;

    // -------------------------------------------------------------------
    // List & details
    // -------------------------------------------------------------------

    public async Task<IActionResult> Index(SopQuery query) =>
        View(new SopIndexViewModel
        {
            Query = query,
            Result = await _api.GetAsync<PagedResult<SopListItemDto>>("api/sops", query),
            Divisions = await DivisionsAsync()
        });

    public async Task<IActionResult> Details(int id)
    {
        var sop = await _api.GetAsync<SopDetailDto>($"api/sops/{id}");
        var isEditor = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Author);
        var isApprover = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Approver);
        var editable = SopStatus.IsEditable(sop.Status);

        var model = new SopDetailsViewModel
        {
            Sop = sop,
            CanEdit = isEditor && editable,
            CanSubmit = isEditor && editable && sop.Steps.Count > 0,
            CanDelete = isEditor && sop.Status == SopStatus.Draft,
            // Reviewers cannot review their own SOP (administrators excepted); the API enforces the same rule.
            CanReview = isApprover && sop.Status == SopStatus.UnderReview && (User.IsInRole(Roles.Admin) || sop.CreatedBy != User.GetUserId()),
            CanPublish = isApprover && sop.Status == SopStatus.Approved,
            CanObsolete = isApprover && sop.Status == SopStatus.Published,
            CanCreateVersion = isEditor && sop.Status is SopStatus.Published or SopStatus.Obsolete
        };

        if (User.IsInternal())
            model.History = (await _api.GetAsync<PagedResult<AuditLogDto>>($"api/sops/{id}/history", new { PageSize = 50 })).Items;

        return View(model);
    }

    /// <summary>Printer-friendly HTML view of the SOP.</summary>
    public async Task<IActionResult> Print(int id) => View(await _api.GetAsync<SopDetailDto>($"api/sops/{id}"));

    public async Task<IActionResult> Pdf(int id)
    {
        var response = await _api.GetFileAsync($"api/sops/{id}/pdf");
        Response.RegisterForDispose(response);
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName ?? $"SOP-{id}.pdf";
        return File(await response.Content.ReadAsStreamAsync(), "application/pdf", fileName.Trim('"'));
    }

    /// <summary>Streams an attachment; images and PDFs open inline unless <paramref name="download"/> is set.</summary>
    [HttpGet("Sops/{id:int}/Attachment/{attachmentId:int}")]
    public async Task<IActionResult> Attachment(int id, int attachmentId, bool download = false)
    {
        var response = await _api.GetFileAsync($"api/sops/{id}/attachments/{attachmentId}");
        Response.RegisterForDispose(response);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var fileName = (response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName ?? "attachment").Trim('"');
        var stream = await response.Content.ReadAsStreamAsync();

        Response.Headers.CacheControl = "private, max-age=300";
        // Uploaded files are served with the type derived from their extension; never let the browser sniff another.
        Response.Headers.XContentTypeOptions = "nosniff";
        return download ? File(stream, contentType, fileName) : File(stream, contentType);
    }

    // -------------------------------------------------------------------
    // Create / edit / delete
    // -------------------------------------------------------------------

    [HttpGet]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> Create() => View("Form", new SopFormViewModel { Divisions = await DivisionsAsync() });

    [HttpPost]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> Create(SopFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var created = await _api.PostAsync<SopDetailDto>("api/sops", model.Item);
                TempData.Success($"SOP {created.SopCode} created as a draft. Add the procedure steps next.");
                return RedirectToAction(nameof(Details), new { id = created.Id });
            }
            catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                ModelState.AddApiErrors(ex, nameof(model.Item));
            }
        }

        model.Divisions = await DivisionsAsync();
        return View("Form", model);
    }

    [HttpGet]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> Edit(int id)
    {
        var sop = await _api.GetAsync<SopDetailDto>($"api/sops/{id}");
        if (!SopStatus.IsEditable(sop.Status))
        {
            TempData.Error($"The SOP is {SopStatus.Display(sop.Status)} and cannot be edited.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View("Form", new SopFormViewModel
        {
            Id = id,
            Version = sop.Version,
            Item = new SopSaveRequest
            {
                SopCode = sop.SopCode, Title = sop.Title, ActivityType = sop.ActivityType, Purpose = sop.Purpose,
                DivisionId = sop.DivisionId, CategoryId = sop.CategoryId, ModelId = sop.ModelId, VariantId = sop.VariantId,
                AssemblyId = sop.AssemblyId, StandardTimeMinutes = sop.StandardTimeMinutes, SkillLevel = sop.SkillLevel,
                SafetyNotes = sop.SafetyNotes
            },
            Divisions = await DivisionsAsync()
        });
    }

    [HttpPost]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> Edit(int id, SopFormViewModel model)
    {
        model.Id = id;
        if (ModelState.IsValid)
        {
            try
            {
                await _api.PutAsync($"api/sops/{id}", model.Item);
                TempData.Success("SOP details saved.");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                ModelState.AddApiErrors(ex, nameof(model.Item));
            }
        }

        model.Divisions = await DivisionsAsync();
        return View("Form", model);
    }

    [HttpPost]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> Delete(int id)
    {
        await _api.DeleteAsync($"api/sops/{id}");
        TempData.Success("Draft SOP deleted.");
        return RedirectToAction(nameof(Index));
    }

    // -------------------------------------------------------------------
    // Workflow
    // -------------------------------------------------------------------

    [HttpPost]
    [Authorize(Roles = Roles.SopEditors)]
    public Task<IActionResult> Submit(int id) => WorkflowAsync(id, "submit", null, "SOP submitted for review.");

    [HttpPost]
    [Authorize(Roles = Roles.SopApprovers)]
    public Task<IActionResult> Approve(int id, string? remarks) => WorkflowAsync(id, "approve", remarks, "SOP approved. It can now be published.");

    [HttpPost]
    [Authorize(Roles = Roles.SopApprovers)]
    public Task<IActionResult> Reject(int id, string? remarks) => WorkflowAsync(id, "reject", remarks, "SOP rejected and returned to the author.");

    [HttpPost]
    [Authorize(Roles = Roles.SopApprovers)]
    public Task<IActionResult> Publish(int id) => WorkflowAsync(id, "publish", null, "SOP published. It is now visible to dealers.");

    [HttpPost]
    [Authorize(Roles = Roles.SopApprovers)]
    public Task<IActionResult> Obsolete(int id, string? remarks) => WorkflowAsync(id, "obsolete", remarks, "SOP marked obsolete.");

    [HttpPost]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> NewVersion(int id)
    {
        var created = await _api.PostAsync<SopDetailDto>($"api/sops/{id}/new-version");
        TempData.Success($"Version {created.Version} created as a draft.");
        return RedirectToAction(nameof(Details), new { id = created.Id });
    }

    private async Task<IActionResult> WorkflowAsync(int id, string action, string? remarks, string successMessage)
    {
        try
        {
            await _api.PostAsync($"api/sops/{id}/{action}", new WorkflowActionRequest { Remarks = remarks });
            TempData.Success(successMessage);
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.Forbidden)
        {
            TempData.Error(ex.Message);
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // -------------------------------------------------------------------
    // Steps
    // -------------------------------------------------------------------

    [HttpGet]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> AddStep(int id)
    {
        var sop = await _api.GetAsync<SopDetailDto>($"api/sops/{id}");
        return View("StepForm", new StepFormViewModel
        {
            SopId = id, SopCode = sop.SopCode, SopTitle = sop.Title, StepCount = sop.Steps.Count,
            Item = new SopStepSaveRequest { StepNo = sop.Steps.Count + 1 }
        });
    }

    [HttpPost]
    [Authorize(Roles = Roles.SopEditors)]
    public Task<IActionResult> AddStep(int id, StepFormViewModel model, string? next) => SaveStepAsync(id, null, model, next == "add");

    [HttpGet]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> EditStep(int id, int stepId)
    {
        var sop = await _api.GetAsync<SopDetailDto>($"api/sops/{id}");
        var step = sop.Steps.FirstOrDefault(s => s.Id == stepId);
        if (step == null) return View("NotFound");

        return View("StepForm", new StepFormViewModel
        {
            SopId = id, SopCode = sop.SopCode, SopTitle = sop.Title, StepId = stepId, StepCount = sop.Steps.Count,
            Item = new SopStepSaveRequest
            {
                StepNo = step.StepNo, Title = step.Title, Instruction = step.Instruction, ToolsRequired = step.ToolsRequired,
                Specification = step.Specification, Caution = step.Caution, EstimatedMinutes = step.EstimatedMinutes
            }
        });
    }

    [HttpPost]
    [Authorize(Roles = Roles.SopEditors)]
    public Task<IActionResult> EditStep(int id, int stepId, StepFormViewModel model) => SaveStepAsync(id, stepId, model, false);

    private async Task<IActionResult> SaveStepAsync(int id, int? stepId, StepFormViewModel model, bool addAnother)
    {
        model.SopId = id;
        model.StepId = stepId;
        if (ModelState.IsValid)
        {
            try
            {
                if (stepId.HasValue) await _api.PutAsync($"api/sops/{id}/steps/{stepId}", model.Item);
                else await _api.PostAsync<SopStepDto>($"api/sops/{id}/steps", model.Item);

                TempData.Success($"Step '{model.Item.Title}' saved.");
                return addAnother
                    ? RedirectToAction(nameof(AddStep), new { id })
                    : Redirect(Url.Action(nameof(Details), new { id }) + "#steps");
            }
            catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                ModelState.AddApiErrors(ex, nameof(model.Item));
            }
        }
        return View("StepForm", model);
    }

    [HttpPost]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> DeleteStep(int id, int stepId)
    {
        await _api.DeleteAsync($"api/sops/{id}/steps/{stepId}");
        TempData.Success("Step deleted.");
        return Redirect(Url.Action(nameof(Details), new { id }) + "#steps");
    }

    [HttpPost]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> MoveStep(int id, int stepId, int direction)
    {
        await _api.PostAsync($"api/sops/{id}/steps/{stepId}/move?direction={direction}");
        return Redirect(Url.Action(nameof(Details), new { id }) + "#steps");
    }

    // -------------------------------------------------------------------
    // Attachments & resources
    // -------------------------------------------------------------------

    [HttpPost]
    [Authorize(Roles = Roles.SopEditors)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(int id, int stepId, IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            TempData.Error("Choose a file to upload.");
        }
        else
        {
            try
            {
                await _api.UploadAsync($"api/sops/{id}/steps/{stepId}/attachments", file);
                TempData.Success($"'{file.FileName}' attached.");
            }
            catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.RequestEntityTooLarge)
            {
                TempData.Error(ex.Message);
            }
        }
        return Redirect(Url.Action(nameof(Details), new { id }) + $"#step-{stepId}");
    }

    [HttpPost]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> DeleteAttachment(int id, int attachmentId)
    {
        await _api.DeleteAsync($"api/sops/{id}/attachments/{attachmentId}");
        TempData.Success("Attachment removed.");
        return Redirect(Url.Action(nameof(Details), new { id }) + "#steps");
    }

    [HttpPost]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> AddResource(int id, [Bind(Prefix = "NewResource")] SopResourceSaveRequest resource)
    {
        if (!ModelState.IsValid)
        {
            TempData.Error(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
        }
        else
        {
            try
            {
                await _api.PostAsync<SopResourceDto>($"api/sops/{id}/resources", resource);
                TempData.Success($"{resource.ResourceType} '{resource.Description}' added.");
            }
            catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                TempData.Error(ex.Message);
            }
        }
        return Redirect(Url.Action(nameof(Details), new { id }) + "#resources");
    }

    [HttpPost]
    [Authorize(Roles = Roles.SopEditors)]
    public async Task<IActionResult> DeleteResource(int id, int resourceId)
    {
        await _api.DeleteAsync($"api/sops/{id}/resources/{resourceId}");
        TempData.Success("Item removed.");
        return Redirect(Url.Action(nameof(Details), new { id }) + "#resources");
    }

    private async Task<IReadOnlyList<LookupItem>> DivisionsAsync() =>
        await _api.GetAsync<List<LookupItem>>($"api/masters/{MasterType.Division}/lookup");
}

/// <summary>Dealer-facing search: pick a product down to assembly level and see every applicable published SOP.</summary>
[Authorize]
public class FinderController : Controller
{
    private readonly ApiClient _api;

    public FinderController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(SopFinderQuery query)
    {
        var model = new FinderViewModel
        {
            Query = query,
            Divisions = await _api.GetAsync<List<LookupItem>>($"api/masters/{MasterType.Division}/lookup")
        };

        if (query.DivisionId.HasValue)
            model.Result = await _api.GetAsync<PagedResult<SopListItemDto>>("api/sops/finder", query);

        return View(model);
    }
}
