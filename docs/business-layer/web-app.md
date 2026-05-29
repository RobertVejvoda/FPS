# Web App Business

## User Interface
- **Responsive Design**: Adapts to mobile and desktop devices
- **Intuitive Navigation**: Easy-to-use interface with clear paths
- **Dark Mode**: Optional dark theme for better visibility
- **Search**: Global search functionality
- **Dashboard Customization**: Personalized widget arrangement
- **Accessibility**: WCAG-compliant features

## Internationalization
- **Multi-language Support**: Multiple UI languages
- **Language Preferences**: Persistent language settings

## User Profile
- **Profile Picture**: Upload and manage avatar
- **Personal Info**: Manage user details and vehicles
- **Account Management**:
    - Download personal data
    - Delete account

## System Features
- **Parking Management**: View available parking slots
- **Request Tracking**: Monitor request status
- **My Spots**: Employee default page for today/tomorrow allocations, quick requests, request history, and allocation explanations. See [My Spots Employee UX](./my-spots-ux).

## Role-Specific Workspaces

FairSpot must not show every role the same operational page. The web shell should route users to the workspace that matches their current responsibility:

- employees start from **My Spots**, focused on their own requests, outcomes, next action, and notifications;
- HR/facility managers start from an **Operations** or **HR Operations** workspace, focused on tenant/location request queues, Draw status, exceptions, cancellations, and employee support;
- tenant administrators start from setup/configuration readiness rather than employee booking workflows;
- auditors start from read-only evidence and audit timelines.

The HR operations workspace must show information that employees should not see, while still avoiding hidden lottery internals unless the role is explicitly authorized. Minimum HR needs:

- request queue for the tenant or assigned locations, with employee-safe identity/reference, date, time slot, status, and support reason;
- clear next scheduled Draw time for the selected location/date/time slot, including whether the Draw has not started, is running, completed, failed, or needs manual intervention;
- a privileged **Run Draw now** action for authorized HR/admin users, requiring location, date, time slot, and reason;
- cancellation of any pending or allocated request within HR scope, requiring a reason and notifying the affected employee;
- links from queue items to audit/evidence details when the actor is authorized.

## Security
- **Login History**: View session history
- **Session Management**: Log out from all devices

## WEB009 Boundary

WEB009 replaces the development bearer-token handoff in the React web app with a real browser login experience. The app should authenticate through the configured OIDC provider using Authorization Code + PKCE, obtain an access token for FairSpot APIs, call `GET /me`, and enter the existing authenticated web shell only when the session is valid.

WEB009 must preserve the existing security boundary: the web app never supplies tenant ID, requestor ID, user ID, or roles for API scoping. Backend services continue to resolve tenant, user, and roles from authenticated token claims.

WEB009 includes:

- login start, callback handling, authenticated session restoration, and logout;
- session validation through `GET /me`;
- runtime configuration for API base URL, issuer/discovery endpoint, client ID, scopes, redirect URI, and post-logout redirect URI;
- clear states for unauthenticated, login cancelled, login failed, invalid/expired token, and unreachable backend;
- a development-only manual token fallback only when explicitly enabled for local smoke testing.

WEB009 does not implement identity-provider provisioning, tenant onboarding, MFA policy design, role administration, booking behavior changes, backend business behavior changes, or mobile login.

## Notifications
- **Email Settings**: Configure email notifications
- **Notification Center**: View and manage notifications
