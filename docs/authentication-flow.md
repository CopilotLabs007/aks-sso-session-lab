# Authentication Flow

## Sequence

1. The user selects **Continue to sign in**. Angular navigates the browser to `GET /api/auth/sso-login`.
2. The API creates cryptographically random, short-lived state, stores it in memory, and redirects to `/simulated-idp-login?state=...`.
3. The user enters the local demo username and password. Angular sends them with state to `POST /api/auth/sso-authenticate`.
4. The simulated IdP validates the credentials and pending state. It issues a random, short-lived, single-use authorization code bound to that user and state.
5. Angular navigates to `/auth-callback?code=...&state=...`. The callback displays the temporary code and sends code/state to `POST /api/auth/create-session`.
6. The API validates the code, state, expiry, and one-time use, then creates a signed JWT and places it only in an HttpOnly cookie.
7. Angular opens the dashboard and requests `GET /api/auth/session`. The browser attaches the cookie; ASP.NET Core validates the token and returns non-secret session details.
8. The dashboard can call `POST /api/auth/refresh-session` while the session is valid. The API issues a new JWT with the same session identity and an expiry bounded by the maximum session age.
9. `POST /api/auth/logout` expires the cookie. Angular clears only its in-memory session display state.

## Terms

- **Identity Provider authentication:** the simulator checks the configured demo account. It does not authenticate a real-world identity.
- **Authorization code:** a short-lived, one-time handoff value. It is not the application JWT.
- **State:** a random value associated with a pending sign-in and checked against the authorization code to reject mismatched transactions.
- **Callback:** the Angular route that receives code/state and asks the API to create the app session.
- **Application session:** the API-authenticated period represented by the signed JWT in this lab.
- **JWT:** a signed claims document. Signing protects integrity; it does not encrypt claims.
- **Authentication cookie:** browser-managed storage and transport for the JWT. `HttpOnly` prevents JavaScript from reading it.

## Security boundary and limitations

This local simulator is not OAuth 2.0 or OpenID Connect. It lacks real provider discovery, client registration, key rotation, distributed transaction storage, production abuse controls, and a production refresh-token lifecycle. Its state and authorization-code store is process-local and is lost on restart. Never reuse demo settings in a deployed system.

The callback temporarily displays the authorization code because this is an instructional simulator. The JWT is never included in an Angular response model, displayed in the UI, or stored in local/session storage.