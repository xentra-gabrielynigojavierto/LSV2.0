import { NotificationChannel, ProviderHealthStatus, FailureCategory, TenantChannelProviderMode } from "../types";
import { logger } from "../shared/logger";
import { ProviderHealth } from "../models/provider-health.model";
import { TenantChannelProviderSettingRepository } from "../repositories/tenant-channel-provider-setting.repository";
import { TenantProviderConfigRepository } from "../repositories/tenant-provider-config.repository";
import { AppConfig } from "../config";

export interface ResolvedProvider {
  providerType: string;
  ownershipMode: "platform_managed" | "tenant_managed";
  isFallback: boolean;
  platformFallbackUsed: boolean;
  providerConfigId: string | null;
}

const RETRYABLE_CATEGORIES: FailureCategory[] = [
  "retryable_provider_failure",
  "provider_unavailable",
];

export function isFailoverEligible(category: FailureCategory): boolean {
  return RETRYABLE_CATEGORIES.includes(category);
}

export class ProviderRoutingService {
  private config: AppConfig;
  private channelSettingRepo: TenantChannelProviderSettingRepository;
  private providerConfigRepo: TenantProviderConfigRepository;

  constructor(config: AppConfig) {
    this.config = config;
    this.channelSettingRepo = new TenantChannelProviderSettingRepository();
    this.providerConfigRepo = new TenantProviderConfigRepository();
  }

  private platformDefault(channel: NotificationChannel): string {
    if (channel === "email") return this.config.providers.defaultEmailProvider || "sendgrid";
    if (channel === "sms") return this.config.providers.defaultSmsProvider || "twilio";
    return "";
  }

  private async getPlatformProviderHealth(
    channel: NotificationChannel,
    provider: string
  ): Promise<ProviderHealthStatus> {
    try {
      const record = await ProviderHealth.findOne({ where: { channel, provider, tenantId: null } });
      return (record?.status as ProviderHealthStatus) ?? "healthy";
    } catch {
      return "healthy";
    }
  }

  private async getTenantProviderHealth(
    tenantId: string,
    channel: NotificationChannel,
    provider: string
  ): Promise<ProviderHealthStatus> {
    try {
      const record = await ProviderHealth.findOne({ where: { channel, provider, tenantId } });
      return (record?.status as ProviderHealthStatus) ?? "healthy";
    } catch {
      return "healthy";
    }
  }

  /**
   * Full BYOP routing decision tree:
   *
   * 1. Load TenantChannelProviderSetting for (tenantId, channel)
   * 2. platform_managed (or no setting) → use platform default
   * 3. tenant_managed:
   *    a. Primary tenant config (from setting or isPrimary flag) → if healthy, use it
   *    b. allowAutomaticFailover + fallback tenant config → if healthy, use it
   *    c. allowPlatformFallback → use platform provider
   *    d. Nothing available → null
   */
  async resolveProvider(
    tenantId: string,
    channel: NotificationChannel
  ): Promise<ResolvedProvider | null> {
    const channelSetting = await this.channelSettingRepo.findByTenantAndChannel(tenantId, channel);
    const providerMode: TenantChannelProviderMode = channelSetting?.providerMode ?? "platform_managed";

    if (providerMode === "platform_managed" || !channelSetting) {
      logger.debug("Routing via platform_managed mode", { tenantId, channel });
      return this.resolvePlatformDefault(channel, false);
    }

    logger.debug("Routing via tenant_managed mode", { tenantId, channel });

    // Resolve primary tenant config
    let primaryConfig = channelSetting.primaryTenantProviderConfigId
      ? await this.providerConfigRepo.findById(channelSetting.primaryTenantProviderConfigId)
      : await this.providerConfigRepo.findPrimaryForChannel(tenantId, channel);

    if (
      primaryConfig &&
      primaryConfig.isActive &&
      primaryConfig.status === "active" &&
      primaryConfig.validationStatus === "valid"
    ) {
      const health = await this.getTenantProviderHealth(tenantId, channel, primaryConfig.providerType);
      if (health !== "down") {
        logger.debug("Routing to tenant primary provider", {
          tenantId, channel, provider: primaryConfig.providerType, configId: primaryConfig.id,
        });
        return {
          providerType: primaryConfig.providerType,
          ownershipMode: "tenant_managed",
          isFallback: false,
          platformFallbackUsed: false,
          providerConfigId: primaryConfig.id,
        };
      }
      logger.warn("Tenant primary provider is down", { tenantId, channel, provider: primaryConfig.providerType });
    }

    // Try tenant fallback
    if (channelSetting.allowAutomaticFailover) {
      let fallbackConfig = channelSetting.fallbackTenantProviderConfigId
        ? await this.providerConfigRepo.findById(channelSetting.fallbackTenantProviderConfigId)
        : await this.providerConfigRepo.findFallbackForChannel(tenantId, channel);

      if (
        fallbackConfig &&
        fallbackConfig.id !== primaryConfig?.id &&
        fallbackConfig.isActive &&
        fallbackConfig.status === "active" &&
        fallbackConfig.validationStatus === "valid"
      ) {
        const health = await this.getTenantProviderHealth(tenantId, channel, fallbackConfig.providerType);
        if (health !== "down") {
          logger.info("Routing to tenant fallback provider", {
            tenantId, channel, provider: fallbackConfig.providerType, configId: fallbackConfig.id,
          });
          return {
            providerType: fallbackConfig.providerType,
            ownershipMode: "tenant_managed",
            isFallback: true,
            platformFallbackUsed: false,
            providerConfigId: fallbackConfig.id,
          };
        }
        logger.warn("Tenant fallback provider is also down", { tenantId, channel, provider: fallbackConfig.providerType });
      }
    }

    // Try platform fallback
    if (channelSetting.allowPlatformFallback) {
      const fallback = await this.resolvePlatformDefault(channel, true);
      if (fallback) {
        logger.info("Tenant provider(s) unavailable — falling back to platform", { tenantId, channel });
        return fallback;
      }
    }

    logger.warn("No eligible provider found for tenant", { tenantId, channel });
    return null;
  }

