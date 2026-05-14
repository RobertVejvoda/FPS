## Confidentiality Overview

Confidentiality within the Fair Parking System (FPS) is crucial to ensure that sensitive information is protected from unauthorized access. Each application component and role has specific confidentiality requirements to maintain the integrity and privacy of the data.

### Application Components Confidentiality

| Component                    | Confidentiality Level |
|------------------------------|-----------------------|
| Web Application              | Confidential          |
| Mobile Application           | Confidential          |
| Backend APIs (microservices) | Internal              |
| Databases                    | Confidential          |
| Authentication Service       | Confidential          |
| Notification                 | Internal              |
| Logging, metrics             | Internal              |
| Payment Gateway              | Confidential          |
| Third-Party Integrations     | Internal              |

### Microsoft Applications Confidentiality

| Application                  | Confidentiality Level |
|------------------------------|-----------------------|
| Microsoft Entra ID           | Confidential          |
| Azure API Management         | Internal              |
| Azure Communication Services | Internal              |
| Azure Monitor                | Internal              |
| Azure SignalR                | Internal              |
| Azure Event Grid             | Internal              |
| Azure Service Bus            | Internal              |
| Azure Key Vault              | Confidential          |
| Azure Redis Cache            | Internal              |
| Azure Blob Storage           | Internal              |
| Azure Cosmos DB              | Internal              |
| Azure Container Registry     | Internal              |

