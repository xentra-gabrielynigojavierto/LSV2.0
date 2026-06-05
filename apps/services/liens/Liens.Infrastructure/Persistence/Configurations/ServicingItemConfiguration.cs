using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class ServicingItemConfiguration : IEntityTypeConfiguration<ServicingItem>
{
    public void Configure(EntityTypeBuilder<ServicingItem> builder)
    {
        builder.ToTable("liens_ServicingItems");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).IsRequired();
        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.OrgId).IsRequired();

        builder.Property(s => s.TaskNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.TaskType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Priority)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(s => s.AssignedTo)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.AssignedToUserId);

        builder.Property(s => s.CaseId);
        builder.Property(s => s.LienId);

        builder.Property(s => s.DueDate)
            .HasColumnType("date");

        builder.Property(s => s.Notes)
            .HasMaxLength(4000);

        builder.Property(s => s.Resolution)
            .HasMaxLength(4000);

        builder.Property(s => s.StartedAtUtc);
        builder.Property(s => s.CompletedAtUtc);
        builder.Property(s => s.EscalatedAtUtc);

        builder.Property(s => s.CreatedByUserId).IsRequired();
        builder.Property(s => s.UpdatedByUserId);
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();

        builder.HasIndex(s => new { s.TenantId, s.TaskNumber })
            .IsUnique()
            .HasDatabaseName("UX_ServicingItems_TenantId_TaskNumber");

        builder.HasIndex(s => new { s.TenantId, s.Status })
            .HasDatabaseName("IX_ServicingItems_TenantId_Status");

        builder.HasIndex(s => new { s.TenantId, s.Priority })
            .HasDatabaseName("IX_ServicingItems_TenantId_Priority");

        builder.HasIndex(s => new { s.TenantId, s.AssignedTo })
            .HasDatabaseName("IX_ServicingItems_TenantId_AssignedTo");

        builder.HasIndex(s => new { s.TenantId, s.CaseId })
            .HasDatabaseName("IX_ServicingItems_TenantId_CaseId");

        builder.HasIndex(s => new { s.TenantId, s.LienId })
            .HasDatabaseName("IX_ServicingItems_TenantId_LienId");
    }
}
