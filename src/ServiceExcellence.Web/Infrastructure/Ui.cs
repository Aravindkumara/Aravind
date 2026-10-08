using System.Security.Claims;
using ServiceExcellence.Core.Constants;

namespace ServiceExcellence.Web.Infrastructure;

public static class Ui
{
    /// <summary>Tabler colour name for an SOP status (used with status-*, bg-*-lt, text-*).</summary>
    public static string StatusColor(string status) => status switch
    {
        SopStatus.Draft => "secondary",
        SopStatus.UnderReview => "yellow",
        SopStatus.Approved => "azure",
        SopStatus.Rejected => "red",
        SopStatus.Published => "green",
        SopStatus.Obsolete => "dark",
        _ => "secondary"
    };

    public static string StatusIcon(string status) => status switch
    {
        SopStatus.Draft => "ti-pencil",
        SopStatus.UnderReview => "ti-eye-search",
        SopStatus.Approved => "ti-circle-check",
        SopStatus.Rejected => "ti-arrow-back-up",
        SopStatus.Published => "ti-world-upload",
        SopStatus.Obsolete => "ti-archive",
        _ => "ti-file"
    };

    public static string ActivityIcon(string activityType) => activityType switch
    {
        "PDI" => "ti-clipboard-check",
        "PeriodicMaintenance" => "ti-calendar-repeat",
        "Repair" => "ti-tool",
        "Inspection" => "ti-zoom-scan",
        "Warranty" => "ti-shield-check",
        "Campaign" => "ti-speakerphone",
        _ => "ti-file-text"
    };

    public static string ActivityColor(string activityType) => activityType switch
    {
        "PDI" => "blue",
        "PeriodicMaintenance" => "teal",
        "Repair" => "orange",
        "Inspection" => "purple",
        "Warranty" => "green",
        "Campaign" => "red",
        _ => "secondary"
    };

    /// <summary>Icon for an audit trail action.</summary>
    public static string AuditIcon(string action) => action switch
    {
        "Created" or "VersionCreated" => "ti-file-plus",
        "Submitted" => "ti-send",
        "Approved" => "ti-circle-check",
        "Rejected" => "ti-arrow-back-up",
        "Published" => "ti-world-upload",
        "Obsoleted" => "ti-archive",
        "Deleted" or "StepDeleted" or "ResourceDeleted" or "AttachmentDeleted" => "ti-trash",
        "AttachmentAdded" => "ti-paperclip",
        "StepMoved" => "ti-arrows-sort",
        "PasswordChanged" => "ti-key",
        _ => "ti-edit"
    };

    public static string AuditColor(string action) => action switch
    {
        "Approved" or "Published" => "green",
        "Rejected" or "Deleted" or "StepDeleted" or "ResourceDeleted" or "AttachmentDeleted" => "red",
        "Submitted" => "yellow",
        "Obsoleted" => "dark",
        "Created" or "VersionCreated" => "blue",
        _ => "secondary"
    };

    public static string ResourceIcon(string resourceType) => resourceType switch
    {
        ResourceTypes.Part => "ti-settings",
        ResourceTypes.Tool => "ti-tool",
        ResourceTypes.Consumable => "ti-droplet",
        _ => "ti-box"
    };

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
        };
    }

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
