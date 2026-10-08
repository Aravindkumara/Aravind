using System.Security.Claims;
using ServiceExcellence.Core.Constants;

namespace ServiceExcellence.Web.Infrastructure;

public static class Ui
{
    public static string StatusBadge(string status) => status switch
    {
        SopStatus.Draft => "bg-secondary",
        SopStatus.UnderReview => "bg-warning text-dark",
        SopStatus.Approved => "bg-info text-dark",
        SopStatus.Rejected => "bg-danger",
        SopStatus.Published => "bg-success",
        SopStatus.Obsolete => "bg-dark",
        _ => "bg-light text-dark"
    };

    public static string FileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024):0.#} MB"
    };

    public static string Date(DateTime? value) => value?.ToLocalTime().ToString("dd-MMM-yyyy") ?? "-";
    public static string DateTime(DateTime? value) => value?.ToLocalTime().ToString("dd-MMM-yyyy HH:mm") ?? "-";

    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(WebClaims.UserId)?.Value, out var id) ? id : 0;

    public static string GetFullName(this ClaimsPrincipal user) => user.FindFirst(WebClaims.FullName)?.Value ?? user.Identity?.Name ?? "";

    public static bool IsInternal(this ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true && !user.IsInRole(Roles.Dealer);
}
