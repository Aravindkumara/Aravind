using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;

namespace ServiceExcellence.Web.Infrastructure;

/// <summary>An error response from the Web API, carrying its problem details.</summary>
public class ApiException : Exception
{
    public ApiException(HttpStatusCode statusCode, string message, IDictionary<string, string[]>? errors = null) : base(message)
    {
        StatusCode = statusCode;
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public HttpStatusCode StatusCode { get; }
    public IDictionary<string, string[]> Errors { get; }
}

/// <summary>Thin JSON client for the Service Excellence Web API. Errors are raised as <see cref="ApiException"/>.</summary>
public class ApiClient
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;

    public ApiClient(HttpClient http) => _http = http;

    public async Task<T> GetAsync<T>(string url, object? query = null)
    {
        using var response = await _http.GetAsync(WithQuery(url, query));
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    public async Task<T> PostAsync<T>(string url, object? body = null)
    {
        using var response = await _http.PostAsJsonAsync(url, body ?? new { }, JsonOptions);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    public async Task PostAsync(string url, object? body = null)
    {
        using var response = await _http.PostAsJsonAsync(url, body ?? new { }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task PutAsync(string url, object body)
    {
        using var response = await _http.PutAsJsonAsync(url, body, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task DeleteAsync(string url)
    {
        using var response = await _http.DeleteAsync(url);
        await EnsureSuccessAsync(response);
    }

    public async Task UploadAsync(string url, IFormFile file)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(fileContent, "file", file.FileName);

        using var response = await _http.PostAsync(url, content);
        await EnsureSuccessAsync(response);
    }

    /// <summary>Downloads a binary resource (attachment or PDF). The caller owns the returned response.</summary>
    public async Task<HttpResponseMessage> GetFileAsync(string url)
    {
        var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            using (response) await EnsureSuccessAsync(response);
        }
        return response;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        string message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
            HttpStatusCode.Forbidden => "You do not have permission to perform this action.",
            HttpStatusCode.NotFound => "The requested record was not found.",
            _ => "The request could not be completed."
        };
        IDictionary<string, string[]>? errors = null;

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOptions);
            if (problem != null)
            {
                message = problem.Detail ?? problem.Title ?? message;
                errors = problem.Errors;
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            // Not a problem-details body; keep the generic message.
        }

        throw new ApiException(response.StatusCode, message, errors);
    }

    /// <summary>Appends the non-null public properties of <paramref name="query"/> as a query string.</summary>
    public static string WithQuery(string url, object? query)
    {
        if (query == null) return url;
        var builder = new QueryBuilder();
        var type = query.GetType();
        // Anonymous types have read-only properties; on DTOs, read-only properties are computed and skipped.
        var isAnonymous = type.Name.Contains("AnonymousType", StringComparison.Ordinal);
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || (!prop.CanWrite && !isAnonymous)) continue;
            var value = prop.GetValue(query);
            if (value == null || (value is string s && string.IsNullOrWhiteSpace(s))) continue;
            builder.Add(prop.Name, value is DateTime d ? d.ToString("yyyy-MM-dd") : value.ToString()!);
        }
        return url + builder.ToQueryString();
    }

    private sealed class ProblemResponse
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}

/// <summary>Adds the signed-in user's API token to every outgoing API request.</summary>
public class BearerTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _http;

    public BearerTokenHandler(IHttpContextAccessor http) => _http = http;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _http.HttpContext?.User.FindFirst(WebClaims.AccessToken)?.Value;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, cancellationToken);
    }
}

public static class WebClaims
{
    public const string AccessToken = "access_token";
    public const string UserId = "user_id";
    public const string FullName = "full_name";
    public const string DealerId = "dealer_id";
}
