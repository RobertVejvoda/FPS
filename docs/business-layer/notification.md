## Purpose

FairSpot notifications keep employees, HR, and administrators informed about allocation outcomes and policy-sensitive events. For v1, notification delivery is part of the core parking workflow, not an optional enhancement.

## Channel Decision

v1 requires both:

- in-app notifications;
- email notifications.

Push notifications may be added later, but they are not required for v1.
SMS is not a v1 channel. If SMS is added later, use Twilio SMS behind a Dapr output binding; do not introduce Amazon SNS as a notification provider.

Implementation is sliced. `N001` establishes the Booking-event consumer and durable in-app notification records first. Email delivery remains a v1 requirement, but it is implemented in a later Notification slice after the in-app record contract and idempotency behavior are stable.

Production email delivery uses the logical Dapr output binding `notification-email`. The current hosted/demo provider is Twilio SendGrid (`bindings.twilio.sendgrid`); application code must invoke the binding contract rather than a provider SDK. Local and evaluation profiles may keep the in-memory sender unless a real staging binding is explicitly configured.

Booking operational events currently carry recipient user IDs, not email addresses. Real employee email delivery therefore requires a Profile or Identity recipient lookup before the SendGrid sender can deliver to users. Sales/onboarding alerts may send immediately because the configured recipient is already an email address.

### Email delivery configuration (SendGrid)

The Notification service selects the email sender by configuration. Any of `SendGrid`, `DaprSendGrid`, or `DaprBinding` in `Notification:Email:Provider` activates the Dapr-binding sender; anything else (or unset) keeps the in-memory sender. When the SendGrid provider is selected, `Notification:Email:FromEmail` is **required** — the service fails fast at startup if it is missing, because SendGrid needs a verified From address at send time.

| Setting | Env var | Example | Notes |
|---|---|---|---|
| Provider | `Notification__Email__Provider` | `SendGrid` | Unset/`InMemory` keeps the evaluation sender |
| Binding name | `Notification__Email__BindingName` | `notification-email` | Logical Dapr binding; app never names a provider SDK |
| From address | `Notification__Email__FromEmail` | `notifications@fairspot.net` | Required for SendGrid; public config, not a secret |
| From name | `Notification__Email__FromName` | `FairSpot` | Public config |

**Operator prerequisites** (complete before enabling SendGrid in any hosted/demo runtime):

- SendGrid domain authentication configured and verified for `fairspot.net`.
- The SendGrid-generated CNAME records added to Cloudflare DNS **DNS-only (gray cloud), not proxied**.
- Verified sender identity: from `notifications@fairspot.net`, reply-to `support@fairspot.net`.
- Cloudflare Email Routing (or another mailbox provider) routing `notifications@fairspot.net` and `support@fairspot.net` to a real monitored mailbox.
- A SendGrid API key restricted to **Mail Send** only.

**Secret handling.** The real SendGrid API key must never be committed or pasted into issues, PRs, docs, screenshots, or tracked env files. Tracked env examples/templates may contain **placeholders only** (e.g. `SENDGRID_API_KEY=<set-in-ignored-operator-env-or-secret-manager>`). The real key lives only in an ignored operator env file or is provisioned directly into the active Dapr secret store as secret name `sendgrid-credentials`, key `apiKey`. The Dapr component YAML references it solely through `secretKeyRef`; neither application code nor the component reads the raw key from app configuration.

**NAS seeding path.** Set `SENDGRID_API_KEY` in the ignored operator env source `code/infrastructure/nas.env` (it already feeds compose and `vault-init`). When present, `vault-init` seeds Vault as `secret/dapr/sendgrid-credentials apiKey=…`; when absent it is skipped and the `notification-email` binding stays unconfigured. Do not place SendGrid config in `cloudflared.yml`. For the Vault-backed profile the equivalent manual shape is:

```bash
vault kv put secret/dapr/sendgrid-credentials apiKey=<real SendGrid API key>
```

## Notification Classes

| Class | Meaning | User preference allowed |
| --- | --- | --- |
| Critical operational | Required to understand booking, allocation, penalty, or manual decision outcomes. | No |
| Reminder | Helps users avoid missed usage or cancellation windows. | Yes |
| Informational | Product, maintenance, or non-critical updates. | Yes |

Critical operational notifications are mandatory and must be sent through both in-app and email channels.

## Mandatory V1 Events

FairSpot must notify affected employees for these events:

| Event | Recipient | Required channels |
| --- | --- | --- |
| Request submitted | Requestor | In-app, email |
| Request rejected | Requestor | In-app, email |
| Slot allocated | Requestor | In-app, email |
| Slot allocated by reallocation | Requestor | In-app, email |
| Request cancelled before allocation | Requestor | In-app, email |
| Allocated reservation cancelled | Requestor | In-app, email |
| Slot released and reallocated | Original requestor and new requestor | In-app, email |
| Late-cancellation penalty applied | Requestor | In-app, email |
| No-show recorded | Requestor | In-app, email |
| No-show penalty applied | Requestor | In-app, email |
| Manual correction or override | Affected requestor | In-app, email |
| Draw completed | Requestors included in the Draw | In-app, email |

When a request remains `Pending` after the Draw because it was eligible but no matching capacity was available, the Draw completed notification must explain that the request is still waiting for a released slot until the requested time slot expires.

FairSpot must notify HR or configured administrators for these events:

