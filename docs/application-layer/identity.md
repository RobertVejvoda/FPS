# Identity Application

[Identity](../technology-layer/identity) is responsible for managing user authentication, authorization, and session management across the system. This module ensures secure access control and user identity verification.

## Application Functions

- **User Authentication**  
    Handles secure login through the configured OIDC/OAuth provider. Company SSO, local-account login, MFA, passkeys, and recovery ceremonies are enforced by the provider.

- **Session Management**  
    Controls user sessions, ensuring secure timeouts and preventing unauthorized access.

- **Password Management**  
    Delegates password resets and changes to the configured identity provider. FairSpot application services do not store or reset passwords.

- **Single Sign-On (SSO)**  
    Enables seamless authentication across integrated systems using SSO protocols.

- **Access Control**  
    Implements role-based access control (RBAC) for system resources.

- **Security Monitoring**  
    Tracks login activities and maintains security audit logs.

- **Multi-Factor Authentication**  
    Relies on the enforcing identity provider for MFA/passkey policy and factor challenge. Backend authorization still comes only from validated token claims.

- **Identity Brokering**
    Supports configured company identity providers through OIDC/OAuth federation where a tenant enables company SSO.
