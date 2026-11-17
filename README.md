# SSO Session Management Lab

A local learning simulator for SSO-style handoff, JWT validation, HttpOnly cookie behavior, and session renewal. This is not a real OAuth 2.0 or OpenID Connect provider and must not be deployed as an authentication system.

## Prerequisites

- .NET SDK 10.x
- Node.js 24.x and npm 11.x
- Angular CLI 21.x

The frontend was scaffolded with Angular CLI 21.2.30. It uses the Angular development proxy to forward `/api` requests to `http://localhost:5208`, keeping browser requests same-origin.

## Local configuration

Do not commit credentials or signing keys. Non-secret settings have code defaults (issuer, audience, cookie name `sso_session`, 6-minute JWT lifetime, 30-minute maximum session age, frontend URL `http://localhost:4200`) and can be overridden through configuration.

Set the simulated IdP demo account with User Secrets (stored outside the repository):

```powershell
dotnet user-secrets set "DemoIdp:Username" "<your-test-user>" --project backend\SsoSession.Api
dotnet user-secrets set "DemoIdp:Password" "<your-test-password>" --project backend\SsoSession.Api
```

Until these are set, the API starts but `POST /api/auth/sso-authenticate` returns `503`.

`Jwt:SigningKey` (Base64, at least 32 random bytes) is optional in Development: when absent, the API generates a random in-memory key at startup, so restarting the API invalidates existing sessions. It is required in every other environment.

## Run locally

### Option A: Angular dev server (http://localhost:4200)

```powershell
dotnet run --project backend\SsoSession.Api --launch-profile http
cd frontend\sso-session-ui; npm install; npm start
```

The Angular development proxy ([proxy.conf.json](frontend/sso-session-ui/proxy.conf.json)) sends `/api` traffic to `http://localhost:5208`.

### Option B: Nginx reverse proxy (http://localhost:8080)

Requires Nginx for Windows (`winget install --id nginxinc.nginx --exact`).

```powershell
cd frontend\sso-session-ui; npx ng build; cd ..\..
dotnet run --project backend\SsoSession.Api --launch-profile nginx
.\nginx\nginx.ps1            # -Action start | stop | reload | test
```

Nginx serves the production build and proxies `/api/` to the API. The `nginx` launch profile points the API's IdP redirects at port 8080. See [nginx/conf/nginx.conf](nginx/conf/nginx.conf) and [docs/architecture.md](docs/architecture.md).

## Build and test

```powershell
dotnet test backend\SsoSession.Api.Tests
cd frontend\sso-session-ui; npx ng test --watch=false
```

Backend tests host the API in memory with per-run random credentials and signing key and use mock JWTs; they never read local User Secrets. Frontend tests use Angular's `HttpTestingController` with mock responses.

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/health` | API health check |
| GET | `/api/auth/sso-login` | Start simulated sign-in and create state |
| POST | `/api/auth/sso-authenticate` | Check demo credentials and issue a one-time code |
| POST | `/api/auth/create-session` | Exchange code/state for an application session cookie |
| GET | `/api/auth/session` | Validate cookie JWT and return session details |
| POST | `/api/auth/refresh-session` | Replace a valid JWT cookie with a renewed JWT |
| POST | `/api/auth/logout` | Clear the authentication cookie |

See [docs/architecture.md](docs/architecture.md), [docs/authentication-flow.md](docs/authentication-flow.md), [docs/cookie-lifecycle.md](docs/cookie-lifecycle.md), and [docs/troubleshooting.md](docs/troubleshooting.md).

## Manual acceptance scenarios

Run these after the local configuration is provisioned and both applications are running:

1. **Successful login:** enter the authorized local demo credentials; expect the callback to exchange code/state and the dashboard to show the authenticated username.
2. **Session validation:** reload the dashboard; expect `GET /api/auth/session` to succeed using the browser cookie.
3. **HttpOnly cookie:** inspect the browser's Application/Storage cookie panel; verify `HttpOnly`, `SameSite=Lax`, the local `Secure` setting, and the absence of a readable cookie in JavaScript.
4. **Expiration:** allow the six-minute JWT to expire, then request the session; expect HTTP `401` and a return to login.
5. **Logout:** sign out; expect the cookie to be cleared and subsequent session requests to return `401`.
6. **Invalid JWT:** send a malformed token under the configured cookie name; expect the API to reject it with HTTP `401`.
7. **Refresh:** refresh an active session; expect a replacement cookie with a newly signed JWT and updated expiration, bounded by the maximum session age.

Scenarios 1, 2, 5 and 7 have been exercised manually in the browser. The automated backend tests cover the server side of all seven scenarios with mock values; the JavaScript-inaccessibility check in scenario 3 still needs a manual browser check.