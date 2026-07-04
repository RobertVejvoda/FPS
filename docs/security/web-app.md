# Web App Security

Web app security protects browser users, API calls, session material, and rendered tenant data. Controls are provider-neutral and apply whether the app runs locally, through NAS/Cloudflare, DigitalOcean, or a client-owned platform.

## Input and Output Safety

- Validate user input on both client and server.
- Escape or encode data before rendering.
- Use safe component patterns and avoid raw HTML rendering unless sanitized.
- Keep employee-visible screens free of hidden allocation internals and other employees' data.

## Session Security

- Use OIDC Authorization Code + PKCE.
- Store session material only in secure browser storage/cookie patterns approved for the profile.
- Clear session state on logout.
- Validate the session with `GET /me` before rendering authenticated routes.

## Browser Protections

- Use HTTPS for every hosted public endpoint.
- Configure secure cookie attributes where cookies are used.
- Use SameSite protections and anti-CSRF measures for cookie-backed flows.
- Apply a Content Security Policy where feasible.
- Avoid exposing tokens in URLs, logs, or browser-visible error details.

## Monitoring and Review

- Log security-relevant failures without tokens or PII.
- Monitor unusual authentication failures and authorization denials.
- Run build/typecheck and relevant smoke checks for UI changes.
- Review auth, tenant isolation, and employee-visible data exposure before merge.
