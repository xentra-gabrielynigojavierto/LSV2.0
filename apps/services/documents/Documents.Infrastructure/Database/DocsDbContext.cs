using Documents.Domain.Entities;
using Documents.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace Documents.Infrastructure.Database;

public sealed class DocsDbContext : DbContext
{
    public DocsDbContext(DbContextOptions<DocsDbContext> options) : base(options) { }

    public DbSet<Document>        Documents       { get; set; } = null!;
    public DbSet<DocumentVersion> DocumentVersions { get; set; } = null!;
    public DbSet<DocumentAudit>   DocumentAudits   { get; set; } = null!;
    public DbSet<FileBlob>        FileBlobs        { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── Document ─────────────────────────────────────────────────────────
        mb.Entity<Document>(e =>
        {
            e.ToTable("docs_documents");
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasColumnName("id");
            e.Property(d => d.TenantId).HasColumnName("tenant_id");
            e.Property(d => d.ProductId).HasColumnName("product_id").HasMaxLength(100);
            e.Property(d => d.ReferenceId).HasColumnName("reference_id").HasMaxLength(500);
            e.Property(d => d.ReferenceType).HasColumnName("reference_type").HasMaxLength(100);
            e.Property(d => d.DocumentTypeId).HasColumnName("document_type_id");
            e.Property(d => d.Title).HasColumnName("title").HasMaxLength(500);
            e.Property(d => d.Description).HasColumnName("description").HasMaxLength(2000);
            e.Property(d => d.Status)
                .HasColumnName("status")
                .HasConversion(new EnumToStringConverter<DocumentStatus>());
            e.Property(d => d.MimeType).HasColumnName("mime_type").HasMaxLength(200);
            e.Property(d => d.FileSizeBytes).HasColumnName("file_size_bytes");
            e.Property(d => d.StorageKey).HasColumnName("storage_key");
            e.Property(d => d.StorageBucket).HasColumnName("storage_bucket");
            e.Property(d => d.Checksum).HasColumnName("checksum");
            e.Property(d => d.CurrentVersionId).HasColumnName("current_version_id");
            e.Property(d => d.VersionCount).HasColumnName("version_count");
            e.Property(d => d.ScanStatus)
                .HasColumnName("scan_status")
                .HasConversion(new EnumToStringConverter<ScanStatus>());
            e.Property(d => d.ScanCompletedAt).HasColumnName("scan_completed_at");
            e.Property(d => d.ScanDurationMs).HasColumnName("scan_duration_ms");
            e.Property(d => d.ScanThreats)
                .HasColumnName("scan_threats")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new())
                .HasColumnType("json");
            e.Property(d => d.ScanEngineVersion).HasColumnName("scan_engine_version").HasMaxLength(100);
            e.Property(d => d.IsPublishedAsLogo).HasColumnName("is_published_as_logo").HasDefaultValue(false);
            e.Property(d => d.IsDeleted).HasColumnName("is_deleted");
            e.Property(d => d.DeletedAt).HasColumnName("deleted_at");
            e.Property(d => d.DeletedBy).HasColumnName("deleted_by");
            e.Property(d => d.RetainUntil).HasColumnName("retain_until");
            e.Property(d => d.LegalHoldAt).HasColumnName("legal_hold_at");
            e.Property(d => d.CreatedAt).HasColumnName("created_at");
            e.Property(d => d.CreatedBy).HasColumnName("created_by");
            e.Property(d => d.UpdatedAt).HasColumnName("updated_at");
            e.Property(d => d.UpdatedBy).HasColumnName("updated_by");

            e.HasQueryFilter(d => !d.IsDeleted);

            e.HasIndex(d => d.TenantId).HasDatabaseName("idx_documents_tenant");
            e.HasIndex(d => new { d.TenantId, d.ProductId }).HasDatabaseName("idx_documents_product");
            e.HasIndex(d => new { d.TenantId, d.ReferenceId }).HasDatabaseName("idx_documents_reference");
        });

