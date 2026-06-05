using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenant.Domain;

namespace Tenant.Infrastructure.Data.Configurations;

public class TenantSettingConfiguration : IEntityTypeConfiguration<TenantSetting>
{
    public void Configure(EntityTypeBuilder<TenantSetting> builder)
    {
        builder.ToTable("tenant_Settings");

        builder.HasKey(s => s.Id);

        // ── Tenant FK ─────────────────────────────────────────────────────────

        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("IX_tenant_Settings_TenantId");

        // ── Setting key ───────────────────────────────────────────────────────

        builder.Property(s => s.SettingKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(s => new { s.TenantId, s.SettingKey })
            .HasDatabaseName("IX_tenant_Settings_TenantId_SettingKey");

        // ── Setting value ─────────────────────────────────────────────────────

        builder.Property(s => s.SettingValue)
            .IsRequired()
            .HasMaxLength(4000);

        // ── ValueType stored as string ────────────────────────────────────────

        builder.Property(s => s.ValueType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // ── Product key scope ─────────────────────────────────────────────────

        builder.Property(s => s.ProductKey)
            .HasMaxLength(100);

        builder.HasIndex(s => new { s.TenantId, s.ProductKey })
            .HasDatabaseName("IX_tenant_Settings_TenantId_ProductKey");

        // ── Timestamps ────────────────────────────────────────────────────────

        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();

        // ── Navigation ────────────────────────────────────────────────────────

        builder.HasOne(s => s.Tenant)
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
