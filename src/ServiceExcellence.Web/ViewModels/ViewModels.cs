using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;

namespace ServiceExcellence.Web.ViewModels;

public class LoginViewModel : LoginRequest
{
    public string? ReturnUrl { get; set; }
}

public class ListViewModel<TItem, TQuery> where TQuery : PagedQuery
{
    public TQuery Query { get; set; } = default!;
    public PagedResult<TItem> Result { get; set; } = new();
}

public class MasterIndexViewModel : ListViewModel<MasterItemDto, MasterQuery>
{
    public MasterType Type { get; set; }
    public IReadOnlyList<LookupItem> Parents { get; set; } = Array.Empty<LookupItem>();
}

public class MasterFormViewModel
{
    public MasterType Type { get; set; }
    public int? Id { get; set; }
    public MasterSaveRequest Item { get; set; } = new();
    public IReadOnlyList<LookupItem> Parents { get; set; } = Array.Empty<LookupItem>();
}

public class AssemblyIndexViewModel : ListViewModel<AssemblyDto, AssemblyQuery>
{
    public IReadOnlyList<LookupItem> Variants { get; set; } = Array.Empty<LookupItem>();
}

public class AssemblyFormViewModel
{
    public int? Id { get; set; }
    public AssemblySaveRequest Item { get; set; } = new();
    public IReadOnlyList<LookupItem> Variants { get; set; } = Array.Empty<LookupItem>();
}

public class UserFormViewModel
{
    public int? Id { get; set; }
    public UserSaveRequest Item { get; set; } = new();
    public IReadOnlyList<LookupItem> Dealers { get; set; } = Array.Empty<LookupItem>();
}

public class SopIndexViewModel : ListViewModel<SopListItemDto, SopQuery>
{
    public IReadOnlyList<LookupItem> Divisions { get; set; } = Array.Empty<LookupItem>();
}

public class SopFormViewModel
{
    public int? Id { get; set; }
    public int Version { get; set; } = 1;
    public SopSaveRequest Item { get; set; } = new();
    public IReadOnlyList<LookupItem> Divisions { get; set; } = Array.Empty<LookupItem>();
}

public class SopDetailsViewModel
{
    public SopDetailDto Sop { get; set; } = new();
    public IReadOnlyList<AuditLogDto> History { get; set; } = Array.Empty<AuditLogDto>();
    public SopResourceSaveRequest NewResource { get; set; } = new();

    public bool CanEdit { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanReview { get; set; }
    public bool CanPublish { get; set; }
    public bool CanObsolete { get; set; }
    public bool CanCreateVersion { get; set; }
    public bool CanDelete { get; set; }
}

public class StepFormViewModel
{
    public int SopId { get; set; }
    public string SopCode { get; set; } = "";
    public string SopTitle { get; set; } = "";
    public int? StepId { get; set; }
    public int StepCount { get; set; }
    public SopStepSaveRequest Item { get; set; } = new();
}

public class FinderViewModel
{
    public SopFinderQuery Query { get; set; } = new();
    public PagedResult<SopListItemDto>? Result { get; set; }
    public IReadOnlyList<LookupItem> Divisions { get; set; } = Array.Empty<LookupItem>();
}

/// <summary>Paging information for the shared _Pager partial.</summary>
public record PagerModel(int Page, int PageSize, int TotalCount, int TotalPages)
{
    public static PagerModel From<T>(PagedResult<T> result) => new(result.Page, result.PageSize, result.TotalCount, result.TotalPages);
}

/// <summary>Model for the _ClassificationSelects partial (division -> category -> model -> variant -> assembly).</summary>
public record ClassificationSelectsModel(
    string Prefix,
    IReadOnlyList<LookupItem> Divisions,
    int? DivisionId,
    int? CategoryId,
    int? ModelId,
    int? VariantId,
    int? AssemblyId,
    string ColumnClass = "col-md")
{
    public string Name(string field) => string.IsNullOrEmpty(Prefix) ? field : $"{Prefix}.{field}";
    public string Id(string field) => string.IsNullOrEmpty(Prefix) ? field : $"{Prefix}_{field}";
}
