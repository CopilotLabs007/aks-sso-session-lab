using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace SsoSession.Api.Authentication;

public sealed class AuthFlowStore
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AuthorizationCodeLifetime = TimeSpan.FromMinutes(2);
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _pendingStates = [];
    private readonly Dictionary<string, AuthorizationCodeRecord> _authorizationCodes = [];

    public string CreateState()
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            RemoveExpiredEntries(now);

            string state;
            do
            {
                state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            }
            while (_pendingStates.ContainsKey(state));

            _pendingStates.Add(state, now.Add(StateLifetime));
            return state;
        }
    }

    public bool TryIssueAuthorizationCode(string state, string username, out string code)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            RemoveExpiredEntries(now);

            if (!_pendingStates.Remove(state))
            {
                code = string.Empty;
                return false;
            }

            code = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            _authorizationCodes.Add(
                code,
                new AuthorizationCodeRecord(username, state, now.Add(AuthorizationCodeLifetime)));
            return true;
        }
    }

    public bool TryConsumeAuthorizationCode(string code, string state, out string username)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            RemoveExpiredEntries(now);

            if (!_authorizationCodes.TryGetValue(code, out var record)
                || !string.Equals(record.State, state, StringComparison.Ordinal))
            {
                username = string.Empty;
                return false;
            }

            _authorizationCodes.Remove(code);
            username = record.Username;
            return true;
        }
    }

    private void RemoveExpiredEntries(DateTimeOffset now)
    {
        foreach (var state in _pendingStates
                     .Where(entry => entry.Value <= now)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            _pendingStates.Remove(state);
        }

        foreach (var code in _authorizationCodes
                     .Where(entry => entry.Value.ExpiresAt <= now)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            _authorizationCodes.Remove(code);
        }
    }

    private sealed record AuthorizationCodeRecord(string Username, string State, DateTimeOffset ExpiresAt);
}
