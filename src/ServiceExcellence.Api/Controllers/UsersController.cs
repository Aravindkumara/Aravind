using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceExcellence.Api.Infrastructure;
using ServiceExcellence.Api.Services;
using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Core.Interfaces;

namespace ServiceExcellence.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IDealerRepository _dealers;
    private readonly IAuditService _audit;

    public UsersController(IUserRepository users, IDealerRepository dealers, IAuditService audit)
    {
        _users = users;
        _dealers = dealers;
        _audit = audit;
    }

    [HttpGet]
    public Task<PagedResult<UserDto>> Search([FromQuery] UserQuery query) => _users.SearchAsync(query);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> Get(int id) =>
        await _users.GetByIdAsync(id) ?? throw new NotFoundException("User not found.");

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(UserSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("A password is required for a new user.", nameof(request.Password));
        await ValidateAsync(request, null);

        var id = await _users.CreateAsync(request, BCrypt.Net.BCrypt.HashPassword(request.Password));
        await _audit.LogAsync(AuditEntities.User, id, "Created", $"User {request.Username} ({request.Role}) created");
        return CreatedAtAction(nameof(Get), new { id }, await _users.GetByIdAsync(id));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UserSaveRequest request)
    {
        var existing = await _users.GetByIdAsync(id) ?? throw new NotFoundException("User not found.");
        if (id == User.GetUserId() && (!request.IsActive || request.Role != Roles.Admin))
            throw new ValidationException("You cannot deactivate yourself or remove your own Admin role.", nameof(request.Role));
        await ValidateAsync(request, id);

        var hash = string.IsNullOrWhiteSpace(request.Password) ? null : BCrypt.Net.BCrypt.HashPassword(request.Password);
        await _users.UpdateAsync(id, request, hash);

        var changes = new List<string>();
        if (existing.Role != request.Role) changes.Add($"role {existing.Role} -> {request.Role}");
        if (existing.IsActive != request.IsActive) changes.Add(request.IsActive ? "activated" : "deactivated");
        if (hash != null) changes.Add("password reset");
        await _audit.LogAsync(AuditEntities.User, id, "Updated",
            $"User {request.Username} updated" + (changes.Count > 0 ? ": " + string.Join(", ", changes) : ""));
        return NoContent();
    }

    private async Task ValidateAsync(UserSaveRequest request, int? id)
    {
        if (!Roles.All.Contains(request.Role))
            throw new ValidationException("Select a valid role.", nameof(request.Role));
        if (await _users.UsernameExistsAsync(request.Username, id))
            throw new ValidationException($"Username '{request.Username}' is already taken.", nameof(request.Username));

        if (request.Role == Roles.Dealer)
        {
            if (request.DealerId is not { } dealerId || await _dealers.GetByIdAsync(dealerId) == null)
                throw new ValidationException("Select the dealer this technician belongs to.", nameof(request.DealerId));
        }
        else
        {
            request.DealerId = null;
        }
    }
}
