- [**Non-functional Requirements**](./technology-layer/non-functional-requirements): Addresses performance, reliability, usability, and other non-functional aspects.
- [**Software Architecture**](./technology-layer/software-architecture): Defines the structure and organization of the software components.
- [**Packaging**](./technology-layer/packaging): Manages the distribution and deployment of the application.

Security is a cross-cutting top-level section, not a child of the technology layer. See [Security](./security) for the FPS security model, controls, privacy requirements, and service-specific security notes.

Production operation is also a top-level architecture section. See [Production](./production) for the hosted runtime model, deployment path, backups, restore, monitoring, incidents, and readiness gates.

### Technologies Used

- **.NET 10**: Core framework for backend services.
- **React**: Frontend library for web user interfaces.
- **React Native + Expo**: Mobile platform.
- **Docker**: Containerization platform for consistent deployment.
- **Kubernetes**: Orchestration platform for managing containerized applications.
- **Dapr 1.14+**: Runtime for state, pub/sub, service invocation, sidecars, and future workflows.
- **RabbitMQ via Dapr pub/sub**: Event bus for Booking events consumed by Notification, Audit, and future read models.
- **MongoDB**: Dapr-backed write store and MongoDB-driver read store, isolated collection-per-tenant inside service-owned databases.
- **Keycloak**: OIDC/OAuth 2.0 identity provider.


### Domain Map

![Domain Map](./images/fps-software-architecture-detailed.png)



