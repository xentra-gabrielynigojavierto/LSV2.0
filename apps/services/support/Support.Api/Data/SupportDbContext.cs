using Support.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Support.Api.Data;

public class SupportDbContext : DbContext
{
    public SupportDbContext(DbContextOptions<SupportDbContext> options) : base(options) { }

    public DbSet<SupportTicket> Tickets => Set<SupportTicket>();
    public DbSet<TicketNumberSequence> TicketNumberSequences => Set<TicketNumberSequence>();
    public DbSet<SupportTicketComment> TicketComments => Set<SupportTicketComment>();
    public DbSet<SupportTicketEvent> TicketEvents => Set<SupportTicketEvent>();
    public DbSet<SupportTicketAttachment> TicketAttachments => Set<SupportTicketAttachment>();
    public DbSet<SupportTicketProductRef> TicketProductRefs => Set<SupportTicketProductRef>();
    public DbSet<SupportQueue> Queues => Set<SupportQueue>();
    public DbSet<SupportQueueMember> QueueMembers => Set<SupportQueueMember>();
    public DbSet<ExternalCustomer> ExternalCustomers => Set<ExternalCustomer>();
    public DbSet<SupportTenantSettings> TenantSettings => Set<SupportTenantSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var ticket = modelBuilder.Entity<SupportTicket>();
        ticket.ToTable("support_tickets");
        ticket.HasKey(t => t.Id);

