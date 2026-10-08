using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Web.Infrastructure;

namespace ServiceExcellence.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ApiClient _api;

    public HomeController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index() => View(await _api.GetAsync<DashboardDto>("api/dashboard"));

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    [AllowAnonymous]
    [Route("Home/Status/{code:int}")]
    public IActionResult Status(int code) => code == 404 ? View("NotFound") : View("Error");
}

/// <summary>JSON endpoints feeding the cascading classification drop-downs.</summary>
[Authorize]
[Route("lookup")]
public class LookupController : Controller
{
    private readonly ApiClient _api;

    public LookupController(ApiClient api) => _api = api;

    [HttpGet("masters/{type}")]
    public async Task<IActionResult> Masters(string type, int? parentId) =>
        Json(await _api.GetAsync<List<ServiceExcellence.Core.Common.LookupItem>>($"api/masters/{Uri.EscapeDataString(type)}/lookup", new { ParentId = parentId }));

    [HttpGet("assemblies")]
    public async Task<IActionResult> Assemblies(int variantId)
    {
        var tree = await _api.GetAsync<List<AssemblyDto>>("api/assemblies/tree", new { VariantId = variantId });
        return Json(tree.Select(a => new { a.Id, a.Code, a.Name, a.Level, a.Path }));
    }
}
