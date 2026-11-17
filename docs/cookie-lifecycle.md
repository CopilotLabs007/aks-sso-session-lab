# Cookie Lifecycle

## Issuance

After the API consumes a valid authorization code, it creates a signed JWT with user, session, issue-time, and expiration claims. `CreateSession` returns the JWT as a cookie and returns only user/session metadata in the JSON body.

The local cookie settings are:

| Attribute | Local behavior | Reason |
|---|---|---|
| `HttpOnly` | `true` | Prevents JavaScript from reading the JWT. |
| `Secure` | `false` only for local HTTP | Localhost HTTP needs this; use HTTPS and `Secure=true` outside this lab. |
| `SameSite` | `Lax` | Reduces cross-site cookie sending for this local navigation flow; it is not complete CSRF protection. |
| `Path` | `/` | Sends the cookie to API routes under the application host. |
| `Domain` | omitted | Keeps the cookie host-only; do not broaden it unnecessarily. |
| `Expires` | Matches JWT expiration | Lets the browser stop sending the cookie after the token's intended lifetime. |

The Angular app never reads the cookie. `HttpClient` sends requests with credentials enabled, and the same-origin development proxy forwards the cookie to the API.

## Expiration and validation

The initial JWT lifetime is six minutes. The dashboard countdown is a UI estimate based on the server-provided expiration time; it does not extend or validate the token.

- **JWT expiration:** a claim validated by the API on every protected request.
- **Cookie expiration:** instructs the browser when to stop storing/sending the cookie.
- **Browser storage:** the cookie jar holds and automatically sends matching cookies; `HttpOnly` blocks JavaScript access.
- **Backend validation:** signature, issuer, audience, and lifetime are checked by the JWT bearer handler. A cookie being present does not make an invalid JWT valid.

If the API returns `401` from the session or refresh endpoint, Angular clears its in-memory session state and returns to login.

## Refresh and logout

Refresh creates and signs a new JWT and replaces the cookie. Re-sending a cookie alone would not extend the JWT's signed expiration. This demo preserves the session ID and original creation time and caps the new expiration at the configured maximum session age. It does not provide production refresh-token rotation, revocation lists, reuse detection, or device/session management.

Logout deletes the cookie with the same name and path and the matching relevant attributes. The API does not keep a token revocation list, so an already copied token could remain valid until expiry; the UI and cookie are cleared for this lab's normal browser flow.