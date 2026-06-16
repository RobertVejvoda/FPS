using FPS.DataHub.Domain;
using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Infrastructure;

public enum EventProcessingStatus { Pending, Processed, Failed, Poisoned }

public sealed class DataHubDbContext(DbContextOptions<DataHubDbContext> options) : DbContext(options)
{
    public DbSet<EventInboxRecord> EventInbox => Set<EventInboxRecord>();
    public DbSet<ProjectionCheckpoint> ProjectionCheckpoints => Set<ProjectionCheckpoint>();
    public DbSet<DrawHistoryProjection> DrawHistory => Set<DrawHistoryProjection>();
    public DbSet<BookingOutcomeProjection> BookingOutcomes => Set<BookingOutcomeProjection>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<EventInboxRecord>(e =>
        {
            e.ToTable("datahub_event_inbox");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.SourceEventId).IsRequired().HasMaxLength(200);
            e.Property(x => x.EventName).IsRequired().HasMaxLength(200);
            e.Property(x => x.TenantId).IsRequired().HasMaxLength(100);
            e.Property(x => x.SourceService).HasMaxLength(100);
            e.Property(x => x.AggregateId).HasMaxLength(200);
            e.Property(x => x.Payload).IsRequired().HasColumnType("jsonb");
            e.Property(x => x.PayloadHash).HasMaxLength(64);
            e.Property(x => x.ProcessingStatus)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(EventProcessingStatus.Pending);
            e.HasIndex(x => x.SourceEventId).IsUnique().HasDatabaseName("ux_event_inbox_source_event_id");
            e.HasIndex(x => new { x.TenantId, x.ProcessedAt }).HasDatabaseName("ix_event_inbox_tenant_processed");
            e.HasIndex(x => new { x.ProcessingStatus, x.RetryCount }).HasDatabaseName("ix_event_inbox_status_retry");
        });

        model.Entity<ProjectionCheckpoint>(e =>
        {
            e.ToTable("datahub_projection_checkpoint");
            e.HasKey(x => x.ProjectionName);
            e.Property(x => x.ProjectionName).IsRequired().HasMaxLength(200);
            e.Property(x => x.LastProcessedEventId).IsRequired().HasMaxLength(200);
        });

        model.Entity<DrawHistoryProjection>(e =>
        {
            e.ToTable("datahub_draw_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.DrawAttemptId).IsRequired().HasMaxLength(200);
            e.Property(x => x.TenantId).IsRequired().HasMaxLength(100);
            e.Property(x => x.LocationId).IsRequired().HasMaxLength(100);
            e.Property(x => x.TimeSlot).IsRequired().HasMaxLength(50);
            e.Property(x => x.Status).IsRequired().HasMaxLength(50);
            e.Property(x => x.TriggerSource).HasMaxLength(50);
            e.Property(x => x.RunReason).HasMaxLength(500);
            e.Property(x => x.TriggeredBy).HasMaxLength(200);
            e.Property(x => x.SafeFailureReason).HasMaxLength(500);
            e.Property(x => x.AlgorithmVersion).HasMaxLength(50);
            e.HasIndex(x => x.DrawAttemptId).IsUnique().HasDatabaseName("ux_draw_history_attempt_id");
            e.HasIndex(x => new { x.TenantId, x.LocationId, x.Date, x.TimeSlot }).HasDatabaseName("ix_draw_history_tenant_location_date");
            e.HasIndex(x => new { x.TenantId, x.CompletedAt }).HasDatabaseName("ix_draw_history_tenant_completed");
        });

        model.Entity<BookingOutcomeProjection>(e =>
        {
            e.ToTable("datahub_booking_outcome");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.BookingRequestId).IsRequired().HasMaxLength(200);
            e.Property(x => x.TenantId).IsRequired().HasMaxLength(100);
            e.Property(x => x.RequestorId).IsRequired().HasMaxLength(100);
            e.Property(x => x.LocationId).IsRequired().HasMaxLength(100);
            e.Property(x => x.TimeSlot).IsRequired().HasMaxLength(50);
            e.Property(x => x.FinalStatus).IsRequired().HasMaxLength(50);
            e.Property(x => x.ReasonCode).HasMaxLength(100);
            e.Property(x => x.SafeReasonText).HasMaxLength(500);
            e.Property(x => x.AllocationId).HasMaxLength(200);
            e.Property(x => x.SlotId).HasMaxLength(200);
            e.Property(x => x.AllocationSource).HasMaxLength(50);
            e.Property(x => x.DrawAttemptId).HasMaxLength(200);
            e.HasIndex(x => x.BookingRequestId).IsUnique().HasDatabaseName("ux_booking_outcome_request_id");
            e.HasIndex(x => new { x.TenantId, x.RequestorId, x.Date }).HasDatabaseName("ix_booking_outcome_tenant_requestor_date");
            e.HasIndex(x => new { x.TenantId, x.LocationId, x.Date }).HasDatabaseName("ix_booking_outcome_tenant_location_date");
            e.HasIndex(x => x.DrawAttemptId).HasDatabaseName("ix_booking_outcome_draw_attempt");
        });
    }
}

public sealed class EventInboxRecord
{
    public long Id { get; set; }
    public string SourceEventId { get; set; } = "";
    public string EventName { get; set; } = "";
    public int EventVersion { get; set; } = 1;
    public string TenantId { get; set; } = "";
    public string? SourceService { get; set; }
    public string? AggregateId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Payload { get; set; } = "{}";
    public string? PayloadHash { get; set; }
    public EventProcessingStatus ProcessingStatus { get; set; } = EventProcessingStatus.Pending;
    public int RetryCount { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ProcessingError { get; set; }
}

public sealed class ProjectionCheckpoint
{
    public string ProjectionName { get; set; } = "";
    public string LastProcessedEventId { get; set; } = "";
    public DateTimeOffset LastProcessedAt { get; set; }
    public long EventCount { get; set; }
}
