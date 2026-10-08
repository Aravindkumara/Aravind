using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Web.Infrastructure;
using ServiceExcellence.Web.ViewModels;

namespace ServiceExcellence.Web.Controllers;

[Authorize(Roles = Roles.Internal)]
public class AssembliesController : Controller
{
    private readonly ApiClient _api;

    public AssembliesController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(AssemblyQuery query)
    {
        if (!Request.Query.ContainsKey("pageSize")) query.PageSize = 25;
        return View(new AssemblyIndexViewModel
        {
            Query = query,
            Result = await _api.GetAsync<PagedResult<AssemblyDto>>("api/assemblies", query),
            Variants = await VariantsAsync()
        });
    }

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(int? variantId, int? parentId) =>
        View("Form", new AssemblyFormViewModel
        {
            Item = new AssemblySaveRequest { VariantId = variantId, ParentId = parentId },
            Variants = await VariantsAsync()
        });

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public Task<IActionResult> Create(AssemblyFormViewModel model) => SaveAsync(null, model);

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(int id)
    {
        var a = await _api.GetAsync<AssemblyDto>($"api/assemblies/{id}");
        return View("Form", new AssemblyFormViewModel
        {
            Id = id,
            Item = new AssemblySaveRequest
            {
                VariantId = a.VariantId, ParentId = a.ParentId, Code = a.Code, Name = a.Name, Description = a.Description, IsActive = a.IsActive
            },
            Variants = await VariantsAsync()
        });
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public Task<IActionResult> Edit(int id, AssemblyFormViewModel model) => SaveAsync(id, model);

    private async Task<IActionResult> SaveAsync(int? id, AssemblyFormViewModel model)
    {
        model.Id = id;
        if (ModelState.IsValid)
        {
            try
            {
                if (id.HasValue) await _api.PutAsync($"api/assemblies/{id}", model.Item);
                else await _api.PostAsync<AssemblyDto>("api/assemblies", model.Item);

                TempData.Success($"Assembly '{model.Item.Name}' saved.");
                return RedirectToAction(nameof(Index), new { variantId = model.Item.VariantId });
            }
            catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                ModelState.AddApiErrors(ex, nameof(model.Item));
            }
        }

        model.Variants = await VariantsAsync();
        return View("Form", model);
    }

    private async Task<IReadOnlyList<LookupItem>> VariantsAsync() =>
        await _api.GetAsync<List<LookupItem>>($"api/masters/{MasterType.Variant}/lookup");
}
