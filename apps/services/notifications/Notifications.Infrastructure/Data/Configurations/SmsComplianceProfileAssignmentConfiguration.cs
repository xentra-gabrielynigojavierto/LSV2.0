using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsComplianceProfileAssignmentConfiguration : IEntityTypeConfiguration<SmsComplianceProfileAssignment>
{
    public void Configure(EntityTypeBuilder<SmsComplianceProfileAssignment> b)
    {
        b.ToTable("ntf_SmsComplianceProfileAssignments");
        b.HasKey(a => a.Id);

        b.Property(a => a.Id).HasColumnType("char(36)");
        b.Property(a => a.TenantId).IsRequired().HasColumnType("char(36)");
        b.Property(a => a.ProfileId).IsRequired().HasColumnType("char(36)");
        b.Property(a => a.Scope).IsRequired().HasMaxLength(30).HasDefaultValue("tenant");
        b.Property(a => a.Enabled).HasDefaultValue(true);

        b.HasIndex(a => new { a.TenantId, a.Scope, a.Enabled })
            .HasDatabaseName("IX_ntf_SmsComplianceAssignments_Tenant_Scope_Enabled");

        b.HasIndex(a => a.ProfileId)
            .HasDatabaseName("IX_ntf_SmsComplianceAssignments_ProfileId");
    }
}
