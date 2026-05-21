# Microservice Security Patterns

Implements security measures specific to microservices architecture. This includes an ingress/API gateway for a single client entry point, service-to-service identity through Dapr or trusted internal transport, and distributed tracing for monitoring and logging across microservices. The gateway product is deployment-profile specific.

### Tenant Isolation

- **Tenant-scoped data access**: Services derive tenant scope from authenticated or trusted service context.
- **Strict data segregation**: Data from different tenants is segregated by tenant-safe keys, tenant-specific collections, or an approved equivalent partitioning strategy.
- **Provider-neutral encryption**: Confidential data is encrypted in transit and at rest using the selected deployment profile's storage and secret-management controls.
- **Resource isolation by profile**: Local/demo/client environments may choose stronger physical isolation where required, but the core application must enforce tenant isolation even when infrastructure resources are shared.

### JWT Token Management

- **Secure token generation and validation**: JWT tokens are securely generated and validated to ensure that only authenticated users can access the services.
- **Claims-based authorization**: Access to resources is controlled based on the claims present in the JWT token, allowing for fine-grained access control.
- **Token lifetime management**: Tokens have a defined lifetime and are regularly refreshed to minimize the risk of token misuse.
- **Signature verification**: JWT tokens are signed and their signatures are verified to ensure their integrity and authenticity.
- **Role-based access control (RBAC)**: Access to resources is managed based on user roles defined in the JWT token, ensuring that users can only access resources they are authorized to.


- **API Gateway / ingress**: Acts as a single entry point for client requests. It handles routing, TLS termination, and optional rate limiting; backend services still validate JWTs and required tenant/user/role claims.


todo: sequence diagram
1. retrieve access token
2. access gateway with token
3. validate certificate

alternate:
1. access token expired, refresh access token with refresh token
