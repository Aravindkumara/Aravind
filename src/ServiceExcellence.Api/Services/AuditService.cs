using ServiceExcellence.Api.Infrastructure;
using ServiceExcellence.Core.Interfaces;

namespace ServiceExcellence.Api.Services;

/// <summary>Writes audit trail entries attributed to the current API user.</summary>
public interface IAuditService
{
    Task LogAsync(string entityType, int entityId, string action, string? details = null);
}

public class AuditService : IAuditService
{
    private readonly IAuditRepository _audit;
    private readonly IHttpContextAccessor _http;

    public AuditService(IAuditRepository audit, IHttpContextAccessor http)
    {
        _audit = audit;
        _http = http;
    }

    public Task LogAsync(string entityType, int entityId, string action, string? details = null)
    {
        var user = _http.HttpContext?.User;
        int? userId = user?.Identity?.IsAuthenticated == true ? user.GetUserId() : null;
        return _audit.LogAsync(entityType, entityId, action, details, userId, user?.GetUsername());
    }
}
