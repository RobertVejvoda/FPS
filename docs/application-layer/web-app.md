# Web Application

The web application frontend consists of multiple modules providing a comprehensive user interface for FairSpot.

## Application Functions

- **[Authentication Module](./identity)**
    Starts OIDC login, handles redirect/session validation, and presents provider-owned MFA/passkey ceremonies when the identity provider requires them.

- **[Booking Management](./booking)**
    Provides interface for viewing, creating, and managing parking slot bookings via booking-ui.

- **[Profile Administration](./profile)**
    Enables users to manage personal information and preferences through profile-ui.

- **[Configuration Interface](./configuration)**
    Offers system configuration capabilities for administrators using configuration-ui.

- **[Reporting Dashboard](./reporting)**
    Delivers comprehensive reporting and analytics functionality via reporting-ui.

- **[Customer Management](./customer)**
    Facilitates customer data management and operations through customer-ui.

- **[Notification Center](./notification)**
    Manages system notifications and alerts using notification-ui.

- **[Audit Interface](./audit)**
    Provides access to system audit logs and monitoring via audit-ui.

- **[Billing Portal](./billing)**
    Deferred commercial/account surface only if a future approved business model requires it; no payment processing is part of the current baseline.

- **[Feedback System](./feedback)**
    Deferred authenticated feedback surface for evaluation/support signals; broad support-desk workflow is not part of the current baseline.

## WEB009 Web Real Login

WEB009 replaces the current development-only session page with a real browser login path while keeping the existing web routes and generated API contract.

Application responsibilities:

- Start OIDC Authorization Code + PKCE login from the unauthenticated state.
- Complete the redirect callback and restore a valid session when the app starts.
- Validate the session by calling `GET /me` before rendering authenticated routes.
- Attach the resulting access token through the existing web API access layer.
- Clear local session state on logout and return to the unauthenticated/login state.
- Keep the manual bearer-token handoff available only as an explicit development fallback for local smoke testing.
- Handle login cancelled, login failed, invalid configuration, expired/invalid token, and unreachable backend states without crashing.

Application constraints:

- Do not send tenant ID, requestor ID, user ID, or role data from the web app for API scoping.
- Do not hardcode secrets, tokens, tenant IDs, user IDs, or developer-machine URLs.
- Do not introduce backend behavior changes, role-management workflows, or tenant onboarding behavior in WEB009.
