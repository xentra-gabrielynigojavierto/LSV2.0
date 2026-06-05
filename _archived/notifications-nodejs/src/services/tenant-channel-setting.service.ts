import { TenantChannelProviderSettingRepository } from "../repositories/tenant-channel-provider-setting.repository";
import { TenantProviderConfigRepository } from "../repositories/tenant-provider-config.repository";
import { auditClient } from "../integrations/audit/audit.client";
import { NotificationChannel, NotificationChannels, TenantChannelProviderMode } from "../types";

const settingRepo = new TenantChannelProviderSettingRepository();
const configRepo = new TenantProviderConfigRepository();

export async function listTenantChannelSettings(tenantId: string | undefined) {
  return settingRepo.findAllByTenant(tenantId);
}

export async function getTenantChannelSetting(tenantId: string | undefined, channel: NotificationChannel) {
  return settingRepo.findByTenantAndChannel(tenantId, channel);
}

export interface UpdateChannelSettingInput {
  providerMode?: TenantChannelProviderMode;
  primaryTenantProviderConfigId?: string | null;
  fallbackTenantProviderConfigId?: string | null;
  allowPlatformFallback?: boolean;
  allowAutomaticFailover?: boolean;
}

export async function updateTenantChannelSetting(
  tenantId: string,
  channel: NotificationChannel,
  updates: UpdateChannelSettingInput
): Promise<{ data: object; errors: string[] }> {
  if (!NotificationChannels.includes(channel)) {
    throw Object.assign(new Error(`Invalid channel: ${channel}`), { statusCode: 400 });
  }

  const errors: string[] = [];

  // Validate referenced provider configs
  const primaryId = updates.primaryTenantProviderConfigId;
  const fallbackId = updates.fallbackTenantProviderConfigId;

  if (primaryId) {
    const primary = await configRepo.findByIdAndTenant(primaryId, tenantId);
    if (!primary) errors.push("primaryTenantProviderConfigId does not exist or does not belong to this tenant");
    else if (primary.channel !== channel) errors.push("primaryTenantProviderConfigId must match the channel being configured");
  }

  if (fallbackId) {
    const fallback = await configRepo.findByIdAndTenant(fallbackId, tenantId);
    if (!fallback) errors.push("fallbackTenantProviderConfigId does not exist or does not belong to this tenant");
    else if (fallback.channel !== channel) errors.push("fallbackTenantProviderConfigId must match the channel being configured");
  }

  if (primaryId && fallbackId && primaryId === fallbackId) {
    errors.push("fallbackTenantProviderConfigId cannot be the same as primaryTenantProviderConfigId");
  }

  if (errors.length > 0) {
    return { data: {}, errors };
  }

  const setting = await settingRepo.upsert({ tenantId, channel, ...updates });

  await auditClient.publishEvent({
    eventType: "tenant_channel_setting.updated",
    tenantId,
    channel,
    metadata: { channel, updates },
  });

  return { data: setting, errors: [] };
}