  private async resolvePlatformDefault(
    channel: NotificationChannel,
    isPlatformFallback: boolean
  ): Promise<ResolvedProvider | null> {
    const platformProvider = this.platformDefault(channel);
    if (!platformProvider) {
      logger.warn("No platform default provider configured", { channel });
      return null;
    }

    const health = await this.getPlatformProviderHealth(channel, platformProvider);
    if (health === "down") {
      logger.warn("Platform default provider is down", { channel, platformProvider });
      return null;
    }

    return {
      providerType: platformProvider,
      ownershipMode: "platform_managed",
      isFallback: isPlatformFallback,
      platformFallbackUsed: isPlatformFallback,
      providerConfigId: null,
    };
  }

  async getNextProvider(
    tenantId: string,
    channel: NotificationChannel,
    excludeProviders: string[]
  ): Promise<ResolvedProvider | null> {
    const platformProvider = this.platformDefault(channel);

    if (!platformProvider || excludeProviders.includes(platformProvider)) {
      logger.debug("No failover provider available", { channel, excludeProviders });
      return null;
    }

    const health = await this.getPlatformProviderHealth(channel, platformProvider);
    if (health === "down") {
      logger.warn("Failover provider is also down", { channel, platformProvider });
      return null;
    }

    logger.info("Resolved failover provider", { channel, provider: platformProvider });
    return {
      providerType: platformProvider,
      ownershipMode: "platform_managed",
      isFallback: true,
      platformFallbackUsed: true,
      providerConfigId: null,
    };
  }

  async recordProviderHealth(
    provider: string,
    channel: NotificationChannel,
    status: ProviderHealthStatus,
    isFailure: boolean = false,
    tenantId: string | null = null
  ): Promise<void> {
    try {
      const existing = await ProviderHealth.findOne({ where: { channel, provider, tenantId } });
      const now = new Date();

      if (existing) {
        const newFailureCount = isFailure ? (existing.failureCount ?? 0) + 1 : 0;
        let newStatus = status;

        if (newFailureCount >= 5) newStatus = "down";
        else if (newFailureCount >= 3) newStatus = "degraded";
        else if (!isFailure) newStatus = "healthy";

        await ProviderHealth.update(
          {
            status: newStatus,
            failureCount: newFailureCount,
            lastCheckedAt: now,
            ...(isFailure ? { lastFailureAt: now } : {}),
          },
          { where: { id: existing.id } }
        );
      } else {
        await ProviderHealth.create({
          tenantId,
          channel,
          provider,
          status,
          failureCount: isFailure ? 1 : 0,
          lastCheckedAt: now,
          lastFailureAt: isFailure ? now : null,
        });
      }
    } catch (err) {
      logger.error("Failed to record provider health", { error: String(err), provider, channel });
    }
  }
}
