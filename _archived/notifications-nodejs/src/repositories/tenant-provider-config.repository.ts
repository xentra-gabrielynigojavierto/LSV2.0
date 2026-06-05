import { TenantProviderConfig } from "../models/tenant-provider-config.model";
import { NotificationChannel, TenantProviderValidationStatus, TenantProviderHealthStatus } from "../types";

export class TenantProviderConfigRepository {
  async findById(id: string): Promise<TenantProviderConfig | null> {
    return TenantProviderConfig.findByPk(id);
  }

  async findByIdAndTenant(id: string, tenantId: string | undefined): Promise<TenantProviderConfig | null> {
    const where: Record<string, unknown> = { id };
    if (tenantId !== undefined) where["tenantId"] = tenantId;
    return TenantProviderConfig.findOne({ where });
  }

  async findByTenant(tenantId: string | undefined, channel?: NotificationChannel): Promise<TenantProviderConfig[]> {
    const where: Record<string, unknown> = {};
    if (tenantId !== undefined) where["tenantId"] = tenantId;
    if (channel) where["channel"] = channel;
    return TenantProviderConfig.findAll({ where, order: [["createdAt", "DESC"]] });
  }

  async findPrimaryForChannel(tenantId: string, channel: NotificationChannel): Promise<TenantProviderConfig | null> {
    return TenantProviderConfig.findOne({ where: { tenantId, channel, isPrimary: true, isActive: true, status: "active" } });
  }

  async findFallbackForChannel(tenantId: string, channel: NotificationChannel): Promise<TenantProviderConfig | null> {
    return TenantProviderConfig.findOne({ where: { tenantId, channel, isFallback: true, isActive: true, status: "active" } });
  }

  async findActiveByIds(ids: string[], tenantId: string): Promise<TenantProviderConfig[]> {
    return TenantProviderConfig.findAll({ where: { id: ids, tenantId, isActive: true, status: "active" } });
  }

  async create(input: {
    tenantId?: string | null;
    channel: NotificationChannel;
    providerType: string;
    displayName: string;
    endpointConfigJson?: string | null;
    senderConfigJson?: string | null;
    webhookConfigJson?: string | null;
    credentialReference?: string | null;
    allowPlatformFallback?: boolean;
    allowAutomaticFailover?: boolean;
  }): Promise<TenantProviderConfig> {
    return TenantProviderConfig.create({
      ...input,
      tenantId: input.tenantId ?? null,
      // Platform-level configs (no tenantId) are always platform_managed
      ownershipMode: input.tenantId ? "tenant_managed" : "platform_managed",
      isActive: false,
      isPrimary: false,
      isFallback: false,
      status: "active",
      validationStatus: "not_validated",
      healthStatus: "unknown",
    });
  }

  async update(
    id: string,
    updates: Partial<{
      displayName: string;
      isActive: boolean;
      isPrimary: boolean;
      isFallback: boolean;
      allowAutomaticFailover: boolean;
      allowPlatformFallback: boolean;
      status: "active" | "inactive";
      endpointConfigJson: string | null;
      senderConfigJson: string | null;
      webhookConfigJson: string | null;
      credentialReference: string | null;
      validationStatus: TenantProviderValidationStatus;
      healthStatus: TenantProviderHealthStatus;
      lastValidatedAt: Date | null;
    }>
  ): Promise<void> {
    await TenantProviderConfig.update(updates, { where: { id } });
  }

  async deleteById(id: string, tenantId: string | undefined): Promise<boolean> {
    const where: Record<string, unknown> = { id };
    if (tenantId !== undefined) where["tenantId"] = tenantId;
    const count = await TenantProviderConfig.destroy({ where });
    return count > 0;
  }
}
