using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Infrastructure;

public enum EventProcessingStatus { Pending, Processed, Failed, Poisoned }

public sealed class DataHubDbContext(DbContextOptions<DataHubDbContext> options) : DbContext(options)
{
    public DbSet<EventInboxRecord> EventInbox => Set<EventInboxRecord>();
    public DbSet<ProjectionCheckpoint> ProjectionCheckpoints => Set<ProjectionCheckpoint>();

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
