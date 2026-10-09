namespace ServiceExcellence.Core.Common;

/// <summary>Search, paging and sorting parameters shared by every list endpoint.</summary>
public class PagedQuery
{
    private int _page = 1;
    private int _pageSize = 10;

    public string? Search { get; set; }

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > 100 ? 10 : value;
    }

    public string? SortBy { get; set; }

    /// <summary>"asc" or "desc".</summary>
    public string? SortDir { get; set; }

    public bool Descending => string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase);

    public int Offset => (Page - 1) * PageSize;
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public class LookupItem
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    public string Text => string.IsNullOrEmpty(Code) ? Name : $"{Code} - {Name}";
}
