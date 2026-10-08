using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Web.Infrastructure;
using ServiceExcellence.Web.ViewModels;

namespace ServiceExcellence.Web.Controllers;

/// <summary>Divisions, model categories, models and variants, all served by the same pages.</summary>
[Authorize(Roles = Roles.Internal)]
[Route("Masters/{type}")]
public class MastersController : Controller
{
    private readonly ApiClient _api;

    public MastersController(ApiClient api) => _api = api;

    [HttpGet("")]
    public async Task<IActionResult> Index(MasterType type, MasterQuery query)
    {
        var model = new MasterIndexViewModel
        {
            Type = type,
            Query = query,
            Result = await _api.GetAsync<PagedResult<MasterItemDto>>($"api/masters/{type}", query),
            Parents = await ParentsAsync(type)
        };
        return View(model);
    }

    [HttpGet("Create")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(MasterType type, int? parentId) =>
        View("Form", new MasterFormViewModel { Type = type, Item = new MasterSaveRequest { ParentId = parentId }, Parents = await ParentsAsync(type) });

    [HttpPost("Create")]
    [Authorize(Roles = Roles.Admin)]
    public Task<IActionResult> Create(MasterType type, MasterFormViewModel model) => SaveAsync(type, null, model);

    [HttpGet("Edit/{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(MasterType type, int id)
    {
        var item = await _api.GetAsync<MasterItemDto>($"api/masters/{type}/{id}");
        return View("Form", new MasterFormViewModel
        {
            Type = type,
            Id = id,
            Item = new MasterSaveRequest
            {
                ParentId = item.ParentId, Code = item.Code, Name = item.Name, Description = item.Description, IsActive = item.IsActive
            },
            Parents = await ParentsAsync(type)
        });
    }

    [HttpPost("Edit/{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public Task<IActionResult> Edit(MasterType type, int id, MasterFormViewModel model) => SaveAsync(type, id, model);

    private async Task<IActionResult> SaveAsync(MasterType type, int? id, MasterFormViewModel model)
    {
        model.Type = type;
        model.Id = id;
        if (ModelState.IsValid)
        {
            try
            {
                if (id.HasValue) await _api.PutAsync($"api/masters/{type}/{id}", model.Item);
                else await _api.PostAsync<MasterItemDto>($"api/masters/{type}", model.Item);

                TempData.Success($"{MasterTypes.Display(type)} '{model.Item.Name}' saved.");
                return RedirectToAction(nameof(Index), new { type });
            }
            catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                ModelState.AddApiErrors(ex, nameof(model.Item));
            }
        }

        model.Parents = await ParentsAsync(type);
        return View("Form", model);
    }

    private async Task<IReadOnlyList<LookupItem>> ParentsAsync(MasterType type) =>
        MasterTypes.Parent(type) is { } parent
            ? await _api.GetAsync<List<LookupItem>>($"api/masters/{parent}/lookup")
            : Array.Empty<LookupItem>();
}
