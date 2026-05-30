using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Infrastructure;

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
            e.Property(x => x.Payload).IsRequired().HasColumnType("jsonb");
            e.HasIndex(x => x.SourceEventId).IsUnique().HasDatabaseName("ux_event_inbox_source_event_id");
            e.HasIndex(x => new { x.TenantId, x.ProcessedAt }).HasDatabaseName("ix_event_inbox_tenant_processed");
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

// Idempotent event inbox — one row per source event ID.
public sealed class EventInboxRecord
{
    public long Id { get; set; }
    public string SourceEventId { get; set; } = "";
    public string EventName { get; set; } = "";
    public string TenantId { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Payload { get; set; } = "{}";
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ProcessingError { get; set; }
}

// Tracks the last processed event per projection so rebuilds know where to resume.
public sealed class ProjectionCheckpoint
{
    public string ProjectionName { get; set; } = "";
    public string LastProcessedEventId { get; set; } = "";
    public DateTimeOffset LastProcessedAt { get; set; }
    public long EventCount { get; set; }
}
