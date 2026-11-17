using System.ComponentModel.DataAnnotations;

namespace SsoSession.Api.Contracts;

public sealed record AuthenticateRequest(
    [param: Required] string Username,
    [param: Required] string Password,
    [param: Required] string State);

public sealed record CreateSessionRequest(
    [param: Required] string Code,
    [param: Required] string State);

public sealed record AuthenticationRedirectResponse(string CallbackUrl);

public sealed record ErrorResponse(string Message);

public sealed record SessionResponse(
    string Username,
    string SessionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    long RemainingSeconds,
    string Status);
