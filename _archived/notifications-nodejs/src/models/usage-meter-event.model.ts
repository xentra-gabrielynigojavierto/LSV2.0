import { DataTypes, Model, Sequelize, Optional } from "sequelize";
import { NotificationChannel, UsageUnit } from "../types";

interface UsageMeterEventAttributes {
  id: string;
  tenantId: string;
  notificationId: string | null;
  notificationAttemptId: string | null;
  channel: NotificationChannel | null;
  provider: string | null;
  providerOwnershipMode: string | null;
  providerConfigId: string | null;
  usageUnit: UsageUnit;
  quantity: number;
  isBillable: boolean;
  providerUnitCost: number | null;
  providerTotalCost: number | null;
  currency: string | null;
  metadataJson: string | null;
  occurredAt: Date;
  createdAt?: Date;
  updatedAt?: Date;
}

interface UsageMeterEventCreationAttributes
  extends Optional<
    UsageMeterEventAttributes,
    | "id"
    | "notificationId"
    | "notificationAttemptId"
    | "channel"
    | "provider"
    | "providerOwnershipMode"
    | "providerConfigId"
    | "isBillable"
    | "providerUnitCost"
    | "providerTotalCost"
    | "currency"
    | "metadataJson"
  > {}

export class UsageMeterEvent extends Model<
  UsageMeterEventAttributes,
  UsageMeterEventCreationAttributes
> {
  declare id: string;
  declare tenantId: string;
  declare notificationId: string | null;
  declare notificationAttemptId: string | null;
  declare channel: NotificationChannel | null;
  declare provider: string | null;
  declare providerOwnershipMode: string | null;
  declare providerConfigId: string | null;
  declare usageUnit: UsageUnit;
  declare quantity: number;
  declare isBillable: boolean;
  declare providerUnitCost: number | null;
  declare providerTotalCost: number | null;
  declare currency: string | null;
  declare metadataJson: string | null;
  declare occurredAt: Date;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initUsageMeterEventModel(sequelize: Sequelize): void {
  UsageMeterEvent.init(
    {
      id: { type: DataTypes.UUID, defaultValue: DataTypes.UUIDV4, primaryKey: true },
      tenantId: { type: DataTypes.UUID, allowNull: false, field: "tenant_id" },
      notificationId: { type: DataTypes.UUID, allowNull: true, defaultValue: null, field: "notification_id" },
      notificationAttemptId: { type: DataTypes.UUID, allowNull: true, defaultValue: null, field: "notification_attempt_id" },
      channel: { type: DataTypes.STRING(50), allowNull: true, defaultValue: null },
      provider: { type: DataTypes.STRING(100), allowNull: true, defaultValue: null },
      providerOwnershipMode: { type: DataTypes.STRING(50), allowNull: true, defaultValue: null, field: "provider_ownership_mode" },
      providerConfigId: { type: DataTypes.UUID, allowNull: true, defaultValue: null, field: "provider_config_id" },
      usageUnit: { type: DataTypes.STRING(100), allowNull: false, field: "usage_unit" },
      quantity: { type: DataTypes.INTEGER, allowNull: false, defaultValue: 1 },
      isBillable: { type: DataTypes.BOOLEAN, allowNull: false, defaultValue: false, field: "is_billable" },
      providerUnitCost: { type: DataTypes.DECIMAL(14, 8), allowNull: true, defaultValue: null, field: "provider_unit_cost" },
      providerTotalCost: { type: DataTypes.DECIMAL(14, 8), allowNull: true, defaultValue: null, field: "provider_total_cost" },
      currency: { type: DataTypes.STRING(10), allowNull: true, defaultValue: null },
      metadataJson: { type: DataTypes.TEXT, allowNull: true, defaultValue: null, field: "metadata_json" },
      occurredAt: { type: DataTypes.DATE, allowNull: false, defaultValue: DataTypes.NOW, field: "occurred_at" },
    },
    {
      sequelize,
      tableName: "usage_meter_events",
      timestamps: true,
      underscored: true,
      indexes: [
        { fields: ["tenant_id"] },
        { fields: ["tenant_id", "usage_unit"] },
        { fields: ["tenant_id", "occurred_at"] },
        { fields: ["notification_id"] },
      ],
    }
  );
}
