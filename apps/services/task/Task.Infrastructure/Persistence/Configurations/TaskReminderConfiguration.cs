using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Task.Domain.Entities;
using Task.Domain.Enums;

namespace Task.Infrastructure.Persistence.Configurations;

public class TaskReminderConfiguration : IEntityTypeConfiguration<TaskReminder>
{
    public void Configure(EntityTypeBuilder<TaskReminder> builder)
    {
        builder.ToTable("tasks_Reminders");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).IsRequired();
        builder.Property(r => r.TaskId).IsRequired();
        builder.Property(r => r.TenantId).IsRequired();

        builder.Property(r => r.ReminderType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.RemindAt).IsRequired();

        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(ReminderStatus.Pending);

        builder.Property(r => r.LastAttemptAt);
        builder.Property(r => r.SentAt);

        builder.Property(r => r.FailureReason).HasMaxLength(500);

        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.UpdatedAtUtc).IsRequired();

        builder.HasIndex(r => new { r.TenantId, r.TaskId })
            .HasDatabaseName("IX_Reminders_TenantId_TaskId");

        builder.HasIndex(r => new { r.Status, r.RemindAt })
            .HasDatabaseName("IX_Reminders_Status_RemindAt");
    }
}
