using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Models;

namespace PlatformAuditEventService.Data;

/// <summary>
/// EF Core DbContext for the Platform Audit/Event Service.
///
/// Provider support:
///   InMemory — development/test; registered via UseInMemoryDatabase.
///   MySQL 8.x — production; registered via UseMySql (Pomelo 8).
///
/// Schema design principles:
///   - Append-only: no UPDATE or DELETE operations are exposed by the service layer.
///   - All entity type configurations are in separate IEntityTypeConfiguration classes
///     under Data/Configurations/ and are discovered via ApplyConfigurationsFromAssembly.
///   - Enum columns are stored as tinyint with int conversion (stable, compact, indexable).
///   - DateTimeOffset fields are stored as datetime(6) UTC; Pomelo handles the conversion.
///   - bigint AUTO_INCREMENT surrogate PKs for clustered index efficiency;
///     public-facing Guid identifiers are stored in char(36) unique columns.
///   - No navigation properties on entities — relationships are resolved at the
///     application layer to keep the domain models persistence-agnostic.
///
/// Tables:
///   AuditEvents              — legacy AuditEvent model (in-service use only)
///   AuditEventRecords        — canonical rich audit event model
///   AuditExportJobs          — async export job tracking
///   IntegrityCheckpoints     — aggregate hash snapshots for tamper detection
///   IngestSourceRegistrations — advisory source registry
///   LegalHolds               — compliance-driven retention holds
///   OutboxMessages           — transactional outbox for durable event forwarding
///   AuditAlerts              — durable alert records generated from anomaly detection
/// </summary>
public sealed class AuditEventDbContext : DbContext
{
    public AuditEventDbContext(DbContextOptions<AuditEventDbContext> options)
        : base(options) { }

    // ── DbSets ────────────────────────────────────────────────────────────────

    /// <summary>Legacy audit event records (backed by the old AuditEvent model).</summary>
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <summary>Canonical rich audit event records (backed by AuditEventRecord).</summary>
    public DbSet<AuditEventRecord> AuditEventRecords => Set<AuditEventRecord>();

    /// <summary>Async export job tracking.</summary>
    public DbSet<AuditExportJob> AuditExportJobs => Set<AuditExportJob>();

    /// <summary>Aggregate hash snapshots for integrity verification.</summary>
    public DbSet<IntegrityCheckpoint> IntegrityCheckpoints => Set<IntegrityCheckpoint>();

    /// <summary>Advisory registry of known ingest sources.</summary>
    public DbSet<IngestSourceRegistration> IngestSourceRegistrations => Set<IngestSourceRegistration>();

    /// <summary>Legal holds placed on audit event records by compliance officers.</summary>
    public DbSet<LegalHold> LegalHolds => Set<LegalHold>();

    /// <summary>Transactional outbox messages pending delivery to downstream brokers.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>Durable alert records created from anomaly detection results.</summary>
    public DbSet<AuditAlert> AuditAlerts => Set<AuditAlert>();

    // ── Model configuration ────────────────────────────────────────────────────

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Discover and apply all IEntityTypeConfiguration<T> classes in this assembly.
        // Configurations live in Data/Configurations/ and are picked up automatically
        // without any manual registration required here.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditEventDbContext).Assembly);
    }
}
