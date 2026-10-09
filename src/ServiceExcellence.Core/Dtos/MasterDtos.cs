using System.ComponentModel.DataAnnotations;
using ServiceExcellence.Core.Common;

namespace ServiceExcellence.Core.Dtos;

/// <summary>A row from one of the hierarchy tables: divisions, model categories, models or variants.</summary>
public class MasterItemDto
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string? ParentName { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MasterSaveRequest
{
    /// <summary>Parent id (division for a category, category for a model, model for a variant). Ignored for divisions.</summary>
    [Display(Name = "Parent")]
    public int? ParentId { get; set; }

    [Required, StringLength(30)]
    public string Code { get; set; } = "";

    [Required, StringLength(150)]
    public string Name { get; set; } = "";

    [StringLength(500)]
    public string? Description { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public class MasterQuery : PagedQuery
{
    public int? ParentId { get; set; }
    public bool? IsActive { get; set; }
}

public class AssemblyDto
{
    public int Id { get; set; }
    public int VariantId { get; set; }
    public string? VariantName { get; set; }
    public int? ParentId { get; set; }
    public string? ParentName { get; set; }
    public short Level { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Full path from the top-level assembly, e.g. "Engine / Fuel System / Fuel Injector".</summary>
    public string? Path { get; set; }
}

public class AssemblySaveRequest
{
    [Required, Display(Name = "Variant")]
    public int? VariantId { get; set; }

    /// <summary>Parent assembly. Empty for a top-level assembly; level is derived from the parent.</summary>
    [Display(Name = "Parent assembly")]
    public int? ParentId { get; set; }

    [Required, StringLength(40)]
    public string Code { get; set; } = "";

    [Required, StringLength(150)]
    public string Name { get; set; } = "";

    [StringLength(500)]
    public string? Description { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public class AssemblyQuery : PagedQuery
{
    public int? VariantId { get; set; }
    public short? Level { get; set; }
}

/// <summary>The ids of every level of a product classification, from division down to assembly.</summary>
public class ClassificationPath
{
    public int DivisionId { get; set; }
    public int? CategoryId { get; set; }
    public int? ModelId { get; set; }
    public int? VariantId { get; set; }
    public int? AssemblyId { get; set; }
}
