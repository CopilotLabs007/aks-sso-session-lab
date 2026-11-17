using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using SsoSession.Api.Contracts;
using HeaderSameSiteMode = Microsoft.Net.Http.Headers.SameSiteMode;

namespace SsoSession.Api.Tests;

public sealed class AuthFlowTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client = factory.CreateApiClient();

    [Fact]
    public async Task Health_returns_ok()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Full_sign_in_issues_httponly_cookie_that_validates_session()
    {
        var state = await StartLoginAsync();
        var code = await AuthenticateAsync(state);

        var response = await _client.PostAsJsonAsync("/api/auth/create-session", new CreateSessionRequest(code, state));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = GetSessionCookie(response);
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure, "Outside Development the cookie must be Secure.");
        Assert.Equal(HeaderSameSiteMode.Lax, cookie.SameSite);
        Assert.Equal("/", cookie.Path.Value);
        Assert.Null(cookie.Domain.Value);
        Assert.NotNull(cookie.Expires);
        AssertCloseTo(DateTimeOffset.UtcNow.AddSeconds(360), cookie.Expires!.Value);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(cookie.Value.Value!, body);

        var created = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.Equal(TestApiFactory.Username, created!.Username);
        AssertCloseTo(cookie.Expires.Value, created.ExpiresAt);

        var session = await GetSessionAsync(cookie.Value.Value!);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        var sessionBody = await session.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.Equal(TestApiFactory.Username, sessionBody!.Username);
        Assert.Equal(created.SessionId, sessionBody.SessionId);
    }

    [Fact]
    public async Task Sso_login_redirects_to_simulated_idp_with_state()
    {
        var response = await _client.GetAsync("/api/auth/sso-login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal($"{TestApiFactory.FrontendBaseUrl}/simulated-idp-login", location.GetLeftPart(UriPartial.Path));
        Assert.False(string.IsNullOrEmpty(QueryHelpers.ParseQuery(location.Query)["state"]));
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        var state = await StartLoginAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/auth/sso-authenticate",
            new AuthenticateRequest(TestApiFactory.Username, "wrong-password", state));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_state_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/sso-authenticate",
            new AuthenticateRequest(TestApiFactory.Username, factory.Password, "unknown-state"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task State_cannot_be_used_for_two_authentications()
    {
        var state = await StartLoginAsync();
        await AuthenticateAsync(state);

        var second = await _client.PostAsJsonAsync(
            "/api/auth/sso-authenticate",
            new AuthenticateRequest(TestApiFactory.Username, factory.Password, state));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Authorization_code_is_single_use()
    {
        var state = await StartLoginAsync();
        var code = await AuthenticateAsync(state);

        var first = await _client.PostAsJsonAsync("/api/auth/create-session", new CreateSessionRequest(code, state));
        var replay = await _client.PostAsJsonAsync("/api/auth/create-session", new CreateSessionRequest(code, state));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.False(replay.Headers.Contains(HeaderNames.SetCookie));
    }

    [Fact]
    public async Task Authorization_code_with_mismatched_state_is_rejected()
    {
        var state = await StartLoginAsync();
        var code = await AuthenticateAsync(state);

        var response = await _client.PostAsJsonAsync(
            "/api/auth/create-session",
            new CreateSessionRequest(code, "different-state"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains(HeaderNames.SetCookie));
    }

    [Fact]
    public async Task Session_accepts_mock_token_signed_with_configured_key()
    {
        var token = factory.CreateMockToken(DateTimeOffset.UtcNow.AddMinutes(5));

        var response = await GetSessionAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.Equal("mock-session-id", session!.SessionId);
        Assert.InRange(session.RemainingSeconds, 290, 300);
    }

    public static TheoryData<string> InvalidTokenCases =>
    [
        "missing",
        "garbage",
        "tampered-signature",
        "wrong-issuer",
        "wrong-audience",
        "wrong-signing-key",
        "unsigned",
        "expired"
    ];

    [Theory]
    [MemberData(nameof(InvalidTokenCases))]
    public async Task Session_rejects_invalid_tokens(string tokenCase)
    {
        var validExpiry = DateTimeOffset.UtcNow.AddMinutes(5);
        string? token = tokenCase switch
        {
            "missing" => null,
            "garbage" => "not.a.jwt",
            "tampered-signature" => TamperSignature(factory.CreateMockToken(validExpiry)),
            "wrong-issuer" => factory.CreateMockToken(validExpiry, issuer: "OtherIssuer"),
            "wrong-audience" => factory.CreateMockToken(validExpiry, audience: "OtherAudience"),
            "wrong-signing-key" => factory.CreateMockToken(validExpiry, signingKey: RandomNumberGenerator.GetBytes(64)),
            "unsigned" => factory.CreateMockToken(validExpiry, signed: false),
            "expired" => factory.CreateMockToken(DateTimeOffset.UtcNow.AddSeconds(-1)),
            _ => throw new ArgumentOutOfRangeException(nameof(tokenCase))
        };

        var response = await GetSessionAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_replaces_cookie_with_new_token_and_later_expiry()
    {
        var oldExpiry = DateTimeOffset.UtcNow.AddMinutes(1);
        var oldToken = factory.CreateMockToken(oldExpiry, authTime: DateTimeOffset.UtcNow.AddMinutes(-5));

        var response = await SendWithCookieAsync(HttpMethod.Post, "/api/auth/refresh-session", oldToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = GetSessionCookie(response);
        Assert.NotEqual(oldToken, cookie.Value.Value);
        Assert.True(cookie.HttpOnly);
        AssertCloseTo(DateTimeOffset.UtcNow.AddSeconds(360), cookie.Expires!.Value);

        var refreshed = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.True(refreshed!.ExpiresAt > oldExpiry);
        Assert.Equal("mock-session-id", refreshed.SessionId);

        var session = await GetSessionAsync(cookie.Value.Value!);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }

    [Fact]
    public async Task Refresh_expiry_is_capped_at_maximum_session_age()
    {
        var authTime = DateTimeOffset.UtcNow.AddSeconds(-1790);
        var token = factory.CreateMockToken(DateTimeOffset.UtcNow.AddMinutes(1), authTime: authTime);

        var response = await SendWithCookieAsync(HttpMethod.Post, "/api/auth/refresh-session", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await response.Content.ReadFromJsonAsync<SessionResponse>();
        AssertCloseTo(authTime.AddSeconds(1800), refreshed!.ExpiresAt);
    }

    [Fact]
    public async Task Refresh_after_maximum_session_age_returns_401()
    {
        var token = factory.CreateMockToken(
            DateTimeOffset.UtcNow.AddMinutes(1),
            authTime: DateTimeOffset.UtcNow.AddSeconds(-1801));

        var response = await SendWithCookieAsync(HttpMethod.Post, "/api/auth/refresh-session", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains(HeaderNames.SetCookie));
    }

    [Fact]
    public async Task Refresh_without_cookie_returns_401()
    {
        var response = await _client.PostAsync("/api/auth/refresh-session", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_expires_cookie_with_matching_attributes()
    {
        var token = factory.CreateMockToken(DateTimeOffset.UtcNow.AddMinutes(5));

        var response = await SendWithCookieAsync(HttpMethod.Post, "/api/auth/logout", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = GetSessionCookie(response);
        Assert.True(string.IsNullOrEmpty(cookie.Value.Value));
        Assert.True(cookie.Expires < DateTimeOffset.UtcNow);
        Assert.Equal("/", cookie.Path.Value);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(HeaderSameSiteMode.Lax, cookie.SameSite);
    }

    private async Task<string> StartLoginAsync()
    {
        var response = await _client.GetAsync("/api/auth/sso-login");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return QueryHelpers.ParseQuery(response.Headers.Location!.Query)["state"]!;
    }

    private async Task<string> AuthenticateAsync(string state)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/sso-authenticate",
            new AuthenticateRequest(TestApiFactory.Username, factory.Password, state));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthenticationRedirectResponse>();
        var callback = new Uri(body!.CallbackUrl);
        Assert.Equal($"{TestApiFactory.FrontendBaseUrl}/auth-callback", callback.GetLeftPart(UriPartial.Path));

        var query = QueryHelpers.ParseQuery(callback.Query);
        Assert.Equal(state, query["state"].ToString());
        return query["code"]!;
    }

    private Task<HttpResponseMessage> GetSessionAsync(string? token) =>
        SendWithCookieAsync(HttpMethod.Get, "/api/auth/session", token);

    private Task<HttpResponseMessage> SendWithCookieAsync(HttpMethod method, string url, string? token)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Add(HeaderNames.Cookie, $"{TestApiFactory.CookieName}={token}");
        }

        return _client.SendAsync(request);
    }

    private static SetCookieHeaderValue GetSessionCookie(HttpResponseMessage response)
    {
        var cookies = SetCookieHeaderValue.ParseList(response.Headers.GetValues(HeaderNames.SetCookie).ToList());
        return Assert.Single(cookies, cookie => cookie.Name == TestApiFactory.CookieName);
    }

    private static string TamperSignature(string token)
    {
        var lastDot = token.LastIndexOf('.');
        var signature = token[(lastDot + 1)..].ToCharArray();
        signature[0] = signature[0] == 'A' ? 'B' : 'A';
        return token[..(lastDot + 1)] + new string(signature);
    }

    private static void AssertCloseTo(DateTimeOffset expected, DateTimeOffset actual) =>
        Assert.InRange(actual, expected.AddSeconds(-5), expected.AddSeconds(5));
}
