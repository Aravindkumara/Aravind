using System.ComponentModel.DataAnnotations;
using ServiceExcellence.Core.Common;

namespace ServiceExcellence.Core.Dtos;

public class SopListItemDto
{
    public int Id { get; set; }
    public string SopCode { get; set; } = "";
    public int Version { get; set; }
    public string Title { get; set; } = "";
    public string ActivityType { get; set; } = "";
    public string Status { get; set; } = "";
    public string DivisionName { get; set; } = "";
    public string? CategoryName { get; set; }
    public string? ModelName { get; set; }
    public string? VariantName { get; set; }
    public string? AssemblyName { get; set; }
    public int StandardTimeMinutes { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>The most specific classification level this SOP is attached to.</summary>
    public string AppliesTo =>
        AssemblyName ?? VariantName ?? ModelName ?? CategoryName ?? DivisionName;

    public string AppliesToLevel =>
        AssemblyName != null ? "Assembly" :
        VariantName != null ? "Variant" :
        ModelName != null ? "Model" :
        CategoryName != null ? "Model Category" : "Division";
}

public class SopDetailDto : SopListItemDto
{
    public string? Purpose { get; set; }
    public int DivisionId { get; set; }
    public int? CategoryId { get; set; }
    public int? ModelId { get; set; }
    public int? VariantId { get; set; }
    public int? AssemblyId { get; set; }
    public string? AssemblyPath { get; set; }
    public string? SkillLevel { get; set; }
    public string? SafetyNotes { get; set; }
    public string? ReviewRemarks { get; set; }
    public int? PreviousVersionId { get; set; }
    public int CreatedBy { get; set; }
    public string? UpdatedByName { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public List<SopStepDto> Steps { get; set; } = new();
    public List<SopResourceDto> Resources { get; set; } = new();

    public int TotalStepMinutes => Steps.Sum(s => s.EstimatedMinutes);
}

public class SopSaveRequest
{
    [Required, StringLength(40), Display(Name = "SOP code")]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Use letters, digits, '.', '_' or '-' only.")]
    public string SopCode { get; set; } = "";

    [Required, StringLength(250)]
    public string Title { get; set; } = "";

    [Required, Display(Name = "Service activity")]
    public string ActivityType { get; set; } = "";

    public string? Purpose { get; set; }

    [Required, Display(Name = "Division")]
    public int? DivisionId { get; set; }

    [Display(Name = "Model category")]
    public int? CategoryId { get; set; }

    [Display(Name = "Model")]
    public int? ModelId { get; set; }

    [Display(Name = "Variant")]
    public int? VariantId { get; set; }

    [Display(Name = "Assembly")]
    public int? AssemblyId { get; set; }

    [Range(0, 100000), Display(Name = "Standard labour time (minutes)")]
    public int StandardTimeMinutes { get; set; }

    [StringLength(40), Display(Name = "Skill level")]
    public string? SkillLevel { get; set; }

    [Display(Name = "Safety notes")]
    public string? SafetyNotes { get; set; }
}

public class SopStepDto
{
    public int Id { get; set; }
    public int SopId { get; set; }
    public int StepNo { get; set; }
    public string Title { get; set; } = "";
    public string Instruction { get; set; } = "";
    public string? ToolsRequired { get; set; }
    public string? Specification { get; set; }
    public string? Caution { get; set; }
    public int EstimatedMinutes { get; set; }
    public List<AttachmentDto> Attachments { get; set; } = new();
}

public class SopStepSaveRequest
{
    /// <summary>Position of the step. When 0 or omitted on create, the step is added at the end.</summary>
    [Range(0, 1000), Display(Name = "Step no.")]
    public int StepNo { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = "";

    [Required]
    public string Instruction { get; set; } = "";

    [StringLength(500), Display(Name = "Tools required")]
    public string? ToolsRequired { get; set; }

    [StringLength(500), Display(Name = "Specification / torque")]
    public string? Specification { get; set; }

    [StringLength(1000)]
    public string? Caution { get; set; }

    [Range(0, 10000), Display(Name = "Estimated minutes")]
    public int EstimatedMinutes { get; set; }
}

public class SopResourceDto
{
    public int Id { get; set; }
    public int SopId { get; set; }
    public string ResourceType { get; set; } = "";
    public string? PartNumber { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public string? Uom { get; set; }
}

public class SopResourceSaveRequest
{
    [Required, Display(Name = "Type")]
    public string ResourceType { get; set; } = "";

    [StringLength(60), Display(Name = "Part / tool no.")]
    public string? PartNumber { get; set; }

    [Required, StringLength(250)]
    public string Description { get; set; } = "";

    [Range(0.01, 100000)]
    public decimal Quantity { get; set; } = 1;

    [StringLength(20), Display(Name = "UoM")]
    public string? Uom { get; set; }
}

public class AttachmentDto
{
    public int Id { get; set; }
    public int StepId { get; set; }
    public string FileName { get; set; } = "";
    public string StoredName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime UploadedAt { get; set; }

    public bool IsImage => ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}

public class WorkflowActionRequest
{
    [StringLength(2000)]
    public string? Remarks { get; set; }
}

public class SopQuery : PagedQuery
{
    public string? Status { get; set; }
    public string? ActivityType { get; set; }
    public int? DivisionId { get; set; }
    public int? CategoryId { get; set; }
    public int? ModelId { get; set; }
    public int? VariantId { get; set; }
}

/// <summary>
/// Finds published SOPs that apply to a product. The most specific id supplied is used; the
/// result includes SOPs defined at that level, at any level above it, and at any assembly below it.
/// </summary>
public class SopFinderQuery : PagedQuery
{
    public int? DivisionId { get; set; }
    public int? CategoryId { get; set; }
    public int? ModelId { get; set; }
    public int? VariantId { get; set; }
    public int? AssemblyId { get; set; }
    public string? ActivityType { get; set; }
}
