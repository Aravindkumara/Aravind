using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace ServiceExcellence.Web.Infrastructure;

/// <summary>Builds list URLs that keep the current search and filter values.</summary>
public static class UrlHelperExtensions
{
    public static string SortUrl(this IUrlHelper url, string sortBy)
    {
        var query = url.ActionContext.HttpContext.Request.Query;
        var currentSort = query["sortBy"].ToString();
        var currentDir = query["sortDir"].ToString();
        var dir = string.Equals(currentSort, sortBy, StringComparison.OrdinalIgnoreCase) && currentDir != "desc" ? "desc" : "asc";
        return WithQuery(url, new() { ["sortBy"] = sortBy, ["sortDir"] = dir, ["page"] = "1" });
    }

    public static string PageUrl(this IUrlHelper url, int page) => WithQuery(url, new() { ["page"] = page.ToString() });

    /// <summary>Tabler table-sort state class for a column header ("asc", "desc" or empty).</summary>
    public static string SortClass(this IUrlHelper url, string sortBy)
    {
        var query = url.ActionContext.HttpContext.Request.Query;
        if (!string.Equals(query["sortBy"].ToString(), sortBy, StringComparison.OrdinalIgnoreCase)) return "";
        return query["sortDir"] == "desc" ? "desc" : "asc";
    }

    private static string WithQuery(IUrlHelper url, Dictionary<string, string> overrides)
    {
        var request = url.ActionContext.HttpContext.Request;
        var values = request.Query.ToDictionary(q => q.Key, q => q.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in overrides)
            values[key] = new StringValues(value);

        var builder = new QueryBuilder();
        foreach (var (key, value) in values)
            foreach (var v in value.Where(v => !string.IsNullOrEmpty(v)))
                builder.Add(key, v!);
        return request.PathBase + request.Path + builder.ToQueryString();
    }
}
