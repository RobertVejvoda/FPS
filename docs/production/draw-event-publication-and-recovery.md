# Draw Event Publication And Recovery

## Current Implementation

Draw execution publishes integration events through Dapr PubSub in `QueueIntegrationEventsActivity`. The current implementation uses **fire-and-forget** event publication:

```csharp
await eventPublisher.PublishAsync(new DrawAttemptStartedEvent(...));
await eventPublisher.PublishAsync(new SlotAllocationCreatedEvent(...));
await eventPublisher.PublishAsync(new DrawAttemptCompletedEvent(...));
```

This approach has **acceptable risk** for the current demo/MVP stage but requires hardening before production use with real customer data.

## Event Publication Guarantees

### Current Guarantees

- **Draw attempt state** is persisted with ETag-based optimistic concurrency
- **Booking request status updates** are persisted idempotently
- **Lifecycle steps** are appended with retry logic on ETag conflicts
- **Events are published** during workflow execution
- **Dapr Workflow** ensures activities are not re-executed on replay

### Current Gaps

- **No transactional outbox**: Events are published separately from state updates
- **Service restart before event publication**: If the service crashes after persisting decisions but before publishing events, DataHub projections may not update until a recovery operation is performed
- **At-least-once delivery**: Dapr PubSub provides at-least-once delivery, but duplicate event handling is the consumer's responsibility

## Recovery Behavior

### Completed Draws

Completed Draw attempts are **immutable**. Re-triggering a completed Draw key returns the cached result without re-running allocation or re-publishing events.

This prevents:
- Duplicate allocations
- Inconsistent lottery outcomes
- Lost booking status updates

### Failed Draws

Failed Draw attempts require **explicit recovery**:

```csharp
var cmd = new TriggerDrawCommand(
    TenantId: "tenant-1",
    LocationId: "loc-1",
    Date: targetDate,
    TimeSlotStart: slotStart,
    TimeSlotEnd: slotEnd,
    Reason: "Manual recovery by HR admin",
    TriggerSource: "recovery",
    AllowRecovery: true
);
```

When `AllowRecovery = true`:
1. The failed attempt is archived with status `FailedArchived`
2. A recovery lifecycle step is added
3. A new workflow is started with `TriggerSource = "recovery"`
4. The new workflow uses the same deterministic seed, ensuring consistent reallocation

### In-Progress Draws

In-progress Draw attempts return the current state without starting a duplicate workflow. This handles:
- Duplicate scheduler ticks
- Manual triggers during scheduled execution
- Service restart during workflow execution (Dapr Workflow resumes)

## Transactional Outbox Recommendations

### Option 1: Dapr Transactional Outbox (Recommended for supported state stores)

Use Dapr's built-in transactional outbox where the state store component supports transactions.

**Supported state stores:**
- PostgreSQL (with transactional state store component)
- SQL Server (with transactional state store component)
- MongoDB (with transactions enabled in 4.0+)

**Implementation:**
- Configure Dapr state store component with `outbox.enabled = true`
- Update Draw repository to use Dapr state transactions
- Events are published atomically with state updates

**Benefits:**
- Guaranteed event delivery after state persistence
- No custom outbox implementation needed
- Dapr handles retry and dead-letter behavior

**Limitations:**
- Requires state store with transaction support
- Not available for all Dapr state store components

### Option 2: Service-Owned Outbox (Fallback for non-transactional stores)

Implement an explicit pending-event outbox within the Booking service.

**Implementation:**
1. Add `PendingEvent` state records beside Draw attempt state
2. When completing Draw, persist both:
   - Updated Draw attempt (with decisions and lifecycle)
   - Pending events (one record per event to publish)
3. Background publisher reads pending events and publishes to Dapr PubSub
4. Mark events as published after broker acknowledgement
5. Retry unpublished events on service restart

**Benefits:**
- Works with any Dapr state store
- Full control over retry logic and dead-letter handling
- Can batch event publication

**Limitations:**
- More implementation complexity
- Requires background publisher task
- Must handle service restart during event publication

### Option 3: Accept Fire-and-Forget Risk (Current MVP approach)

**Acceptable for:**
- Demo/MVP environments
- Non-production tenants
- Test-ready validation before hardening

**Risks:**
- Service restart after decision persistence but before event publication loses events
- DataHub projections may be incomplete until manual recovery
- HR/employee views may not show Draw outcomes until explicit refresh

**Mitigations:**
- Completed Draw attempts are immutable and can be queried from Booking service
- DataHub can request backfill from Booking for missing Draw outcomes
- Manual recovery path allows restarting failed Draws

