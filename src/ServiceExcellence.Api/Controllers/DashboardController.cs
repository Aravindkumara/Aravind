using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceExcellence.Api.Infrastructure;
using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Core.Interfaces;

namespace ServiceExcellence.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ISopRepository _sops;

    public DashboardController(ISopRepository sops) => _sops = sops;

    [HttpGet]
    public Task<DashboardDto> Get() => _sops.GetDashboardAsync(User.IsDealer());
}

[ApiController]
[Route("api/audit")]
[Authorize(Roles = Roles.Admin)]
public class AuditController : ControllerBase
{
    private readonly IAuditRepository _audit;

    public AuditController(IAuditRepository audit) => _audit = audit;

    [HttpGet]
    public Task<PagedResult<AuditLogDto>> Search([FromQuery] AuditQuery query) => _audit.SearchAsync(query);
}
