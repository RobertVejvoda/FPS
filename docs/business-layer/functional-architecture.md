## Overview

FairSpot's functional architecture is designed to ensure equitable allocation of scarce shared capacity, starting with parking slots. Users can request capacity, and the system distributes available slots using a daily Draw process where the resource domain requires one. The current parking proof path prioritizes employees with fewer recent allocations after mandatory policy obligations are handled, increasing their chances of securing a slot. This approach supports fairness and maximizes utilization.

## Function Map

![Function Map](../images/fps-function-map.png)

Validation note: [Function Map Validation](../function-map-validation)

### [Identity](./identity)

  - **User Authentication**: Provides secure login through the configured OIDC/OAuth provider, including company SSO and FairSpot-local account paths.
  - **Password Management**: Delegates password reset and change flows to the enforcing identity provider.
  - **Session Management**: Manages user sessions to ensure security and prevent unauthorized access.
  - **Single Sign-On (SSO)**: Supports SSO for seamless access across multiple systems.
  - **Account Lockout**: Implements account lockout policies to protect against brute force attacks.
  - **Login Activity Monitoring**: Tracks and logs login activities for security auditing.
  - **Abuse Protection**: Uses IdP and edge controls such as rate limiting, account lockout, and optional challenge policies.
  - **MFA and Passkeys**: Relies on the enforcing identity provider for MFA, passkeys/WebAuthn, recovery codes, and role-based factor policy. FairSpot does not implement custom MFA code.

### [Profile](./profile)

- **Update Personal Information**: Allows users to update their personal details to keep their profile current.
- **View Booking History**: Enables users to view their past booking history for tracking reservations.
- **Provide Vehicle Information**: Allows users to add and update their vehicle information.
- **Manage Active Sessions**: Enables users to manage their active sessions for account security.
- **View Account Security State**: Shows account/security context that FairSpot can safely obtain from the active identity provider.
- **View Login History**: Allows users or administrators to inspect available login/security evidence where the IdP exposes it.
- **Access Customer Support**: Provides users with access to customer support for assistance with issues.
- **Receive Notifications**: Ensures users receive notifications for important events.
- **Provide Feedback**: Allows users to provide feedback to help improve the service.
- **Ensure Data Security**: Implements measures to protect user data with strong encryption and privacy protocols.


### [Bookings](./booking)

  - **Submit Booking Requests**: Users can submit booking requests to reserve parking slots, specifying time, duration, and any specific requirements.
  - **View Booking Status**: Users can check the status of their booking requests, including pending, confirmed, or denied.
  - **Cancel Booking**: Users can cancel their booking requests if their plans change.
  - **Modify Booking**: Users can modify the details of their existing booking requests.
  - **Get Available Slots**: Users can view available parking slots for a given time frame.
  - **Confirm Slot Usage**: Users can confirm the usage of their allocated parking slots.
  - **Booking History**: Users can view their past booking history.
  - **Submit Feedback**: Users can submit feedback on the booking process.
  - **Allocate Slots**: The system allocates parking slots based on booking requests.
  - **Notify Users**: The system notifies users about their booking status and slot allocation.
  - **Log Draw Process**: The system logs all steps of the draw process for transparency.
  - **Gather User Feedback**: The system collects user feedback on the draw process.
  - **Manual Override**: Authorized personnel can manually override the draw process with proper justification.
  - **Log Conflict Resolution**: The system logs all conflict resolution activities.
  - **Handle System Errors**: The system detects and corrects errors during the draw process.
  - **Document Draw Process**: The system provides comprehensive documentation of the draw process.
  - **Confirm Slot Allocation**: Users can confirm their parking slot allocation upon entering the garage.
  - **Send Automated Notifications**: The system sends automated notifications to users confirming their parking slot usage.
  - **Collect Usage Data**: The system collects data on parking slot usage patterns.
  - **Future Optimization Support**: The system may later use advanced analytics to improve allocation decisions once the core workflow is trusted.
  - **Demand Forecasting**: The system may forecast parking demand from historical request and usage data.
  - **Dynamic Policy Tuning**: The system may recommend policy adjustments based on current capacity, demand, and utilization patterns.
  - **Behavior Pattern Analysis**: The system may analyze repeated cancellations, no-shows, and request patterns to support fairer policy decisions.
  - **Anomaly Detection**: The system may detect unusual booking patterns that require HR or support review.
  - **Personalized Guidance**: The system may suggest better request times or alternative parking options based on user history and availability.
  - **Ensure Data Privacy Compliance**: The system ensures data privacy compliance.
  - **Monitor System Performance**: The system monitors performance during the draw process.
  - **Ensure Scalability**: The system ensures the draw process is scalable.
  - **Handle Cancellations and Modifications**: The system handles booking cancellations and modifications efficiently.

### [Billing and Payments](./billing)

  - **Deferred Product Scope**: Billing is not an active FairSpot product capability until the commercial offer is approved.
  - **Commercial Account Direction**: A future Billing slice may record tenant-level support, implementation, hosted-demo, dual-license, or subscription status.
  - **External Invoice Preference**: Initial paid work can rely on external contract/accounting tools rather than in-product invoice generation.
  - **Employee Data Boundary**: Employee booking and allocation details should not become commercial inputs by default.
  - **Auditability**: Future commercial-record changes must be tenant-scoped and auditable without exposing employee booking details.


