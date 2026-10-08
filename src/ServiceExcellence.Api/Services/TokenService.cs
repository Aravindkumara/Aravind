using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ServiceExcellence.Api.Infrastructure;
using ServiceExcellence.Core.Dtos;

namespace ServiceExcellence.Api.Services;

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "ServiceExcellence.Api";
    public string Audience { get; set; } = "ServiceExcellence.Web";

    /// <summary>HMAC signing key; must be at least 32 characters.</summary>
    public string Key { get; set; } = "";

    public int ExpiryMinutes { get; set; } = 480;

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));
}

public interface ITokenService
{
    LoginResponse CreateToken(UserCredentials user);
}

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public LoginResponse CreateToken(UserCredentials user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(AppClaims.UserId, user.Id.ToString()),
            new(AppClaims.Username, user.Username),
            new(AppClaims.FullName, user.FullName),
            new(AppClaims.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (user.DealerId.HasValue)
            claims.Add(new Claim(AppClaims.DealerId, user.DealerId.Value.ToString()));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            SigningCredentials = new SigningCredentials(_options.SigningKey, SecurityAlgorithms.HmacSha256)
        };

        return new LoginResponse
        {
            Token = new JsonWebTokenHandler().CreateToken(descriptor),
            ExpiresAtUtc = expires,
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            DealerId = user.DealerId
        };
    }
}
