using Comms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comms.Infrastructure.Persistence.Configurations;

public class ExternalParticipantIdentityConfiguration : IEntityTypeConfiguration<ExternalParticipantIdentity>
{
    public void Configure(EntityTypeBuilder<ExternalParticipantIdentity> builder)
    {
        builder.ToTable("comms_ExternalParticipantIdentities");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();

        builder.Property(e => e.NormalizedEmail)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.DisplayName).HasMaxLength(500);
        builder.Property(e => e.ParticipantId);
        builder.Property(e => e.IsActive).IsRequired();

        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedByUserId);
        builder.Property(e => e.UpdatedByUserId);

        builder.HasIndex(e => new { e.TenantId, e.NormalizedEmail })
            .HasDatabaseName("IX_ExternalIdentities_TenantId_NormalizedEmail")
            .IsUnique();
    }
}
