# Customer Business

[Customer module](../application-layer/customer) is designed to manage customer and tenant setup within FairSpot. It encompasses customer entities, tenant lifecycle, user roles and permissions, data privacy and security, and support-facing information. Billing is deferred until the commercial model is approved.

Customer employee onboarding and internal company-system integration are planned separately in [Tenant Onboarding](./tenant-onboarding) and [SSO-First Customer Integration](./customer-data-import). Tenant Onboarding defines how a new company is created, configured, checked, and made ready for employees. SSO-First Customer Integration defines the identity/profile data contract for SSO, local fallback accounts, imports, data classification, validation rules, and downstream implementation slices.

| User Story | Title |
|------------|-------|
| [US501](#us501-define-customer) | Define Customer |
| [US502](#us502-commercial-model-validation) | Commercial Model Validation |
| [US503](#us503-user-management-features) | User Management Features |
| [US504](#us504-custom-reporting) | Custom Reporting |
| [US505](#us505-commercial-account-readiness) | Commercial Account Readiness |
| [US506](#us506-data-privacy-and-security) | Data Privacy and Security |
| [US507](#us507-customer-support) | Customer Support |
| [US508](#us508-integration-with-third-party-services) | Integration with Third-Party Services |
| [US509](#us509-tenant-commercial-settings) | Tenant Commercial Settings |
| [US510](#us510-support-or-service-subscription) | Support Or Service Subscription |

### US501: Define Customer
**Description**: As a system architect, I want to define a customer as a company using FairSpot services, ensuring data isolation for privacy and compliance.
**Acceptance Criteria**:
- Customer data is isolated.
- Tailored services and personalized experiences are provided.
**Priority**: High

### US502: Commercial Model Validation
**Description**: As a product manager, I want commercial options to be validated before pricing or product Billing is implemented.
**Acceptance Criteria**:
- Commercial options are documented as planning candidates, not public promises.
- Product pricing is not published until approved.
- Billing implementation is gated by an approved support, implementation, hosted-demo, dual-license, or subscription offer.
**Priority**: High

### US503: User Management Features
**Description**: As a customer, I want comprehensive user management features to manage users within my organization by assigning roles and permissions.
**Acceptance Criteria**:
- Role-based access control is implemented.
- Users can be assigned specific roles and permissions.
- Audit logs are maintained for user activities.
**Priority**: Medium

### US504: Custom Reporting
**Description**: As a customer, I want to create, save, and modify custom reports in various formats and schedule automatic generation.
**Acceptance Criteria**:
- Custom reports can be created, saved, and modified.
- Reports can be generated in PDF and Excel formats.
- Reports can be scheduled for automatic generation.
**Priority**: Medium

### US505: Commercial Account Readiness
**Description**: As a commercial owner, I want future customer commercial records to be tenant-scoped, auditable, and separate from employee booking data.
**Acceptance Criteria**:
- Commercial account records are tenant-scoped.
- Employee booking data is not used commercially unless explicitly approved.
- External invoice references are preferred before in-product financial collection.
**Priority**: High

### US506: Data Privacy and Security
**Description**: As a customer, I want data encryption for all stored and transmitted data, regular security audits, and timely notifications of data breaches.
**Acceptance Criteria**:
- Data encryption is applied.
- Regular security audits are conducted.
- Customers are notified of data breaches promptly.
**Priority**: High

### US507: Customer Support
**Description**: As a customer, I want access to a dedicated support portal with multiple support channels and tracked response and resolution times.
**Acceptance Criteria**:
- Support portal is available.
- Support is accessible via email, phone, and chat.
- Response and resolution times are tracked.
**Priority**: Medium

### US508: Integration with Third-Party Services
**Description**: As a customer, I want APIs for integration with third-party services, with provided documentation and support.
**Acceptance Criteria**:
- APIs are available for third-party integration.
- Documentation and support for API usage are provided.
- Integration settings can be managed through the customer interface.
**Priority**: Medium

### US509: Tenant Commercial Settings
**Description**: As a tenant administrator, I want future commercial settings to be limited to contract and support information, not financial collection details.
**Acceptance Criteria**:
- Commercial settings are tenant-scoped and role-protected.
- Financial collection details are not stored in FPS unless later approved.
**Priority**: High

### US510: Support Or Service Subscription
**Description**: As a customer sponsor, I want support or service subscription information to be clear if FPS is adopted commercially.
**Acceptance Criteria**:
- Support or service subscription terms can be represented after approval.
- Core fairness, audit, privacy, and tenant operation features are not made unusable without a paid unlock.
**Priority**: High
