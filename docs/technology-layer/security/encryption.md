---
title: Encryption
---

Encryption is a critical component of security, ensuring that data is protected both at rest and in transit. In the context of Azure and Dapr, encryption applies in several ways:

- **Data at Rest**: Azure provides encryption for data at rest using Azure Storage Service Encryption (SSE). This includes encryption for Azure Blob Storage. The encryption uses 256-bit AES encryption, one of the strongest block ciphers available. Additionally, Azure Key Vault manages and controls access to the encryption keys.

- **Data in Transit**: Data in transit is encrypted using Transport Layer Security (TLS). Azure enforces TLS 1.2 for all communications to ensure data integrity and privacy. For microservices communication, Dapr leverages mTLS to provide secure service-to-service communication. Dapr automatically handles the certificate management and rotation, simplifying the implementation of secure communications. 

- **Dapr Sidecar Encryption**: Dapr uses sidecars to provide additional security features, including encryption. The sidecar encrypts messages before they are sent over the network and decrypts them upon receipt. This ensures that even if the network is compromised, the data remains secure.

- **Key Management**: Azure Key Vault is a cloud service that provides secure storage and management of cryptographic keys, secrets, and certificates. It integrates with Azure services and Dapr to provide a centralized key management solution. Dapr uses Azure Key Vault to securely store and retrieve secrets, ensuring that sensitive information is protected.

- **End-to-End Encryption**: By combining Azure's encryption capabilities with Dapr's secure communication features, you achieve end-to-end encryption for your applications. This means that data is encrypted at the source, remains encrypted while in transit, and is only decrypted at the destination.

- **Compliance and Best Practices**: Azure complies with a wide range of industry standards and regulations, including GDPR, HIPAA, and ISO/IEC 27001. By using Azure's encryption services and Dapr's secure communication features, you ensure that your applications meet these compliance requirements and follow best practices for security.