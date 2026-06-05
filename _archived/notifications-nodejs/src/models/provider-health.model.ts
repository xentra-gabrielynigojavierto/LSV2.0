import { DataTypes, Model, Sequelize, Optional } from "sequelize";
import { NotificationChannel, ProviderHealthStatus } from "../types";

interface ProviderHealthAttributes {
  id: string;
  tenantId: string | null;
  channel: NotificationChannel;
  provider: string;
  status: ProviderHealthStatus;
  failureCount: number;
  lastCheckedAt: Date | null;
  lastFailureAt: Date | null;
  createdAt?: Date;
  updatedAt?: Date;
}

interface ProviderHealthCreationAttributes
  extends Optional<
    ProviderHealthAttributes,
    "id" | "tenantId" | "failureCount" | "lastCheckedAt" | "lastFailureAt"
  > {}

export class ProviderHealth extends Model<
  ProviderHealthAttributes,
  ProviderHealthCreationAttributes
> {
  declare id: string;
  declare tenantId: string | null;
  declare channel: NotificationChannel;
  declare provider: string;
  declare status: ProviderHealthStatus;
  declare failureCount: number;
  declare lastCheckedAt: Date | null;
  declare lastFailureAt: Date | null;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initProviderHealthModel(sequelize: Sequelize): void {
  ProviderHealth.init(
    {
      id: {
        type: DataTypes.UUID,
        defaultValue: DataTypes.UUIDV4,
        primaryKey: true,
      },
      tenantId: {
        type: DataTypes.UUID,
        allowNull: true,
        defaultValue: null,
        field: "tenant_id",
      },
      channel: {
        type: DataTypes.ENUM("email", "sms", "push", "in-app"),
        allowNull: false,
      },
      provider: {
        type: DataTypes.STRING(100),
        allowNull: false,
      },
      status: {
        type: DataTypes.ENUM("healthy", "degraded", "down"),
        allowNull: false,
        defaultValue: "healthy",
      },
      failureCount: {
        type: DataTypes.INTEGER,
        allowNull: false,
        defaultValue: 0,
        field: "failure_count",
      },
      lastCheckedAt: {
        type: DataTypes.DATE,
        allowNull: true,
        defaultValue: null,
        field: "last_checked_at",
      },
      lastFailureAt: {
        type: DataTypes.DATE,
        allowNull: true,
        defaultValue: null,
        field: "last_failure_at",
      },
    },
    {
      sequelize,
      tableName: "provider_health",
      timestamps: true,
      underscored: true,
    }
  );
}