        ticket.Property(t => t.Id).HasColumnName("id").HasMaxLength(36);
        ticket.Property(t => t.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        ticket.Property(t => t.ProductCode).HasColumnName("product_code").HasMaxLength(50);
        ticket.Property(t => t.TicketNumber).HasColumnName("ticket_number").HasMaxLength(40).IsRequired();
        ticket.Property(t => t.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        ticket.Property(t => t.Description).HasColumnName("description").HasColumnType("text");
        ticket.Property(t => t.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        ticket.Property(t => t.Priority).HasColumnName("priority").HasConversion<string>().HasMaxLength(20).IsRequired();
        ticket.Property(t => t.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(20);
        ticket.Property(t => t.Category).HasColumnName("category").HasMaxLength(100);
        ticket.Property(t => t.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(20).IsRequired();
        ticket.Property(t => t.RequesterUserId).HasColumnName("requester_user_id").HasMaxLength(64);
        ticket.Property(t => t.RequesterName).HasColumnName("requester_name").HasMaxLength(200);
        ticket.Property(t => t.RequesterEmail).HasColumnName("requester_email").HasMaxLength(320);
        ticket.Property(t => t.RequesterType).HasColumnName("requester_type").HasConversion<string>().HasMaxLength(20).IsRequired().HasDefaultValue(TicketRequesterType.InternalUser);
        ticket.Property(t => t.ExternalCustomerId).HasColumnName("external_customer_id").HasMaxLength(36);
        ticket.Property(t => t.VisibilityScope).HasColumnName("visibility_scope").HasConversion<string>().HasMaxLength(20).IsRequired().HasDefaultValue(TicketVisibilityScope.Internal);
        ticket.Property(t => t.AssignedUserId).HasColumnName("assigned_user_id").HasMaxLength(64);
        ticket.Property(t => t.AssignedQueueId).HasColumnName("assigned_queue_id").HasMaxLength(64);
        ticket.Property(t => t.DueAt).HasColumnName("due_at");
        ticket.Property(t => t.ResolvedAt).HasColumnName("resolved_at");
        ticket.Property(t => t.ClosedAt).HasColumnName("closed_at");
        ticket.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        ticket.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        ticket.Property(t => t.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(64);
        ticket.Property(t => t.UpdatedByUserId).HasColumnName("updated_by_user_id").HasMaxLength(64);

        ticket.HasIndex(t => t.TenantId).HasDatabaseName("ix_support_tickets_tenant");
        ticket.HasIndex(t => new { t.TenantId, t.TicketNumber }).IsUnique().HasDatabaseName("ux_support_tickets_tenant_number");
        ticket.HasIndex(t => new { t.TenantId, t.Status }).HasDatabaseName("ix_support_tickets_tenant_status");
        ticket.HasIndex(t => new { t.TenantId, t.Priority }).HasDatabaseName("ix_support_tickets_tenant_priority");
        ticket.HasIndex(t => new { t.TenantId, t.ProductCode }).HasDatabaseName("ix_support_tickets_tenant_product");
        ticket.HasIndex(t => t.CreatedAt).HasDatabaseName("ix_support_tickets_created_at");
        ticket.HasIndex(t => new { t.TenantId, t.ExternalCustomerId }).HasDatabaseName("ix_support_tickets_tenant_ext_customer");
        ticket.HasIndex(t => new { t.TenantId, t.RequesterType }).HasDatabaseName("ix_support_tickets_tenant_requester_type");
        ticket.HasIndex(t => new { t.TenantId, t.VisibilityScope }).HasDatabaseName("ix_support_tickets_tenant_visibility");

        var seq = modelBuilder.Entity<TicketNumberSequence>();
        seq.ToTable("support_ticket_number_sequences");
        seq.HasKey(s => new { s.TenantId, s.Year });
        seq.Property(s => s.TenantId).HasColumnName("tenant_id").HasMaxLength(64);
        seq.Property(s => s.Year).HasColumnName("year");
        seq.Property(s => s.LastValue).HasColumnName("last_value");
        seq.Property(s => s.RowVersion).HasColumnName("row_version").IsConcurrencyToken();

        var c = modelBuilder.Entity<SupportTicketComment>();
        c.ToTable("support_ticket_comments");
        c.HasKey(x => x.Id);
        c.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
        c.Property(x => x.TicketId).HasColumnName("ticket_id").HasMaxLength(36).IsRequired();
        c.Property(x => x.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        c.Property(x => x.CommentType).HasColumnName("comment_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        c.Property(x => x.Visibility).HasColumnName("visibility").HasConversion<string>().HasMaxLength(20).IsRequired();
        c.Property(x => x.Body).HasColumnName("body").HasColumnType("text").IsRequired();
        c.Property(x => x.AuthorUserId).HasColumnName("author_user_id").HasMaxLength(64);
        c.Property(x => x.AuthorName).HasColumnName("author_name").HasMaxLength(200);
        c.Property(x => x.AuthorEmail).HasColumnName("author_email").HasMaxLength(320);
        c.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        c.HasIndex(x => x.TenantId).HasDatabaseName("ix_support_ticket_comments_tenant");
        c.HasIndex(x => x.TicketId).HasDatabaseName("ix_support_ticket_comments_ticket");
        c.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_support_ticket_comments_created_at");
        c.HasOne<SupportTicket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_support_ticket_comments_ticket");

        var e = modelBuilder.Entity<SupportTicketEvent>();
        e.ToTable("support_ticket_events");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
        e.Property(x => x.TicketId).HasColumnName("ticket_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(50).IsRequired();
        e.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(500).IsRequired();
        e.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasColumnType("text");
        e.Property(x => x.ActorUserId).HasColumnName("actor_user_id").HasMaxLength(64);
        e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        e.HasIndex(x => x.TenantId).HasDatabaseName("ix_support_ticket_events_tenant");
        e.HasIndex(x => x.TicketId).HasDatabaseName("ix_support_ticket_events_ticket");
        e.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_support_ticket_events_created_at");
        e.HasOne<SupportTicket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_support_ticket_events_ticket");

        var a = modelBuilder.Entity<SupportTicketAttachment>();
        a.ToTable("support_ticket_attachments");
        a.HasKey(x => x.Id);
        a.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
        a.Property(x => x.TicketId).HasColumnName("ticket_id").HasMaxLength(36).IsRequired();
        a.Property(x => x.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        a.Property(x => x.DocumentId).HasColumnName("document_id").HasMaxLength(128).IsRequired();
        a.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        a.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(150);
        a.Property(x => x.FileSizeBytes).HasColumnName("file_size_bytes");
        a.Property(x => x.UploadedByUserId).HasColumnName("uploaded_by_user_id").HasMaxLength(64);
        a.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        a.HasIndex(x => x.TenantId).HasDatabaseName("ix_support_ticket_attachments_tenant");
        a.HasIndex(x => x.TicketId).HasDatabaseName("ix_support_ticket_attachments_ticket");
        a.HasIndex(x => x.DocumentId).HasDatabaseName("ix_support_ticket_attachments_document");
        a.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_support_ticket_attachments_created_at");
        a.HasIndex(x => new { x.TenantId, x.TicketId, x.DocumentId })
            .IsUnique()
            .HasDatabaseName("ux_support_ticket_attachments_tenant_ticket_document");
        a.HasOne<SupportTicket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_support_ticket_attachments_ticket");

        var p = modelBuilder.Entity<SupportTicketProductRef>();
        p.ToTable("support_ticket_product_refs");
        p.HasKey(x => x.Id);
        p.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
        p.Property(x => x.TicketId).HasColumnName("ticket_id").HasMaxLength(36).IsRequired();
        p.Property(x => x.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        p.Property(x => x.ProductCode).HasColumnName("product_code").HasMaxLength(50).IsRequired();
        p.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
        p.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(128).IsRequired();
        p.Property(x => x.DisplayLabel).HasColumnName("display_label").HasMaxLength(255);
        p.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasColumnType("text");
        p.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(64);
        p.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        p.HasIndex(x => x.TenantId).HasDatabaseName("ix_support_ticket_product_refs_tenant");
        p.HasIndex(x => x.TicketId).HasDatabaseName("ix_support_ticket_product_refs_ticket");
        p.HasIndex(x => x.ProductCode).HasDatabaseName("ix_support_ticket_product_refs_product");
        p.HasIndex(x => x.EntityType).HasDatabaseName("ix_support_ticket_product_refs_entity_type");
        p.HasIndex(x => x.EntityId).HasDatabaseName("ix_support_ticket_product_refs_entity_id");
        p.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_support_ticket_product_refs_created_at");
        p.HasIndex(x => new { x.TenantId, x.TicketId, x.ProductCode, x.EntityType, x.EntityId })
            .IsUnique()
            .HasDatabaseName("ux_support_ticket_product_refs_unique");
        p.HasOne<SupportTicket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_support_ticket_product_refs_ticket");

        var q = modelBuilder.Entity<SupportQueue>();
        q.ToTable("support_queues");
        q.HasKey(x => x.Id);
        q.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
        q.Property(x => x.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        q.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        q.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
        q.Property(x => x.ProductCode).HasColumnName("product_code").HasMaxLength(50);
        q.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        q.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        q.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        q.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(64);
        q.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id").HasMaxLength(64);
        q.HasIndex(x => x.TenantId).HasDatabaseName("ix_support_queues_tenant");
        q.HasIndex(x => new { x.TenantId, x.Name }).HasDatabaseName("ix_support_queues_tenant_name");
        q.HasIndex(x => new { x.TenantId, x.ProductCode }).HasDatabaseName("ix_support_queues_tenant_product");
        q.HasIndex(x => x.IsActive).HasDatabaseName("ix_support_queues_is_active");
        q.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasDatabaseName("ux_support_queues_tenant_name");

        var qm = modelBuilder.Entity<SupportQueueMember>();
        qm.ToTable("support_queue_members");
        qm.HasKey(x => x.Id);
        qm.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
        qm.Property(x => x.QueueId).HasColumnName("queue_id").HasMaxLength(36).IsRequired();
        qm.Property(x => x.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        qm.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        qm.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
        qm.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        qm.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        qm.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        qm.HasIndex(x => x.TenantId).HasDatabaseName("ix_support_queue_members_tenant");
        qm.HasIndex(x => x.QueueId).HasDatabaseName("ix_support_queue_members_queue");
        qm.HasIndex(x => x.UserId).HasDatabaseName("ix_support_queue_members_user");
        qm.HasIndex(x => new { x.QueueId, x.UserId }).IsUnique().HasDatabaseName("ux_support_queue_members_queue_user");
        qm.HasOne<SupportQueue>()
            .WithMany()
            .HasForeignKey(x => x.QueueId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_support_queue_members_queue");

        var ec = modelBuilder.Entity<ExternalCustomer>();
        ec.ToTable("support_external_customers");
        ec.HasKey(x => x.Id);
        ec.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
        ec.Property(x => x.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        ec.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        ec.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
        ec.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        ec.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        ec.HasIndex(x => x.TenantId).HasDatabaseName("ix_support_external_customers_tenant");
        ec.HasIndex(x => x.Email).HasDatabaseName("ix_support_external_customers_email");
        ec.HasIndex(x => x.Status).HasDatabaseName("ix_support_external_customers_status");
        ec.HasIndex(x => new { x.TenantId, x.Email })
            .IsUnique()
            .HasDatabaseName("ux_support_external_customers_tenant_email");

        var ts = modelBuilder.Entity<SupportTenantSettings>();
        ts.ToTable("support_tenant_settings");
        ts.HasKey(x => x.TenantId);
        ts.Property(x => x.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        ts.Property(x => x.SupportMode)
            .HasColumnName("support_mode")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(SupportTenantMode.InternalOnly);
        ts.Property(x => x.CustomerPortalEnabled)
            .HasColumnName("customer_portal_enabled")
            .IsRequired()
            .HasDefaultValue(false);
        ts.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        ts.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
