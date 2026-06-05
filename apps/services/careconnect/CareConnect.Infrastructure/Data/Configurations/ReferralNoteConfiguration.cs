using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ReferralNoteConfiguration : IEntityTypeConfiguration<ReferralNote>
{
    public void Configure(EntityTypeBuilder<ReferralNote> builder)
    {
        builder.ToTable("cc_ReferralNotes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).IsRequired();
        builder.Property(n => n.TenantId).IsRequired();
        builder.Property(n => n.ReferralId).IsRequired();

        // Visibility model (new columns; nullable during migration window)
        builder.Property(n => n.OwnerOrganizationId);
        builder.Property(n => n.VisibilityScope).HasMaxLength(20).HasDefaultValue("SHARED");

        // Legacy field kept for backward compat during migration
        builder.Property(n => n.IsInternal).IsRequired();

        builder.Property(n => n.NoteType).IsRequired().HasMaxLength(20);
        builder.Property(n => n.Content).IsRequired().HasMaxLength(4000);
        builder.Property(n => n.CreatedAtUtc).IsRequired();
        builder.Property(n => n.UpdatedAtUtc).IsRequired();
        builder.Property(n => n.CreatedByUserId);
        builder.Property(n => n.UpdatedByUserId);

        builder.HasIndex(n => new { n.TenantId, n.ReferralId, n.CreatedAtUtc })
            .HasDatabaseName("IX_ReferralNotes_TenantId_ReferralId_CreatedAtUtc");
        builder.HasIndex(n => new { n.TenantId, n.NoteType })
            .HasDatabaseName("IX_ReferralNotes_TenantId_NoteType");
        builder.HasIndex(n => new { n.ReferralId, n.OwnerOrganizationId, n.VisibilityScope })
            .HasDatabaseName("IX_ReferralNotes_ReferralId_Org_Visibility");

        builder.HasOne(n => n.Referral)
               .WithMany()
               .HasForeignKey(n => n.ReferralId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
