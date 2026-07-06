# Application Layer

![Application Architecture](./images/fairspot-application-arch-1.png)

Validation note: [Application Architecture 1 Validation](./application-arch-1-validation.md)

## Application Components

| App. Component | Description | Confidentiality | Integrity | Availability | Traceability |
| --------------------- | ----------- | --------------- | --------- | ------------ | ------------ |
| [Web App](./application-layer/web-app) | User interface for web users | Internal | Standard | High | Simple |
| [Mobile App](./application-layer/mobile-app) | User interface for mobile users | Internal | Standard | High | Simple |
| [Identity](./application-layer/identity) | Authentication features | Internal | High | High | Simple |
| [Audit](./application-layer/audit) | Manage and retrieve audit logs | Internal | High | High | Detailed |
| [Billing](./application-layer/billing) | Deferred commercial account capability | Confidential | High | Standard | Simple |
| [Booking](./application-layer/booking) | Manage booking requests and allocations | Internal | High | High | Simple |
| [Configuration](./application-layer/configuration) | Manage configuration options | Internal | Standard | High | Simple |
| [Customer](./application-layer/customer) | Manage customer and tenant information | Confidential | High | High | Simple |
| [DataHub](./application-layer/datahub) | Own cross-service read models and projection storage | Confidential | High | High | Detailed |
| [Feedback](./application-layer/feedback) | Deferred feedback capability | Internal | Standard | Standard | Simple |
| [Notification](./application-layer/notification) | Manage and send notifications | Internal | Standard | High | Simple |
| [Profile](./application-layer/profile) | Manage customer users and profiles | Confidential | High | High | Simple |
| [Reporting](./application-layer/reporting) | Legacy transitional report surface; DataHub is target for new read models | Internal | Standard | Standard | Simple |
![Application Architecture 2](./images/fairspot-application-arch-2.png)
