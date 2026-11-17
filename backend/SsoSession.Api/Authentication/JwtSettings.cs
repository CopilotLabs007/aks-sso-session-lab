using System.Security.Cryptography;

namespace SsoSession.Api.Authentication;

public sealed record JwtSettings(
    string Issuer,
    string Audience,
    string CookieName,
    int TokenLifetimeSeconds,
    int MaxSessionAgeSeconds,
    byte[] SigningKey,
    bool UsesEphemeralSigningKey)
{
    public static JwtSettings Load(IConfiguration configuration, IHostEnvironment environment)
    {
        var signingKeyValue = configuration["Jwt:SigningKey"];
        byte[] signingKey;
        var ephemeral = false;

        if (string.IsNullOrWhiteSpace(signingKeyValue))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("Jwt:SigningKey is required outside Development.");
            }

            // Local-only fallback: random in-memory key, never persisted or logged.
            // Restarting the API invalidates all previously issued JWTs.
            signingKey = RandomNumberGenerator.GetBytes(64);
            ephemeral = true;
        }
        else
        {
            try
            {
                signingKey = Convert.FromBase64String(signingKeyValue);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("Jwt:SigningKey must be a Base64-encoded key.", exception);
            }

            if (signingKey.Length < 32)
            {
                throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 random bytes.");
            }
        }

        var tokenLifetimeSeconds = configuration.GetValue("Jwt:TokenLifetimeSeconds", 360);
        var maxSessionAgeSeconds = configuration.GetValue("Jwt:MaxSessionAgeSeconds", 1800);
        if (tokenLifetimeSeconds <= 0 || maxSessionAgeSeconds < tokenLifetimeSeconds)
        {
            throw new InvalidOperationException("JWT lifetime settings are invalid.");
        }

        return new JwtSettings(
            configuration["Jwt:Issuer"] ?? "SsoSessionLab",
            configuration["Jwt:Audience"] ?? "SsoSessionLabApi",
            configuration["Jwt:CookieName"] ?? "sso_session",
            tokenLifetimeSeconds,
            maxSessionAgeSeconds,
            signingKey,
            ephemeral);
    }
}

public sealed record DemoIdpSettings(string? Username, string? Password)
{
    public bool IsConfigured =>
        !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

    public static DemoIdpSettings Load(IConfiguration configuration) =>
        new(configuration["DemoIdp:Username"], configuration["DemoIdp:Password"]);
}
