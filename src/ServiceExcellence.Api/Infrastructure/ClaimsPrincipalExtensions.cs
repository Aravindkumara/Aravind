using System.Security.Claims;
using ServiceExcellence.Core.Constants;

namespace ServiceExcellence.Api.Infrastructure;

public static class AppClaims
{
    public const string UserId = "sub";
    public const string Username = "unique_name";
    public const string FullName = "name";
    public const string Role = "role";
    public const string DealerId = "dealer_id";
}

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(AppClaims.UserId), out var id) ? id : throw new ForbiddenException("Invalid user token.");

    public static string GetUsername(this ClaimsPrincipal user) => user.FindFirstValue(AppClaims.Username) ?? "";

    public static bool IsDealer(this ClaimsPrincipal user) => user.IsInRole(Roles.Dealer);

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);
}
