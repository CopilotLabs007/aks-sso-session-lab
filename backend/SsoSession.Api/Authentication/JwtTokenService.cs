using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Globalization;
using Microsoft.IdentityModel.Tokens;

namespace SsoSession.Api.Authentication;

public sealed class JwtTokenService(JwtSettings settings)
{
    public IssuedJwt Create(string username, string sessionId, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Name, username),
            new Claim("sid", sessionId),
            new Claim("auth_time", createdAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(settings.SigningKey),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            claims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            signingCredentials);
        var value = new JwtSecurityTokenHandler().WriteToken(jwt);

        return new IssuedJwt(value, expiresAt);
    }
}

public sealed record IssuedJwt(string Value, DateTimeOffset ExpiresAt);
