using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LienTaskLienLinkConfiguration : IEntityTypeConfiguration<LienTaskLienLink>
{
    public void Configure(EntityTypeBuilder<LienTaskLienLink> builder)
    {
        builder.ToTable("liens_TaskLienLinks");

        builder.HasKey(l => new { l.TaskId, l.LienId });

        builder.Property(l => l.TaskId).IsRequired();
        builder.Property(l => l.LienId).IsRequired();
        builder.Property(l => l.CreatedByUserId).IsRequired();
        builder.Property(l => l.CreatedAtUtc).IsRequired();

        builder.HasIndex(l => l.LienId)
            .HasDatabaseName("IX_TaskLienLinks_LienId");

        builder.HasIndex(l => l.TaskId)
            .HasDatabaseName("IX_TaskLienLinks_TaskId");
    }
}