| Event | Recipient |
| --- | --- |
| Company-car overflow rejection occurs | HR/facility manager |
| Draw fails or requires manual intervention | HR/facility manager |
| Policy publication fails validation | Administrator/configuration manager |
| Manual correction is applied | HR/facility manager and auditor where configured |

## Message Content

Employee-facing notifications must be clear and avoid implementation details.

Each notification must include:

- notification type;
- request date and time slot;
- location where relevant;
- current request status;
- short human-readable reason;
- next action when one exists;
- timestamp.

Notifications must not expose:

- random seed;
- internal algorithm details;
- stack traces;
- private details about other employees;
- audit-only diagnostic fields.

## Employee-Facing Message Examples

| Event | Example message |
| --- | --- |
| Request submitted | Your parking request was submitted and is waiting for allocation. |
| Request rejected | Your parking request could not be allocated because no matching slot is available. |
| Slot allocated | A parking slot was allocated to your request. |
| Slot allocated by reallocation | A parking slot became available and was allocated to your request. |
| Request cancelled before allocation | Your parking request was cancelled. No penalty was applied. |
| Allocated reservation cancelled | Your allocated parking reservation was cancelled. |
| Late-cancellation penalty applied | A late-cancellation penalty was applied because the reservation was cancelled after allocation. |
| No-show recorded | Your allocated parking slot was not confirmed as used. |
| No-show penalty applied | A no-show penalty was applied according to parking policy. |
| Manual correction | Your parking request was updated by an authorized administrator. |
| Draw completed | Parking allocation for your requested time slot is complete. |

## Preferences

User preferences may control:

- reminder notifications;
- informational notifications;
- preferred reminder timing;
- future optional channels such as push notifications.

User preferences must not disable critical operational notifications for booking, allocation, cancellation, reallocation, no-show, penalty, or manual correction outcomes.

## Delivery and Idempotency

Notification delivery must be idempotent.

Rules:

- Booking workflows publish notification events asynchronously after the authoritative booking state change is persisted.
- the same source event must not create duplicate in-app notifications;
- the same source event must not send duplicate emails;
- each notification should have a stable deduplication key based on event ID, recipient, notification type, and channel;
- retries must use the same deduplication key;
- in-app notification creation and email sending may complete independently;
- failure in one channel must not silently suppress the other channel.
- notification delivery failure must not roll back a completed booking cancellation, allocation, reallocation, penalty, or Draw outcome.

## Delivery Failure Behavior

If in-app notification creation fails:

- FairSpot must log the failure;
- FairSpot must retry according to infrastructure policy;
- email delivery should still be attempted.

If email delivery fails:

- FairSpot must log the failure;
- FairSpot must retain the in-app notification;
- FairSpot should retry according to infrastructure policy;
- persistent email failure should be visible to support or administrators.

FairSpot must not roll back a completed booking or allocation solely because email delivery failed.

## Slice N001: In-App Booking Event Records

`N001` is the first Notification implementation slice. It consumes Booking events and persists in-app notification records only.

N001 must:

- consume Booking event envelopes from the Booking event topic;
- create in-app records for affected recipients;
- deduplicate by `eventId + recipientId + notificationType + channel`;
- keep unread/read state on the in-app record;
- store source event ID, recipient, notification type, related booking request ID, related date/time slot, location, message text, delivery status, and timestamps;
- tolerate additive event payload fields;
- ignore or reject malformed events without creating misleading notifications.

N001 must not:

- send email;
- send push notifications;
- expose an SSE stream;
- add notification history or unread-count APIs;
- implement user notification preferences;
- query Booking or Profile to infer recipients not present in the event;
- change Booking state or publish new Booking events.

The full v1 requirement for email remains authoritative. Later Notification slices add employee email address resolution, retry policy, preferences, streaming/history APIs, and production persistence without changing the N001 in-app record idempotency contract.

## Notification History

Employees must be able to view their in-app notification history.

Notification history should support:

- unread/read status;
- timestamp;
- notification type;
- related request ID;
- related date/time slot;
- message text;
- basic filtering by unread, booking, penalty, or system notification.

## Audit and Reporting

Notification delivery is not the source of truth for booking outcomes. Booking, allocation, penalty, and audit records remain authoritative.

Notification service must record:

- source event ID;
- recipient;
- channel;
- notification type;
- deduplication key;
- delivery status;
- failure reason when delivery fails;
- timestamps for creation, send attempt, success, and failure.

## Acceptance Criteria For Implementation

- Given a request is submitted, when FairSpot accepts it into the queue, then the requestor receives both in-app and email notifications.
- Given a request is allocated, when the allocation is persisted, then the requestor receives both in-app and email notifications.
- Given a request is rejected, when the rejection is persisted, then the requestor receives both in-app and email notifications with a clear reason.
- Given an allocated reservation is cancelled, when the cancellation is persisted, then the original requestor receives both in-app and email notifications.
- Given a released slot is reallocated, when the new allocation is persisted, then both original and new affected requestors receive both in-app and email notifications.
- Given a penalty is applied, when the penalty is persisted, then the affected requestor receives both in-app and email notifications.
- Given a no-show is recorded, when the no-show status is persisted, then the affected requestor receives both in-app and email notifications.
- Given the same source event is processed twice, when notifications are generated, then FairSpot does not create duplicate in-app notifications or duplicate emails.
- Given email delivery fails, when the in-app notification succeeds, then booking state remains unchanged and email retry/failure is recorded.
- Given a user disables reminders, when a critical operational event occurs, then FairSpot still sends both in-app and email notifications.