        // ── DocumentVersion ──────────────────────────────────────────────────
        mb.Entity<DocumentVersion>(e =>
        {
            e.ToTable("docs_document_versions");
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).HasColumnName("id");
            e.Property(v => v.DocumentId).HasColumnName("document_id");
            e.Property(v => v.TenantId).HasColumnName("tenant_id");
            e.Property(v => v.VersionNumber).HasColumnName("version_number");
            e.Property(v => v.MimeType).HasColumnName("mime_type").HasMaxLength(200);
            e.Property(v => v.FileSizeBytes).HasColumnName("file_size_bytes");
            e.Property(v => v.StorageKey).HasColumnName("storage_key");
            e.Property(v => v.StorageBucket).HasColumnName("storage_bucket");
            e.Property(v => v.Checksum).HasColumnName("checksum");
            e.Property(v => v.ScanStatus)
                .HasColumnName("scan_status")
                .HasConversion(new EnumToStringConverter<ScanStatus>());
            e.Property(v => v.ScanCompletedAt).HasColumnName("scan_completed_at");
            e.Property(v => v.ScanDurationMs).HasColumnName("scan_duration_ms");
            e.Property(v => v.ScanThreats)
                .HasColumnName("scan_threats")
                .HasConversion(
                    v2 => JsonSerializer.Serialize(v2, (JsonSerializerOptions?)null),
                    v2 => JsonSerializer.Deserialize<List<string>>(v2, (JsonSerializerOptions?)null) ?? new())
                .HasColumnType("json");
            e.Property(v => v.ScanEngineVersion).HasColumnName("scan_engine_version");
            e.Property(v => v.Label).HasColumnName("label").HasMaxLength(200);
            e.Property(v => v.IsDeleted).HasColumnName("is_deleted");
            e.Property(v => v.DeletedAt).HasColumnName("deleted_at");
            e.Property(v => v.DeletedBy).HasColumnName("deleted_by");
            e.Property(v => v.UploadedAt).HasColumnName("uploaded_at");
            e.Property(v => v.UploadedBy).HasColumnName("uploaded_by");

            e.HasQueryFilter(v => !v.IsDeleted);
            e.HasIndex(v => new { v.DocumentId, v.TenantId }).HasDatabaseName("idx_versions_document");

            e.HasOne(v => v.Document)
                .WithMany(d => d.Versions)
                .HasForeignKey(v => v.DocumentId);
        });

        // ── FileBlob ───────────────────────────────────────────────────────
        mb.Entity<FileBlob>(e =>
        {
            e.ToTable("docs_file_blobs");
            e.HasKey(b => b.StorageKey);
            e.Property(b => b.StorageKey).HasColumnName("storage_key").HasMaxLength(500);
            e.Property(b => b.Content).HasColumnName("content").HasColumnType("longblob").IsRequired();
            e.Property(b => b.MimeType).HasColumnName("mime_type").HasMaxLength(200);
            e.Property(b => b.SizeBytes).HasColumnName("size_bytes");
            e.Property(b => b.CreatedAtUtc).HasColumnName("created_at_utc");
        });

        // ── DocumentAudit ────────────────────────────────────────────────────
        mb.Entity<DocumentAudit>(e =>
        {
            e.ToTable("docs_document_audits");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id");
            e.Property(a => a.TenantId).HasColumnName("tenant_id");
            e.Property(a => a.DocumentId).HasColumnName("document_id");
            e.Property(a => a.Event).HasColumnName("event").HasMaxLength(100);
            e.Property(a => a.ActorId).HasColumnName("actor_id");
            e.Property(a => a.ActorEmail).HasColumnName("actor_email").HasMaxLength(500);
            e.Property(a => a.Outcome).HasColumnName("outcome").HasMaxLength(20);
            e.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
            e.Property(a => a.UserAgent).HasColumnName("user_agent").HasMaxLength(1000);
            e.Property(a => a.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
            e.Property(a => a.Detail).HasColumnName("detail").HasColumnType("json");
            e.Property(a => a.OccurredAt).HasColumnName("occurred_at");

            e.HasIndex(a => new { a.DocumentId, a.TenantId }).HasDatabaseName("idx_audits_document");
            e.HasIndex(a => a.TenantId).HasDatabaseName("idx_audits_tenant");
        });
    }
}
