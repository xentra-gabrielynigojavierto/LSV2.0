import { ProviderHealth } from "../models/provider-health.model";
import { NotificationChannel, ProviderHealthStatus } from "../types";

export class ProviderHealthRepository {
  async findByChannelAndProvider(
    channel: NotificationChannel,
    provider: string,
    tenantId?: string
  ): Promise<ProviderHealth | null> {
    return ProviderHealth.findOne({
      where: { channel, provider, tenantId: tenantId ?? null },
    });
  }

  async upsertHealth(
    channel: NotificationChannel,
    provider: string,
    status: ProviderHealthStatus,
    tenantId?: string
  ): Promise<void> {
    const existing = await this.findByChannelAndProvider(channel, provider, tenantId);

    if (existing) {
      await ProviderHealth.update(
        { status, lastCheckedAt: new Date() },
        { where: { id: existing.id } }
      );
    } else {
      await ProviderHealth.create({
        tenantId: tenantId ?? null,
        channel,
        provider,
        status,
        lastCheckedAt: new Date(),
      });
    }
  }
}
