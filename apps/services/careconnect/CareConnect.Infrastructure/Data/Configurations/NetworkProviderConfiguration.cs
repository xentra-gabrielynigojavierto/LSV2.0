using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

// CC2-INT-B06
public class NetworkProviderConfiguration : IEntityTypeConfiguration<NetworkProvider>
{
    public void Configure(EntityTypeBuilder<NetworkProvider> builder)
    {
        builder.ToTable("cc_NetworkProviders");

        builder.HasKey(np => np.Id);

        builder.Property(np => np.Id).IsRequired();
        builder.Property(np => np.TenantId).IsRequired();
        builder.Property(np => np.ProviderNetworkId).IsRequired();
        builder.Property(np => np.ProviderId).IsRequired();
        builder.Property(np => np.CreatedAtUtc).IsRequired();
        builder.Property(np => np.UpdatedAtUtc).IsRequired();
        builder.Property(np => np.CreatedByUserId);
        builder.Property(np => np.UpdatedByUserId);

        builder.HasIndex(np => new { np.ProviderNetworkId, np.ProviderId }).IsUnique();

        // BLK-PERF-01: GetNetworkProvidersAsync filters on (TenantId, ProviderNetworkId).
        // The existing unique index on (ProviderNetworkId, ProviderId) does not lead on TenantId,
        // so tenant-scoped network provider list queries would scan all rows for that network.
        builder.HasIndex(np => new { np.TenantId, np.ProviderNetworkId })
               .HasDatabaseName("IX_NetworkProviders_TenantId_ProviderNetworkId");

        builder.HasOne(np => np.Provider)
               .WithMany()
               .HasForeignKey(np => np.ProviderId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
