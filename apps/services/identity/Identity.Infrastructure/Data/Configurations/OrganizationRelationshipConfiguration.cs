using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class OrganizationRelationshipConfiguration : IEntityTypeConfiguration<OrganizationRelationship>
{
    public void Configure(EntityTypeBuilder<OrganizationRelationship> builder)
    {
        builder.ToTable("idt_OrganizationRelationships");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.SourceOrganizationId).IsRequired();
        builder.Property(r => r.TargetOrganizationId).IsRequired();
        builder.Property(r => r.RelationshipTypeId).IsRequired();
        builder.Property(r => r.ProductId);
        builder.Property(r => r.IsActive).IsRequired();
        builder.Property(r => r.EstablishedAtUtc).IsRequired();
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.UpdatedAtUtc).IsRequired();
        builder.Property(r => r.CreatedByUserId);

        builder.HasIndex(r => new { r.TenantId, r.SourceOrganizationId, r.TargetOrganizationId, r.RelationshipTypeId })
            .IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.SourceOrganizationId });
        builder.HasIndex(r => new { r.TenantId, r.TargetOrganizationId });
        builder.HasIndex(r => r.RelationshipTypeId);

        builder.HasOne(r => r.SourceOrganization)
            .WithMany(o => o.OutgoingRelationships)
            .HasForeignKey(r => r.SourceOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.TargetOrganization)
            .WithMany(o => o.IncomingRelationships)
            .HasForeignKey(r => r.TargetOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.RelationshipType)
            .WithMany(rt => rt.Relationships)
            .HasForeignKey(r => r.RelationshipTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Product)
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
