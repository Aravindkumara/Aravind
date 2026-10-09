using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Web.Infrastructure;
using ServiceExcellence.Web.ViewModels;

namespace ServiceExcellence.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class DealersController : Controller
{
    private readonly ApiClient _api;

    public DealersController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(PagedQuery query) =>
        View(new ListViewModel<DealerDto, PagedQuery> { Query = query, Result = await _api.GetAsync<PagedResult<DealerDto>>("api/dealers", query) });

    [HttpGet]
    public IActionResult Create() => View("Form", new DealerSaveRequest());

    [HttpPost]
    public Task<IActionResult> Create(DealerSaveRequest model) => SaveAsync(null, model);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var d = await _api.GetAsync<DealerDto>($"api/dealers/{id}");
        ViewBag.Id = id;
        return View("Form", new DealerSaveRequest
        {
            Code = d.Code, Name = d.Name, City = d.City, State = d.State, ContactPhone = d.ContactPhone, Email = d.Email, IsActive = d.IsActive
        });
    }

    [HttpPost]
    public Task<IActionResult> Edit(int id, DealerSaveRequest model) => SaveAsync(id, model);

    private async Task<IActionResult> SaveAsync(int? id, DealerSaveRequest model)
    {
        ViewBag.Id = id;
        if (!ModelState.IsValid) return View("Form", model);
        try
        {
            if (id.HasValue) await _api.PutAsync($"api/dealers/{id}", model);
            else await _api.PostAsync<DealerDto>("api/dealers", model);
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            ModelState.AddApiErrors(ex);
            return View("Form", model);
        }

        TempData.Success($"Dealer '{model.Name}' saved.");
        return RedirectToAction(nameof(Index));
    }
}

[Authorize(Roles = Roles.Admin)]
public class UsersController : Controller
{
    private readonly ApiClient _api;

    public UsersController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(UserQuery query) =>
        View(new ListViewModel<UserDto, UserQuery> { Query = query, Result = await _api.GetAsync<PagedResult<UserDto>>("api/users", query) });

    [HttpGet]
    public async Task<IActionResult> Create() =>
        View("Form", new UserFormViewModel { Dealers = await DealersAsync() });

    [HttpPost]
    public Task<IActionResult> Create(UserFormViewModel model) => SaveAsync(null, model);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var u = await _api.GetAsync<UserDto>($"api/users/{id}");
        return View("Form", new UserFormViewModel
        {
            Id = id,
            Item = new UserSaveRequest
            {
                Username = u.Username, FullName = u.FullName, Email = u.Email, Role = u.Role, DealerId = u.DealerId, IsActive = u.IsActive
            },
            Dealers = await DealersAsync()
        });
    }

    [HttpPost]
    public Task<IActionResult> Edit(int id, UserFormViewModel model) => SaveAsync(id, model);

    private async Task<IActionResult> SaveAsync(int? id, UserFormViewModel model)
    {
        model.Id = id;
        if (!id.HasValue && string.IsNullOrWhiteSpace(model.Item.Password))
            ModelState.AddModelError("Item.Password", "A password is required for a new user.");

        if (ModelState.IsValid)
        {
            try
            {
                if (id.HasValue) await _api.PutAsync($"api/users/{id}", model.Item);
                else await _api.PostAsync<UserDto>("api/users", model.Item);

                TempData.Success($"User '{model.Item.Username}' saved.");
                return RedirectToAction(nameof(Index));
            }
            catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                ModelState.AddApiErrors(ex, nameof(model.Item));
            }
        }

        model.Dealers = await DealersAsync();
        return View("Form", model);
    }

    private async Task<IReadOnlyList<LookupItem>> DealersAsync() => await _api.GetAsync<List<LookupItem>>("api/dealers/lookup");
}

[Authorize(Roles = Roles.Admin)]
public class AuditController : Controller
{
    private readonly ApiClient _api;

    public AuditController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(AuditQuery query)
    {
        if (!Request.Query.ContainsKey("pageSize")) query.PageSize = 25;
        return View(new ListViewModel<AuditLogDto, AuditQuery> { Query = query, Result = await _api.GetAsync<PagedResult<AuditLogDto>>("api/audit", query) });
    }
}