## Event Idempotency

All published events include deterministic IDs to support idempotent consumption:

```csharp
EventId = $"{tenantId}:{drawKey}:{eventType}:{requestId}"
```

DataHub inbox uses `EventId` as the idempotency key, preventing duplicate projection updates even if events are delivered multiple times.

## Service Restart Scenarios

### Scenario 1: Restart Before Decision Persistence

**State:** Draw workflow in progress, decisions not yet persisted.

**Recovery:**
- Dapr Workflow resumes from last completed activity
- Allocation is re-run with same deterministic seed
- Decisions are persisted
- Events are published
- Draw completes normally

**Outcome:** No data loss, eventual consistency maintained.

### Scenario 2: Restart After Decision Persistence, Before Event Publication

**State:** Decisions persisted, events not published (fire-and-forget).

**Current Risk:**
- DataHub projections may be incomplete
- HR Draw History may not show completed Draw
- Employee Past Draw Outcomes may be missing

**Mitigation:**
1. Query Draw attempt from Booking service (source of truth)
2. DataHub can request event backfill for missing Draws
3. Manual recovery trigger can re-publish events if needed

**Future Hardening:** Use transactional outbox to eliminate this gap.

### Scenario 3: Restart After Completion

**State:** Draw attempt marked `Completed`, all decisions and events persisted.

**Recovery:** None needed. Completed state is cached and immutable.

**Outcome:** Re-triggering returns cached result without re-execution.

## Duplicate Trigger Protection

### Deterministic Workflow Instance ID

Workflow instance ID = Draw key:

```csharp
var instanceId = $"draw:{tenantId}:{locationId}:{date:yyyy-MM-dd}:{timeSlot.Start:HHmm}";
```

Starting the same workflow ID twice results in:
- First call: Workflow starts
- Subsequent calls: Dapr Workflow returns "already exists" error

The workflow starter catches this error and returns `AlreadyRunning` status gracefully.

### Status Checks Before Workflow Start

Before starting a workflow, `TriggerDrawHandler` checks:

1. **Completed:** Return cached result, do not re-run
2. **InProgress:** Return current status, do not start duplicate
3. **Failed:** Return failed status unless `AllowRecovery = true`
4. **No prior attempt:** Start workflow

This prevents duplicate workflow starts even if multiple replicas receive the same trigger.

### ETag-Based Lifecycle Updates

Lifecycle step appends use optimistic concurrency with retry:

```csharp
for (int attempt = 0; attempt < MaxRetries; attempt++)
{
    var drawAttempt = await repo.GetByKeyAsync(drawKey, ct);
    drawAttempt.LifecycleSteps.Add(newStep);
    if (await repo.TrySaveAsync(drawAttempt, ct))
        return; // Success
    // ETag conflict — retry with fresh read
}
```

This prevents lost lifecycle step updates during concurrent activity execution.

## Production Readiness Checklist

Before using Draw execution with real customer data:

- [ ] **Choose outbox strategy**: Transactional outbox (Option 1) or service-owned outbox (Option 2)
- [ ] **Implement chosen outbox**: Update `QueueIntegrationEventsActivity` to use transactional or explicit outbox
- [ ] **Test service restart scenarios**: Verify event publication after restart
- [ ] **Test duplicate triggers**: Verify idempotency under concurrent scheduler ticks
- [ ] **Test recovery path**: Verify failed Draw can be retried with `AllowRecovery = true`
- [ ] **Document backfill procedure**: How DataHub requests missing events from Booking
- [ ] **Configure Dapr state store**: Ensure ETag support is enabled
- [ ] **Monitor event lag**: DataHub projection freshness metrics
- [ ] **Define SLO**: Acceptable lag between Draw completion and DataHub projection visibility

## Recommended Next Steps

1. **Choose outbox strategy** based on selected Dapr state store component
2. **Implement transactional outbox** if state store supports it (PostgreSQL, SQL Server, MongoDB with transactions)
3. **Add integration tests** for restart scenarios and event publication guarantees
4. **Document backfill procedure** for DataHub to request missing Draw events from Booking
5. **Add monitoring** for Draw completion lag and event publication failures
6. **Test recovery path** end-to-end with HR admin workflow

## References

- `docs/production/draw-scheduling-and-workflow.md` — Draw execution model
- `docs/application-layer/datahub.md` — DataHub event consumption and projections
- Dapr Transactional Outbox: https://docs.dapr.io/developing-applications/building-blocks/state-management/howto-outbox/
- Dapr State Management: https://docs.dapr.io/developing-applications/building-blocks/state-management/