### [Reporting](./reporting)

  - **Allocation Outcome Reports**: Shows request, allocation, rejection, cancellation, and reallocation outcomes without exposing hidden Draw internals or other employees' private data.
  - **Fairness Evidence Reports**: Compares request and assignment patterns, reason codes, and allocation history needed to explain the Draw outcome safely.
  - **Utilization Reports**: Summarizes shared-capacity usage by tenant, location, zone, slot/resource type, and time window.
  - **Operational History Reports**: Supports administrator/HR review of request history and policy-relevant events with tenant-safe filters.
  - **Projection Status Reports**: Shows read-model freshness, event processing status, and projection lag where supported.
  - **Privacy and Erasure Evidence**: Reports the status of account deletion or erasure requests without exposing raw PII in general reporting surfaces.
  - **Export Evidence**: Supports controlled exports for customer operations and audit review. Durable projection ownership belongs in DataHub; the legacy Reporting service remains a transitional compatibility surface.

### [Customer](./customer)

  - **Customer Requirements**:
  Manages tenant onboarding, readiness, identity setup, parking bootstrap, and customer-facing lifecycle evidence.
  - **User Management**: Coordinates tenant administrators, identity configuration, and role mapping while leaving credentials and MFA state in the IdP.
  - **Reporting Readiness**: Confirms the tenant has the reports and exports needed for evaluation and operations.
  - **Commercial Account Management**: Future tenant-level commercial records only after the commercial model is approved.
  - **Data Privacy and Security**: Secure storage and transmission of customer data, regular security audits, and timely breach notifications.
  - **Customer Support**: Deferred support workflow; current public docs cover issue intake, evaluation, and operational handoff expectations.
  - **Integration with Third-Party Services**: Generic OIDC, Dapr, event, and API contracts stay open; customer-specific adapters are commercial/support candidates.

  For more details, refer to the [Customer](./customer) documentation.

### [Feedback](./feedback)

- **Submit Feedback**: Allows users to submit feedback about the application to help improve the user experience.
- **View Feedback**: Enables developers to view user feedback to understand user issues and suggestions.
- **Respond to Feedback**: Allows support team members to respond to user feedback, ensuring users feel heard and valued.
- **Feedback Dashboard**: Provides a dashboard for developers and support team members to view, filter, and manage feedback.
- **Feedback Categorization**: Categorizes feedback by type (e.g., bug report, feature request) for better organization and analysis.
- **Feedback Notifications**: Sends notifications to users when their feedback is responded to, keeping them informed.
- **Feedback Logging**: Logs all feedback and responses for future reference and analysis.

### [Audit](./audit)

- **Audit Trail**: Maintains a comprehensive audit trail of all user actions and system events for accountability and transparency.
- **Change Tracking**: Tracks changes to critical data, including who made the changes and when.
- **Compliance Reporting**: Generates reports to ensure compliance with regulatory requirements.
- **Data Integrity Checks**: Regularly checks data integrity to detect and prevent unauthorized modifications.
- **Access Logs**: Logs access to sensitive data and resources to monitor and prevent unauthorized access.
- **Audit Log Management**: Provides tools for managing and archiving audit logs securely.
- **Alerting and Notifications**: Sends alerts for suspicious activities detected in audit logs.

### [Notification](./notification)

- **Real-time Notifications**: Sends real-time notifications to users for important events and updates.
- **Customizable Notification Settings**: Allows users to customize their notification preferences and delivery methods.
- **Email Notifications**: Sends email notifications for mandatory booking and allocation lifecycle events when a production provider is configured.
- **SMS Notifications**: Deferred channel; not part of the current v1 baseline.
- **Push Notifications**: Deferred channel; not part of the current v1 baseline.
- **In-app Notifications**: Displays notifications within the application for seamless user experience.
- **Notification History**: Maintains a history of all notifications sent to users for reference and auditing purposes.
- **Event-based Triggers**: Configures notifications based on specific events or conditions within the system.
- **Notification Templates**: Allows customization of notification templates to include branding and personalized messages.
- **Batch Notifications**: Supports sending batch notifications for bulk updates and announcements.
- **Notification Analytics**: Provides analytics on notification delivery and engagement to optimize communication strategies.


### User Interface

- **[UI Requirements](./ui):**
  - Ensures modular and reusable UI components adhering to design principles and accessibility standards.

  #### [Web App](./web-app)

  - **Responsive Design**: Adapts seamlessly to different screen sizes and devices.
  - **Cross-browser Support**: Functions consistently across major web browsers.
  - **Progressive Loading**: Implements progressive loading for improved performance.
  - **Offline Capabilities**: Provides basic functionality when offline.
  - **SEO Optimization**: Ensures content is optimized for search engines.
  - **Accessibility Features**: Complies with WCAG guidelines.
  - **Session Management**: Handles user sessions securely.

#### [Mobile App](./mobile-app)

  - **Native Performance**: Optimized for iOS and Android platforms.
  - **Hosted Login**: Uses the configured OIDC provider and secure token storage.
  - **Booking and My Spots**: Supports the employee booking, cancellation, and allocation-status workflows.
  - **Notification Access**: Shows in-app notification history and status; operating-system push notifications are deferred.
  - **Offline Tolerance**: Handles connectivity loss gracefully without promising full offline booking.
  - **Future Device Capabilities**: Location, camera, and native passkey/biometric ceremonies may be considered only when backed by approved product slices and IdP policy.
