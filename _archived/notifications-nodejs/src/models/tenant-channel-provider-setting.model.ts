import { DataTypes, Model, Sequelize, Optional } from "sequelize";
import { NotificationChannel, TenantChannelProviderMode } from "../types";

interface TenantChannelProviderSettingAttributes {
  id: string;
  tenantId: string;
  channel: NotificationChannel;
  providerMode: TenantChannelProviderMode;
  primaryTenantProviderConfigId: string | null;
  fallbackTenantProviderConfigId: string | null;
  allowPlatformFallback: boolean;
  allowAutomaticFailover: boolean;
  status: "active" | "inactive";
  createdAt?: Date;
  updatedAt?: Date;
}

interface TenantChannelProviderSettingCreationAttributes
  extends Optional<
    TenantChannelProviderSettingAttributes,
    | "id"
    | "primaryTenantProviderConfigId"
    | "fallbackTenantProviderConfigId"
    | "allowPlatformFallback"
    | "allowAutomaticFailover"
    | "status"
  > {}

export class TenantChannelProviderSetting extends Model<
  TenantChannelProviderSettingAttributes,
  TenantChannelProviderSettingCreationAttributes
> {
  declare id: string;
  declare tenantId: string;
  declare channel: NotificationChannel;
  declare providerMode: TenantChannelProviderMode;
  declare primaryTenantProviderConfigId: string | null;
  declare fallbackTenantProviderConfigId: string | null;
  declare allowPlatformFallback: boolean;
  declare allowAutomaticFailover: boolean;
  declare status: "active" | "inactive";
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initTenantChannelProviderSettingModel(sequelize: Sequelize): void {
  TenantChannelProviderSetting.init(
    {
      id: { type: DataTypes.UUID, defaultValue: DataTypes.UUIDV4, primaryKey: true },
      tenantId: { type: DataTypes.UUID, allowNull: false, field: "tenant_id" },
      channel: { type: DataTypes.ENUM("email", "sms", "push", "in-app"), allowNull: false },
      providerMode: {
        type: DataTypes.ENUM("platform_managed", "tenant_managed"),
        allowNull: false,
        defaultValue: "platform_managed",
        field: "provider_mode",
      },
      primaryTenantProviderConfigId: {
        type: DataTypes.UUID,
        allowNull: true,
        defaultValue: null,
        field: "primary_tenant_provider_config_id",
      },
      fallbackTenantProviderConfigId: {
        type: DataTypes.UUID,
        allowNull: true,
        defaultValue: null,
        field: "fallback_tenant_provider_config_id",
      },
      allowPlatformFallback: {
        type: DataTypes.BOOLEAN,
        allowNull: false,
        defaultValue: true,
        field: "allow_platform_fallback",
      },
      allowAutomaticFailover: {
        type: DataTypes.BOOLEAN,
        allowNull: false,
        defaultValue: true,
        field: "allow_automatic_failover",
      },
      status: {
        type: DataTypes.ENUM("active", "inactive"),
        allowNull: false,
        defaultValue: "active",
      },
    },
    {
      sequelize,
      tableName: "tenant_channel_provider_settings",
      timestamps: true,
      underscored: true,
      indexes: [
        {
          unique: true,
          fields: ["tenant_id", "channel"],
          name: "uq_tenant_channel_provider_setting",
        },
      ],
    }
  );
}
