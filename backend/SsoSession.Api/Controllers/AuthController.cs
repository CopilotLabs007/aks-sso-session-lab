using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using SsoSession.Api.Authentication;
using SsoSession.Api.Contracts;

namespace SsoSession.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AuthFlowStore flowStore,
    DemoIdpSettings idpSettings,
    FrontendSettings frontendSettings,
    JwtSettings jwtSettings,
    JwtTokenService tokenService,
    IWebHostEnvironment environment,
    ILogger<AuthController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("sso-login")]
    public IActionResult SsoLogin()
    {
        logger.LogInformation("SSO authentication initiated.");
        var state = flowStore.CreateState();
        var loginUrl = QueryHelpers.AddQueryString(
            $"{frontendSettings.BaseUrl.TrimEnd('/')}/simulated-idp-login",
            "state",
            state);

        return Redirect(loginUrl);
    }

    [AllowAnonymous]
    [HttpPost("sso-authenticate")]
    public IActionResult SsoAuthenticate([FromBody] AuthenticateRequest request)
    {
        if (!idpSettings.IsConfigured)
        {
            logger.LogWarning("Simulated IdP authentication attempted but demo credentials are not configured.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse("Simulated IdP demo credentials are not configured. Set them with dotnet user-secrets (see README)."));
        }

        if (!CredentialsMatch(request.Username, request.Password))
        {
            logger.LogWarning("Simulated IdP authentication failed.");
            return Unauthorized(new ErrorResponse("The demo username or password is incorrect."));
        }

        if (!flowStore.TryIssueAuthorizationCode(request.State, request.Username, out var code))
        {
            logger.LogWarning("Simulated IdP authentication rejected an invalid or expired state.");
            return BadRequest(new ErrorResponse("The sign-in state is invalid or expired. Start login again."));
        }

        logger.LogInformation("Simulated IdP authentication completed.");
        var callbackUrl = QueryHelpers.AddQueryString(
            $"{frontendSettings.BaseUrl.TrimEnd('/')}/auth-callback",
            new Dictionary<string, string?>
            {
                ["code"] = code,
                ["state"] = request.State
            });

        return Ok(new AuthenticationRedirectResponse(callbackUrl));
    }

    [AllowAnonymous]
    [HttpPost("create-session")]
    public IActionResult CreateSession([FromBody] CreateSessionRequest request)
    {
        logger.LogInformation("CreateSession method entered.");
        try
        {
            if (!flowStore.TryConsumeAuthorizationCode(request.Code, request.State, out var username))
            {
                logger.LogWarning("CreateSession rejected an invalid, expired, or previously used authorization transaction.");
                return BadRequest(new ErrorResponse("The authorization code or state is invalid, expired, or already used."));
            }

            var createdAt = DateTimeOffset.UtcNow;
            var expiresAt = createdAt.AddSeconds(jwtSettings.TokenLifetimeSeconds);
            var issuedJwt = tokenService.Create(username, Guid.NewGuid().ToString("N"), createdAt, expiresAt);
            logger.LogInformation("JWT generation completed.");

            SetAuthenticationCookie(issuedJwt);
            logger.LogInformation("Set-Cookie response generated.");

            return Ok(new SessionResponse(
                username,
                ReadSessionId(issuedJwt.Value),
                createdAt,
                expiresAt,
                jwtSettings.TokenLifetimeSeconds,
                "active"));
        }
        finally
        {
            logger.LogInformation("CreateSession method exited.");
        }
    }

    [Authorize]
    [HttpGet("session")]
    public IActionResult GetSession()
    {
        logger.LogInformation("Session validation requested.");
        var session = ReadCurrentSession();
        if (session is null)
        {
            logger.LogWarning("Session validation failed because required session claims are missing.");
            return Unauthorized(new ErrorResponse("The session is invalid."));
        }

        logger.LogInformation("Session validation successful.");
        return Ok(session);
    }

    [Authorize]
    [HttpPost("refresh-session")]
    public IActionResult RefreshSession()
    {
        var currentSession = ReadCurrentSession();
        if (currentSession is null)
        {
            logger.LogWarning("Session refresh rejected because required session claims are missing.");
            return Unauthorized(new ErrorResponse("The session is invalid."));
        }

        var now = DateTimeOffset.UtcNow;
        var maximumExpiresAt = currentSession.CreatedAt.AddSeconds(jwtSettings.MaxSessionAgeSeconds);
        if (now >= maximumExpiresAt)
        {
            logger.LogInformation("Session refresh rejected because the maximum session age was reached.");
            return Unauthorized(new ErrorResponse("The maximum session age was reached. Sign in again."));
        }

        var expiresAt = Min(
            now.AddSeconds(jwtSettings.TokenLifetimeSeconds),
            maximumExpiresAt);
        var issuedJwt = tokenService.Create(
            currentSession.Username,
            currentSession.SessionId,
            currentSession.CreatedAt,
            expiresAt);
        SetAuthenticationCookie(issuedJwt);
        logger.LogInformation("Session refreshed and authentication cookie replaced.");

        return Ok(currentSession with
        {
            ExpiresAt = expiresAt,
            RemainingSeconds = (long)Math.Ceiling((expiresAt - now).TotalSeconds)
        });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(jwtSettings.CookieName, CreateCookieOptions());
        logger.LogInformation("Logout completed and authentication cookie cleared.");
        return NoContent();
    }

    private bool CredentialsMatch(string username, string password)
    {
        if (!string.Equals(username, idpSettings.Username, StringComparison.Ordinal))
        {
            return false;
        }

        var suppliedPassword = Encoding.UTF8.GetBytes(password);
        var expectedPassword = Encoding.UTF8.GetBytes(idpSettings.Password!);
        try
        {
            return suppliedPassword.Length == expectedPassword.Length
                && CryptographicOperations.FixedTimeEquals(suppliedPassword, expectedPassword);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(suppliedPassword);
            CryptographicOperations.ZeroMemory(expectedPassword);
        }
    }

    private SessionResponse? ReadCurrentSession()
    {
        var username = User.FindFirst(JwtRegisteredClaimNames.Name)?.Value;
        var sessionId = User.FindFirst("sid")?.Value;
        var createdAtValue = User.FindFirst("auth_time")?.Value;
        var expiresAtValue = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

        if (string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(sessionId)
            || !long.TryParse(createdAtValue, NumberStyles.None, CultureInfo.InvariantCulture, out var createdAtSeconds)
            || !long.TryParse(expiresAtValue, NumberStyles.None, CultureInfo.InvariantCulture, out var expiresAtSeconds))
        {
            return null;
        }

        var createdAt = DateTimeOffset.FromUnixTimeSeconds(createdAtSeconds);
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtSeconds);
        var remainingSeconds = Math.Max(
            0,
            (long)Math.Ceiling((expiresAt - DateTimeOffset.UtcNow).TotalSeconds));

        return new SessionResponse(username, sessionId, createdAt, expiresAt, remainingSeconds, "active");
    }

    private void SetAuthenticationCookie(IssuedJwt issuedJwt) =>
        Response.Cookies.Append(
            jwtSettings.CookieName,
            issuedJwt.Value,
            CreateCookieOptions(issuedJwt.ExpiresAt));

    private CookieOptions CreateCookieOptions(DateTimeOffset? expiresAt = null) =>
        new()
        {
            HttpOnly = true,
            Secure = !(environment.IsDevelopment() && !Request.IsHttps),
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expiresAt,
            IsEssential = true
        };

    private static string ReadSessionId(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .First(claim => claim.Type == "sid").Value;

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) =>
        first <= second ? first : second;
}
