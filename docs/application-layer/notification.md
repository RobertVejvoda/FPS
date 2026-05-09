---
title: Notification
---

[Notification](../technology-layer/notification) provides functionalities to manage and display notifications to the user.

## Application Functions

- **Notification Delivery Function:**  
    Provides real-time notifications using web sockets, push services, email, and SMS.  
    Corresponds to features like booking confirmations, cancellations, and system alerts.

- **Notification Processing Function:**  
    Processes events for booking updates, security alerts, system maintenance, and changes.  
    Ensures that notifications are prioritized and dispatched appropriately.

- **Alert Management Function:**  
    Handles the routing and escalation of alerts for suspicious activities and audit log entries.  
    Supports both web and mobile notification channels.

- **Reminder and Scheduling Function:**  
    Schedules reminders for upcoming reservations and session expirations.  
    Balances between immediate alerts and scheduled notifications.

- **User Preference Management Function:**  
    Allows users to customize their notification settings across multiple channels and locales.  
    Integrates preference storage with immediate application of changes.

- **Localization and Integration Function:**  
    Supports localized notifications in multiple languages and integrates with external systems for additional channels.  
