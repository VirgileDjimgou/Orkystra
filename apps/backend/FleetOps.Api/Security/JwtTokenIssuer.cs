using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FleetOps.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FleetOps.Api.Security;

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

public interface IJwtTokenIssuer
{
    Task<IssuedToken> IssueAsync(ApplicationUser user, string organizationName, CancellationToken cancellationToken);
}

public sealed class JwtTokenIssuer(
    UserManager<ApplicationUser> userManager,
    IOptions<JwtOptions> options) : IJwtTokenIssuer
{
    private readonly JwtOptions _options = options.Value;

    public async Task<IssuedToken> IssueAsync(
        ApplicationUser user,
        string organizationName,
        CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(_options.TokenLifetimeMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(TenantClaimTypes.OrganizationId, user.OrganizationId.ToString()),
            new(TenantClaimTypes.OrganizationName, organizationName),
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
