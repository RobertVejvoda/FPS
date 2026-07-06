# Identity Business

[Identity module](../application-layer/identity) is designed to provide secure and user-friendly authentication mechanisms for accessing the system through the configured OIDC/OAuth provider. It supports the current two-path model: company SSO for tenants that bring an external identity provider, and FairSpot-local accounts hosted in FairSpot-controlled Keycloak for demo, small-tenant, fallback, and break-glass use. Password reset, MFA, passkeys, recovery codes, and credential validation are handled by the enforcing identity provider rather than custom FairSpot application logic. Identity also supports login monitoring and token-claim mapping for tenant, user, and role context.

### Authentication Management
- Manage user identities and access control
- Process login requests through OIDC/OAuth flows
- Delegate password reset workflows to the enforcing identity provider
- Avoid storing passwords, MFA factors, passkeys, or recovery codes in FairSpot application services
- Enforce brute force attack prevention
- Ensure secure data transmission

### User Experience Control
- Provide responsive authentication interfaces
- Handle authentication errors and user feedback
- Manage user sessions and timeouts

### External Authentication Services
- Integrate configured company identity providers for tenant SSO
- Implement secure OAuth/OIDC workflows
- Validate external authentication tokens

### System Security Monitoring
- Track and log authentication activities
- Monitor for security incidents
- Generate security alerts and reports

### Single Sign-On Operations
- Support SSO authentication flows
- Manage SSO token lifecycle
- Implement secure provider integration
- Coordinate cross-system authentication
