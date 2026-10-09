using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceExcellence.Api.Infrastructure;
using ServiceExcellence.Api.Services;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;
using ServiceExcellence.Core.Interfaces;

namespace ServiceExcellence.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly ITokenService _tokens;
    private readonly IAuditService _audit;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserRepository users, ITokenService tokens, IAuditService audit, ILogger<AuthController> logger)
    {
        _users = users;
        _tokens = tokens;
        _audit = audit;
        _logger = logger;
    }

    /// <summary>Authenticates a user and returns a JWT bearer token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await _users.GetCredentialsByUsernameAsync(request.Username.Trim());
        if (user == null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login for {Username}", request.Username);
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid username or password.");
        }

        await _users.SetLastLoginAsync(user.Id);
        _logger.LogInformation("User {Username} logged in", user.Username);
        return _tokens.CreateToken(user);
    }

    /// <summary>Returns the profile of the signed-in user.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await _users.GetByIdAsync(User.GetUserId());
        return user == null ? NotFound() : user;
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await _users.GetCredentialsByIdAsync(User.GetUserId()) ?? throw new NotFoundException("User not found.");
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ValidationException("The current password is incorrect.", nameof(request.CurrentPassword));

        await _users.UpdatePasswordAsync(user.Id, BCrypt.Net.BCrypt.HashPassword(request.NewPassword));
        await _audit.LogAsync(AuditEntities.User, user.Id, "PasswordChanged", $"{user.Username} changed their password");
        return NoContent();
    }
}
