import { DataTypes, Model, Sequelize, Optional } from "sequelize";
import { NotificationChannel, TenantProviderValidationStatus, TenantProviderHealthStatus, TenantProviderConfigStatus, TenantChannelProviderMode } from "../types";

interface TenantProviderConfigAttributes {
  id: string;
  tenantId: string | null;
  channel: NotificationChannel;
  providerType: string;
  ownershipMode: TenantChannelProviderMode;
  displayName: string;
  isActive: boolean;
  isPrimary: boolean;
  isFallback: boolean;
  allowAutomaticFailover: boolean;
  allowPlatformFallback: boolean;
  status: TenantProviderConfigStatus;
  endpointConfigJson: string | null;
  senderConfigJson: string | null;
  webhookConfigJson: string | null;
  credentialReference: string | null;
  lastValidatedAt: Date | null;
  validationStatus: TenantProviderValidationStatus;
  healthStatus: TenantProviderHealthStatus;
  createdAt?: Date;
  updatedAt?: Date;
}

interface TenantProviderConfigCreationAttributes
  extends Optional<
    TenantProviderConfigAttributes,
    | "id" | "tenantId" | "ownershipMode" | "isActive" | "isPrimary" | "isFallback"
    | "allowAutomaticFailover" | "allowPlatformFallback" | "status"
    | "endpointConfigJson" | "senderConfigJson" | "webhookConfigJson"
    | "credentialReference" | "lastValidatedAt" | "validationStatus" | "healthStatus"
  > {}

export class TenantProviderConfig extends Model<TenantProviderConfigAttributes, TenantProviderConfigCreationAttributes> {
  declare id: string;
  declare tenantId: string | null;
  declare channel: NotificationChannel;
  declare providerType: string;
  declare ownershipMode: TenantChannelProviderMode;
  declare displayName: string;
  declare isActive: boolean;
  declare isPrimary: boolean;
  declare isFallback: boolean;
  declare allowAutomaticFailover: boolean;
  declare allowPlatformFallback: boolean;
  declare status: TenantProviderConfigStatus;
  declare endpointConfigJson: string | null;
  declare senderConfigJson: string | null;
  declare webhookConfigJson: string | null;
  declare credentialReference: string | null;
  declare lastValidatedAt: Date | null;
  declare validationStatus: TenantProviderValidationStatus;
  declare healthStatus: TenantProviderHealthStatus;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initTenantProviderConfigModel(sequelize: Sequelize): void {
  TenantProviderConfig.init(
    {
      id: { type: DataTypes.UUID, defaultValue: DataTypes.UUIDV4, primaryKey: true },
      tenantId: { type: DataTypes.UUID, allowNull: true, defaultValue: null, field: "tenant_id" },
      channel: { type: DataTypes.ENUM("email", "sms", "push", "in-app"), allowNull: false },
      providerType: { type: DataTypes.STRING(100), allowNull: false, field: "provider_type" },
      ownershipMode: { type: DataTypes.ENUM("platform_managed", "tenant_managed"), allowNull: false, defaultValue: "tenant_managed", field: "ownership_mode" },
      displayName: { type: DataTypes.STRING(200), allowNull: false, field: "display_name" },
      isActive: { type: DataTypes.BOOLEAN, allowNull: false, defaultValue: false, field: "is_active" },
      isPrimary: { type: DataTypes.BOOLEAN, allowNull: false, defaultValue: false, field: "is_primary" },
      isFallback: { type: DataTypes.BOOLEAN, allowNull: false, defaultValue: false, field: "is_fallback" },
      allowAutomaticFailover: { type: DataTypes.BOOLEAN, allowNull: false, defaultValue: true, field: "allow_automatic_failover" },
      allowPlatformFallback: { type: DataTypes.BOOLEAN, allowNull: false, defaultValue: true, field: "allow_platform_fallback" },
      status: { type: DataTypes.ENUM("active", "inactive"), allowNull: false, defaultValue: "active" },
      endpointConfigJson: { type: DataTypes.TEXT, allowNull: true, defaultValue: null, field: "endpoint_config_json" },
      senderConfigJson: { type: DataTypes.TEXT, allowNull: true, defaultValue: null, field: "sender_config_json" },
      webhookConfigJson: { type: DataTypes.TEXT, allowNull: true, defaultValue: null, field: "webhook_config_json" },
      credentialReference: { type: DataTypes.TEXT, allowNull: true, defaultValue: null, field: "credential_reference" },
      lastValidatedAt: { type: DataTypes.DATE, allowNull: true, defaultValue: null, field: "last_validated_at" },
      validationStatus: { type: DataTypes.ENUM("not_validated", "valid", "invalid"), allowNull: false, defaultValue: "not_validated", field: "validation_status" },
      healthStatus: { type: DataTypes.ENUM("healthy", "degraded", "down", "unknown"), allowNull: false, defaultValue: "unknown", field: "health_status" },
    },
    { sequelize, tableName: "tenant_provider_configs", timestamps: true, underscored: true }
  );
}
