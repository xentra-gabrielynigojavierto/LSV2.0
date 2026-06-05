using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsGovernanceReleaseItemConfiguration : IEntityTypeConfiguration<SmsGovernanceReleaseItem>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceReleaseItem> b)
    {
        b.ToTable("ntf_SmsGovernanceReleaseItems");
        b.HasKey(i => i.Id);

        b.Property(i => i.Id).HasColumnType("char(36)");
        b.Property(i => i.ReleasePackageId).IsRequired().HasColumnType("char(36)");
        b.Property(i => i.EntityId).IsRequired().HasColumnType("char(36)");
        b.Property(i => i.EntityType).IsRequired().HasMaxLength(30);
        b.Property(i => i.ActionType).IsRequired().HasMaxLength(20);
        b.Property(i => i.EntitySnapshotJson).HasColumnType("mediumtext");
        b.Property(i => i.CreatedBy).HasMaxLength(200);

        // All items for a release package (most frequent query)
        b.HasIndex(i => i.ReleasePackageId)
            .HasDatabaseName("IX_ntf_SmsGovRelItems_Package");

        // Entity lookup (find all releases containing this entity)
        b.HasIndex(i => new { i.EntityType, i.EntityId })
            .HasDatabaseName("IX_ntf_SmsGovRelItems_Entity");
    }
}
