---
title: Software Architecture
---

![Logical Architecture](../images/fps-logical-architecture.png)

**[Domain map](./domain-map)**

## Application Components

| App. Component | Name | Technology |
|------------------- | ---- | ------- |
| [Web App](./web-app) | Web App | React |
| [Mobile App](./mobile-app) | Mobile App | React Native + Expo |
| [Identity](./identity) | Authentication & Authorization | .NET 10 Web API |
| [Audit](./audit) | Audit Service | .NET 10 Web API |
| [Billing](./billing) | Billing Service | .NET 10 Web API |
| [Booking](./booking) | Booking Service | .NET 10 Web API |
| [Configuration](./configuration) | Configuration Service | .NET 10 Web API |
| [Customer](./customer) | Customer Service | .NET 10 Web API |
| [Notification](./notification) | Notification Service | .NET 10 Web API |
| [Profile](./profile) | Profile Service | .NET 10 Web API |
| [Reporting](./reporting) | Reporting Service | .NET 10 Web API |
| [Feedback](./feedback) | Feedback Service | .NET 10 Web API |

## Other Components

| App. Component | Software Component | Name | Technology |
|------------------- | ------------------- | ---- | ------- |
| Authentication | Keycloak | Identity and Access Management | Java |
| Traces and metrics | Prometheus | Monitoring and Alerting | Go |
| Monitoring | Grafana | Analytics and Monitoring | Various |
| Logging | Loki | Log Aggregation | Go |
| Tracing | Jaeger | Distributed Tracing | Go |
| Write store (CQRS) | Dapr State Store → MongoDB | Aggregate persistence per tenant database | Various |
| Read store (CQRS) | MongoDB driver | Query/projection read models per tenant database | Various |
| Event Bus | RabbitMQ via Dapr pub/sub | Message Broker | Various |
| Cache | Redis | In-Memory Data Store | Various |
| API Gateway | Traefik | Edge Router | Go |
| File Storage | MinIO | Object Storage | Go |
| Secret Management | Vault | Secret Management | N/A |

> **Multi-tenancy**: each tenant is isolated in its own MongoDB database (`fps_{tenant_id}`). Services resolve the tenant database from the request context before any read or write operation.

### Dapr

| Software Component | Name | Type | Purpose | Packaging Type |
|------------------- | ---- | ---- | ------- | -------------- |
| Dapr Operator | dapr-operator | Service | Manages component updates and Kubernetes services endpoints for Dapr (state stores, pub/subs, etc.) | Docker container |
| Dapr Sidecar Injector | dapr-sidecar-injector | Service | Injects Dapr sidecar into application pods | Docker container |
| Dapr Sentry | dapr-sentry | Service | Manages mTLS certificates for Dapr services | Docker container |
| Dapr Placement | dapr-placement | Service | Manages actor placement for Dapr actors | Docker container |
| Dapr Dashboard | dapr-dashboard | UI | Web UI for managing and monitoring Dapr applications | Docker container |

### Licensing

### Tool/Framework Versions

This section provides a list of tools and frameworks used in the project, along with their versions, preferred editors, programming languages, distribution formats, and licenses.

| Tool/Framework | Version | Editor | Language | Distribution Format | License | Purpose |
| ---------------| ------- | ------ | -------- | ------------------- | ------- | ------- |
| React          | 17.0.2  | VSCode | JavaScript | npm package         | MIT     | Frontend library for building user interfaces |
| React Native  | 0.74+   | VSCode | TypeScript | npm package         | MIT     | Cross-platform mobile app framework |
| Expo          | 51+     | VSCode | TypeScript | npm package         | MIT     | Managed React Native workflow — no native build tooling required |
| .NET 10        | 10.0    | VSCode | C#        | NuGet package       | MIT     | Framework for building various types of applications |
| Java           | 11      | IntelliJ| Java     | JAR file            | GPL     | General-purpose programming language |
| Docker         | 20.10.7 | VSCode | N/A      | Docker image        | Apache 2.0 | Platform for developing, shipping, and running applications in containers |
| Helm           | 3.5.4   | VSCode | N/A      | Helm chart          | Apache 2.0 | Package manager for Kubernetes applications |
| Dapr           | 1.14+   | VSCode | Various  | Docker image        | Apache 2.0  | Runtime for building distributed applications (Workflows require 1.10+) |
| Kubernetes     | 1.21.0  | VSCode | YAML     | Helm chart          | Apache 2.0 | Container orchestration platform |
| Terraform      | 1.0.0   | VSCode | HCL      | Binary              | MPL 2.0 | Infrastructure as code tool |
| Ansible        | 2.9.0   | VSCode | YAML     | Package             | GPL 3.0 | Automation tool for IT tasks |
| Git            | 2.31.1  | VSCode | N/A      | Binary              | GPL 2.0 | Version control system |




