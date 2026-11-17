# Troubleshooting

## API exits during startup

The API validates configuration at startup. Use the approved local secret-management route to provision every required `Jwt:*`, `DemoIdp:*`, and `Frontend:BaseUrl` setting. The signing key must be Base64 encoded and decode to at least 32 random bytes. Do not put credentials or signing keys in committed JSON settings. This project intentionally has no usable credentials in source control.

## Angular shows API unavailable

- Confirm the API is listening at `http://localhost:5208`.
- Confirm `GET /api/health` returns `200`.
- Confirm the UI is served at `http://localhost:4200` and `Frontend:BaseUrl` uses that origin.
- Keep API calls relative (`/api/...`) so the configured Angular proxy is used.
- Review the browser console for method, endpoint, and status logs. The interceptor does not log request bodies, passwords, codes, cookies, or JWTs.

## Callback says code or state is invalid

Restart the sign-in flow. The API state expires after five minutes, and authorization codes expire after two minutes and can be consumed only once. The code and state must come from the same callback transaction.

## Session returns `401`

The cookie may be missing, expired, malformed, or signed for a different issuer/audience/key. Confirm the browser is using the local proxy and that JWT settings remain stable between API restarts. Re-authenticate after an expired session.

## Cookie is not sent on local HTTP

Check that the API is running in Development and that the request is through the same-origin proxy at `localhost:4200`. Inspect the cookie attributes in browser developer tools. The local Development configuration permits HTTP; deployed environments must use HTTPS and `Secure=true`.

## Frontend dependency installation or build

The project scaffold was generated with `--skip-install`. From `frontend/sso-session-ui`, install dependencies only when approved, then use `npm run build` and `npm test -- --watch=false`. The Angular CLI is globally installed, but project dependencies are not yet installed.