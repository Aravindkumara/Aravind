using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Web.Infrastructure;
using ServiceExcellence.Web.ViewModels;

namespace ServiceExcellence.Web.Controllers;

public class AccountController : Controller
{
    private readonly ApiClient _api;
    private readonly ILogger<AccountController> _logger;

    public AccountController(ApiClient api, ILogger<AccountController> logger)
    {
        _api = api;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        LoginResponse login;
        try
        {
            login = await _api.PostAsync<LoginResponse>("api/auth/login", new LoginRequest { Username = model.Username, Password = model.Password });
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "The API could not be reached during login");
            ModelState.AddModelError("", "The service is currently unavailable. Please try again shortly.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, login.Username),
            new(ClaimTypes.Role, login.Role),
            new(WebClaims.UserId, login.UserId.ToString()),
            new(WebClaims.FullName, login.FullName),
            new(WebClaims.AccessToken, login.Token)
        };
        if (login.DealerId.HasValue) claims.Add(new Claim(WebClaims.DealerId, login.DealerId.Value.ToString()));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { ExpiresUtc = login.ExpiresAtUtc, IsPersistent = false });

        return Url.IsLocalUrl(model.ReturnUrl) ? Redirect(model.ReturnUrl) : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordRequest());

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            await _api.PostAsync("api/auth/change-password", model);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            ModelState.AddApiErrors(ex);
            return View(model);
        }

        TempData.Success("Your password has been changed.");
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
