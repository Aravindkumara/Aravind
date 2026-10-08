namespace ServiceExcellence.Core.Constants;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Author = "Author";
    public const string Approver = "Approver";
    public const string Dealer = "Dealer";

    /// <summary>Internal OEM users (everyone except dealer technicians).</summary>
    public const string Internal = Admin + "," + Author + "," + Approver;
    public const string SopEditors = Admin + "," + Author;
    public const string SopApprovers = Admin + "," + Approver;

    public static readonly string[] All = { Admin, Author, Approver, Dealer };
}

public static class SopStatus
{
    public const string Draft = "Draft";
    public const string UnderReview = "UnderReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Published = "Published";
    public const string Obsolete = "Obsolete";

    public static readonly string[] All = { Draft, UnderReview, Approved, Rejected, Published, Obsolete };

    /// <summary>Statuses in which the SOP content (header, steps, resources, attachments) may be edited.</summary>
    public static bool IsEditable(string status) => status is Draft or Rejected;

    public static string Display(string status) => status switch
    {
        UnderReview => "Under Review",
        _ => status
    };
}

public static class ActivityTypes
{
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        ["PDI"] = "Pre-Delivery Inspection",
        ["PeriodicMaintenance"] = "Periodic Maintenance",
        ["Repair"] = "Repair",
        ["Inspection"] = "Inspection / Diagnosis",
        ["Warranty"] = "Warranty",
        ["Campaign"] = "Campaign / Recall"
    };

    public static string Display(string code) => All.TryGetValue(code, out var name) ? name : code;
}

public static class ResourceTypes
{
    public const string Part = "Part";
    public const string Tool = "Tool";
    public const string Consumable = "Consumable";

    public static readonly string[] All = { Part, Tool, Consumable };
}

public static class AssemblyLevels
{
    public static readonly IReadOnlyDictionary<short, string> All = new Dictionary<short, string>
    {
        [1] = "Assembly",
        [2] = "Sub-assembly",
        [3] = "Component"
    };

    public static string Display(short level) => All.TryGetValue(level, out var name) ? name : level.ToString();
}

/// <summary>The four fixed levels of the product hierarchy above assemblies.</summary>
public enum MasterType
{
    Division,
    Category,
    Model,
    Variant
}

public static class MasterTypes
{
    public static string Display(MasterType type) => type switch
    {
        MasterType.Division => "Division",
        MasterType.Category => "Model Category",
        MasterType.Model => "Model",
        MasterType.Variant => "Variant",
        _ => type.ToString()
    };

    public static string DisplayPlural(MasterType type) => type switch
    {
        MasterType.Division => "Divisions",
        MasterType.Category => "Model Categories",
        MasterType.Model => "Models",
        MasterType.Variant => "Variants",
        _ => type.ToString()
    };

    /// <summary>The parent level of a master type, or null for Division (the root).</summary>
    public static MasterType? Parent(MasterType type) => type switch
    {
        MasterType.Category => MasterType.Division,
        MasterType.Model => MasterType.Category,
        MasterType.Variant => MasterType.Model,
        _ => null
    };
}

public static class AuditEntities
{
    public const string Sop = "Sop";
    public const string Master = "Master";
    public const string Assembly = "Assembly";
    public const string Dealer = "Dealer";
    public const string User = "User";
}
