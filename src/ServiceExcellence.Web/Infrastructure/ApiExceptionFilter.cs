using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ServiceExcellence.Web.Infrastructure;

/// <summary>
/// Handles API errors that controllers do not handle themselves: an expired token signs the user out,
/// forbidden/not found show friendly pages, and business errors are shown as an alert on the previous page.
/// </summary>
public class ApiExceptionFilter : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not ApiException api) return;

        var http = context.HttpContext;
        switch (api.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl = http.Request.Path + http.Request.QueryString });
                break;

            case HttpStatusCode.Forbidden:
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                break;

            case HttpStatusCode.NotFound when HttpMethods.IsGet(http.Request.Method):
                context.Result = new ViewResult { ViewName = "NotFound", StatusCode = 404 };
                break;

            default:
                // For a failed POST (e.g. a workflow action), go back to the referring page with the message.
                var tempData = http.RequestServices.GetRequiredService<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>().GetTempData(http);
                tempData.Error(api.Message);
                var referer = http.Request.Headers.Referer.ToString();
                context.Result = Uri.TryCreate(referer, UriKind.Absolute, out var uri) && uri.Host == http.Request.Host.Host
                    ? new RedirectResult(uri.PathAndQuery)
                    : new RedirectToActionResult("Index", "Home", null);
                break;
        }
        context.ExceptionHandled = true;
    }
}

public static class ModelStateExtensions
{
    /// <summary>Copies API validation errors onto the model state; field names are prefixed for nested view models.</summary>
    public static void AddApiErrors(this ModelStateDictionary modelState, ApiException ex, string prefix = "")
    {
        if (ex.Errors.Count == 0)
        {
            modelState.AddModelError("", ex.Message);
            return;
        }

        foreach (var (field, messages) in ex.Errors)
        {
            var key = string.IsNullOrEmpty(field) ? "" : string.IsNullOrEmpty(prefix) ? field : $"{prefix}.{field}";
            foreach (var message in messages)
                modelState.AddModelError(key, message);
        }
    }
}

public static class TempDataExtensions
{
    public static void Success(this Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary tempData, string message) => tempData["Success"] = message;
    public static void Error(this Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary tempData, string message) => tempData["Error"] = message;
}
