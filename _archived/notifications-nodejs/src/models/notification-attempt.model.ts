import { DataTypes, Model, Sequelize, Optional } from "sequelize";
import { FailureCategory } from "../types";

export type AttemptStatus = "created" | "sending" | "sent" | "failed";

interface NotificationAttemptAttributes {
  id: string;
  tenantId: string | null;
  notificationId: string;
  attemptNumber: number;
  provider: string;
  status: AttemptStatus;
  failoverTriggered: boolean;
  providerMessageId: string | null;
  failureCategory: FailureCategory | null;
  errorMessage: string | null;
  startedAt: Date | null;
  completedAt: Date | null;
  providerOwnershipMode: string | null;
  providerConfigId: string | null;
  platformFallbackUsed: boolean;
  createdAt?: Date;
  updatedAt?: Date;
}

interface NotificationAttemptCreationAttributes
  extends Optional<
    NotificationAttemptAttributes,
    | "id"
    | "failoverTriggered"
    | "providerMessageId"
    | "failureCategory"
    | "errorMessage"
    | "startedAt"
    | "completedAt"
    | "providerOwnershipMode"
    | "providerConfigId"
    | "platformFallbackUsed"
  > {}

export class NotificationAttempt extends Model<
  NotificationAttemptAttributes,
  NotificationAttemptCreationAttributes
> {
  declare id: string;
  declare tenantId: string | null;
  declare notificationId: string;
  declare attemptNumber: number;
  declare provider: string;
  declare status: AttemptStatus;
  declare failoverTriggered: boolean;
  declare providerMessageId: string | null;
  declare failureCategory: FailureCategory | null;
  declare errorMessage: string | null;
  declare startedAt: Date | null;
  declare completedAt: Date | null;
  declare providerOwnershipMode: string | null;
  declare providerConfigId: string | null;
  declare platformFallbackUsed: boolean;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initNotificationAttemptModel(sequelize: Sequelize): void {
  NotificationAttempt.init(
    {
      id: {
        type: DataTypes.UUID,
        defaultValue: DataTypes.UUIDV4,
        primaryKey: true,
      },
      tenantId: {
        type: DataTypes.UUID,
        allowNull: false,
        field: "tenant_id",
      },
      notificationId: {
        type: DataTypes.UUID,
        allowNull: false,
        field: "notification_id",
      },
      attemptNumber: {
        type: DataTypes.INTEGER,
        allowNull: false,
        defaultValue: 1,
        field: "attempt_number",
      },
      provider: {
        type: DataTypes.STRING(100),
        allowNull: false,
      },
      status: {
        type: DataTypes.ENUM("created", "sending", "sent", "failed"),
        allowNull: false,
        defaultValue: "created",
      },
      failoverTriggered: {
        type: DataTypes.BOOLEAN,
        allowNull: false,
        defaultValue: false,
        field: "failover_triggered",
      },
      providerMessageId: {
        type: DataTypes.STRING(255),
        allowNull: true,
        defaultValue: null,
        field: "provider_message_id",
      },
      failureCategory: {
        type: DataTypes.STRING(100),
        allowNull: true,
        defaultValue: null,
        field: "failure_category",
      },
      errorMessage: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "error_message",
      },
      startedAt: {
        type: DataTypes.DATE,
        allowNull: true,
        defaultValue: null,
        field: "started_at",
      },
      completedAt: {
        type: DataTypes.DATE,
        allowNull: true,
        defaultValue: null,
        field: "completed_at",
      },
      providerOwnershipMode: {
        type: DataTypes.STRING(50),
        allowNull: true,
        defaultValue: null,
        field: "provider_ownership_mode",
      },
      providerConfigId: {
        type: DataTypes.UUID,
        allowNull: true,
        defaultValue: null,
        field: "provider_config_id",
      },
      platformFallbackUsed: {
        type: DataTypes.BOOLEAN,
        allowNull: false,
        defaultValue: false,
        field: "platform_fallback_used",
      },
    },
    {
      sequelize,
      tableName: "notification_attempts",
      timestamps: true,
      underscored: true,
    }
  );
}
