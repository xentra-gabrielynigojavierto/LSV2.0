using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class TenantBillingPlanConfiguration : IEntityTypeConfiguration<TenantBillingPlan>
{
    public void Configure(EntityTypeBuilder<TenantBillingPlan> builder)
    {
        builder.ToTable("ntf_TenantBillingPlans");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PlanName).HasMaxLength(200);
        builder.Property(e => e.BillingMode).HasMaxLength(30);
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("active");
        builder.Property(e => e.MonthlyFlatRate).HasColumnType("decimal(10,2)");
        builder.Property(e => e.Currency).HasMaxLength(10).HasDefaultValue("USD");
    }
}

public class TenantBillingRateConfiguration : IEntityTypeConfiguration<TenantBillingRate>
{
    public void Configure(EntityTypeBuilder<TenantBillingRate> builder)
    {
        builder.ToTable("ntf_TenantBillingRates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.UsageUnit).HasMaxLength(100);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.ProviderOwnershipMode).HasMaxLength(20);
        builder.Property(e => e.UnitPrice).HasColumnType("decimal(10,6)");
        builder.Property(e => e.Currency).HasMaxLength(10).HasDefaultValue("USD");
        builder.Property(e => e.IsBillable).HasDefaultValue(true);
    }
}

public class TenantRateLimitPolicyConfiguration : IEntityTypeConfiguration<TenantRateLimitPolicy>
{
    public void Configure(EntityTypeBuilder<TenantRateLimitPolicy> builder)
    {
        builder.ToTable("ntf_TenantRateLimitPolicies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("active");
    }
}

public class TenantContactPolicyConfiguration : IEntityTypeConfiguration<TenantContactPolicy>
{
    public void Configure(EntityTypeBuilder<TenantContactPolicy> builder)
    {
        builder.ToTable("ntf_TenantContactPolicies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.BlockSuppressedContacts).HasDefaultValue(true);
        builder.Property(e => e.BlockUnsubscribedContacts).HasDefaultValue(true);
        builder.Property(e => e.BlockComplainedContacts).HasDefaultValue(true);
        builder.Property(e => e.BlockBouncedContacts).HasDefaultValue(false);
        builder.Property(e => e.BlockInvalidContacts).HasDefaultValue(false);
        builder.Property(e => e.BlockCarrierRejectedContacts).HasDefaultValue(false);
        builder.Property(e => e.AllowManualOverride).HasDefaultValue(false);
        builder.Property(e => e.BlockUnknownSmsPreference).HasDefaultValue(true);
    }
}

public class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("ntf_TenantBrandings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ProductType).HasMaxLength(50);
        builder.Property(e => e.BrandName).HasMaxLength(200);
        builder.Property(e => e.LogoUrl).HasMaxLength(2000);
        builder.Property(e => e.PrimaryColor).HasMaxLength(50);
        builder.Property(e => e.SecondaryColor).HasMaxLength(50);
        builder.Property(e => e.AccentColor).HasMaxLength(50);
        builder.Property(e => e.TextColor).HasMaxLength(50);
        builder.Property(e => e.BackgroundColor).HasMaxLength(50);
        builder.Property(e => e.ButtonRadius).HasMaxLength(30);
        builder.Property(e => e.FontFamily).HasMaxLength(200);
        builder.Property(e => e.SupportEmail).HasMaxLength(255);
        builder.Property(e => e.SupportPhone).HasMaxLength(50);
        builder.Property(e => e.WebsiteUrl).HasMaxLength(2000);
        builder.Property(e => e.EmailHeaderHtml).HasColumnType("text");
        builder.Property(e => e.EmailFooterHtml).HasColumnType("text");

        builder.HasIndex(e => new { e.TenantId, e.ProductType })
            .HasDatabaseName("UX_TenantBrandings_TenantId_ProductType")
            .IsUnique();
    }
}

public class UsageMeterEventConfiguration : IEntityTypeConfiguration<UsageMeterEvent>
{
    public void Configure(EntityTypeBuilder<UsageMeterEvent> builder)
    {
        builder.ToTable("ntf_UsageMeterEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.Provider).HasMaxLength(100);
        builder.Property(e => e.ProviderOwnershipMode).HasMaxLength(20);
        builder.Property(e => e.UsageUnit).HasMaxLength(100);
        builder.Property(e => e.Quantity).HasDefaultValue(1);
        builder.Property(e => e.ProviderUnitCost).HasColumnType("decimal(10,6)");
        builder.Property(e => e.ProviderTotalCost).HasColumnType("decimal(10,6)");
        builder.Property(e => e.Currency).HasMaxLength(10);
        builder.Property(e => e.MetadataJson).HasColumnType("text");

        builder.HasIndex(e => new { e.TenantId, e.UsageUnit, e.OccurredAt })
            .HasDatabaseName("IX_UsageMeterEvents_TenantId_UsageUnit_OccurredAt");
    }
}
