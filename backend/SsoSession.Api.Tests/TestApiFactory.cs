using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace SsoSession.Api.Tests;

// Hosts the real API in memory with throwaway, per-run configuration.
// The "Testing" environment keeps local User Secrets from being loaded.
public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "TestIssuer";
    public const string Audience = "TestAudience";
    public const string CookieName = "sso_session";
    public const string FrontendBaseUrl = "http://localhost:4200";
    public const string Username = "test-user";

    public string Password { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
    public byte[] SigningKey { get; } = RandomNumberGenerator.GetBytes(64);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:CookieName", CookieName);
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(SigningKey));
        builder.UseSetting("Jwt:TokenLifetimeSeconds", "360");
        builder.UseSetting("Jwt:MaxSessionAgeSeconds", "1800");
        builder.UseSetting("DemoIdp:Username", Username);
        builder.UseSetting("DemoIdp:Password", Password);
        builder.UseSetting("Frontend:BaseUrl", FrontendBaseUrl);
    }

    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    public string CreateMockToken(
        DateTimeOffset expiresAt,
        DateTimeOffset? authTime = null,
        string issuer = Issuer,
        string audience = Audience,
        byte[]? signingKey = null,
        bool signed = true,
        string sessionId = "mock-session-id")
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Username),
            new Claim(JwtRegisteredClaimNames.Name, Username),
            new Claim("sid", sessionId),
            new Claim("auth_time", (authTime ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        var credentials = signed
            ? new SigningCredentials(new SymmetricSecurityKey(signingKey ?? SigningKey), SecurityAlgorithms.HmacSha256)
            : null;
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expiresAt.AddMinutes(-10).UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
