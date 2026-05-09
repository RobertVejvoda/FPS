---
title: Access Control
---

Access control within the system is managed through a combination of role-based access control (RBAC) and attribute-based access control (ABAC). This ensures that users have the minimum necessary permissions to perform their tasks, adhering to the principle of least privilege. Multi-factor authentication (MFA) is implemented to add an extra layer of security, requiring users to verify their identity through multiple methods. Regular access reviews are conducted to ensure that permissions are up-to-date and appropriate for each user's role. Additionally, access control policies are enforced consistently across all environments, from development to production, to maintain a secure and compliant system.

## Azure Components for Access Control

Azure provides several components to manage access control effectively:

- **[Azure Active Directory (Azure AD)](https://azure.microsoft.com/en-us/services/active-directory/)**: Centralized identity and access management service that provides single sign-on, multi-factor authentication, and conditional access to protect users from cybersecurity threats.
- **[Azure Role-Based Access Control (RBAC)](https://docs.microsoft.com/en-us/azure/role-based-access-control/)**: Allows you to manage access to Azure resources by assigning roles to users, groups, and applications. It provides fine-grained access management for Azure resources.
- **[Azure Policy](https://docs.microsoft.com/en-us/azure/governance/policy/overview)**: Enables you to create, assign, and manage policies that enforce different rules and effects over your resources, ensuring those resources stay compliant with your corporate standards and service level agreements.
- **[Azure Key Vault](https://azure.microsoft.com/en-us/services/key-vault/)**: Helps safeguard cryptographic keys and secrets used by cloud applications and services. It ensures that sensitive information is securely stored and access is tightly controlled.
- **[Azure Security Center](https://azure.microsoft.com/en-us/services/security-center/)**: Provides unified security management and advanced threat protection across hybrid cloud workloads. It helps in assessing the security state of your resources, providing recommendations, and enabling you to apply security policies.

These components work together to ensure robust access control and security across your Azure environment.
