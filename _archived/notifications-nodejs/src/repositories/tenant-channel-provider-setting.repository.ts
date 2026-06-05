import { TenantChannelProviderSetting } from "../models/tenant-channel-provider-setting.model";
import { NotificationChannel, TenantChannelProviderMode } from "../types";

export class TenantChannelProviderSettingRepository {
  async findByTenantAndChannel(
    tenantId: string | undefined,
    channel: NotificationChannel
  ): Promise<TenantChannelProviderSetting | null> {
    const where: Record<string, unknown> = { channel };
    if (tenantId !== undefined) where["tenantId"] = tenantId;
    return TenantChannelProviderSetting.findOne({ where });
  }

  async findAllByTenant(tenantId: string | undefined): Promise<TenantChannelProviderSetting[]> {
    const where: Record<string, unknown> = {};
    if (tenantId !== undefined) where["tenantId"] = tenantId;
    return TenantChannelProviderSetting.findAll({ where });
  }

  async upsert(input: {
    tenantId: string;
    channel: NotificationChannel;
    providerMode?: TenantChannelProviderMode;
    primaryTenantProviderConfigId?: string | null;
    fallbackTenantProviderConfigId?: string | null;
    allowPlatformFallback?: boolean;
    allowAutomaticFailover?: boolean;
  }): Promise<TenantChannelProviderSetting> {
    const existing = await this.findByTenantAndChannel(input.tenantId, input.channel);

    if (existing) {
      const updates: Record<string, unknown> = {};
      if (input.providerMode !== undefined) updates["providerMode"] = input.providerMode;
      if (input.primaryTenantProviderConfigId !== undefined) updates["primaryTenantProviderConfigId"] = input.primaryTenantProviderConfigId;
      if (input.fallbackTenantProviderConfigId !== undefined) updates["fallbackTenantProviderConfigId"] = input.fallbackTenantProviderConfigId;
      if (input.allowPlatformFallback !== undefined) updates["allowPlatformFallback"] = input.allowPlatformFallback;
      if (input.allowAutomaticFailover !== undefined) updates["allowAutomaticFailover"] = input.allowAutomaticFailover;

      await TenantChannelProviderSetting.update(updates, {
        where: { tenantId: input.tenantId, channel: input.channel },
      });

      return (await this.findByTenantAndChannel(input.tenantId, input.channel))!;
    }

    return TenantChannelProviderSetting.create({
      tenantId: input.tenantId,
      channel: input.channel,
      providerMode: input.providerMode ?? "platform_managed",
      primaryTenantProviderConfigId: input.primaryTenantProviderConfigId ?? null,
      fallbackTenantProviderConfigId: input.fallbackTenantProviderConfigId ?? null,
      allowPlatformFallback: input.allowPlatformFallback ?? true,
      allowAutomaticFailover: input.allowAutomaticFailover ?? true,
    });
  }
}
