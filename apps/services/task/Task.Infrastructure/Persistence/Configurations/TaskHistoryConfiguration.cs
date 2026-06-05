using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Configurations;

public class TaskHistoryConfiguration : IEntityTypeConfiguration<TaskHistory>
{
    public void Configure(EntityTypeBuilder<TaskHistory> builder)
    {
        builder.ToTable("tasks_History");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).IsRequired();
        builder.Property(h => h.TaskId).IsRequired();
        builder.Property(h => h.TenantId).IsRequired();

        builder.Property(h => h.Action)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(h => h.Details)
            .HasMaxLength(500);

        builder.Property(h => h.PerformedByUserId).IsRequired();
        builder.Property(h => h.CreatedAtUtc).IsRequired();

        builder.HasIndex(h => new { h.TenantId, h.TaskId })
            .HasDatabaseName("IX_History_TenantId_TaskId");
    }
}
