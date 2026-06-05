import { DataTypes, Model, Sequelize, Optional } from "sequelize";
import { NotificationChannel, FailureCategory } from "../types";

export type NotificationStatus = "accepted" | "processing" | "sent" | "failed" | "blocked";

interface NotificationAttributes {
  id: string;
  tenantId: string | null;
  channel: NotificationChannel;
  status: NotificationStatus;
  recipientJson: string;
  messageJson: string;
  metadataJson: string | null;
  idempotencyKey: string | null;
  providerUsed: string | null;
  failureCategory: FailureCategory | null;
  lastErrorMessage: string | null;
  templateId: string | null;
  templateVersionId: string | null;
  templateKey: string | null;
  renderedSubject: string | null;
  renderedBody: string | null;
  renderedText: string | null;
  providerOwnershipMode: string | null;
  providerConfigId: string | null;
  platformFallbackUsed: boolean;
  blockedByPolicy: boolean;
  blockedReasonCode: string | null;
  overrideUsed: boolean;
  createdAt?: Date;
  updatedAt?: Date;
}

interface NotificationCreationAttributes
  extends Optional<
    NotificationAttributes,
    | "id"
    | "metadataJson"
    | "idempotencyKey"
    | "providerUsed"
    | "failureCategory"
    | "lastErrorMessage"
    | "templateId"
    | "templateVersionId"
    | "templateKey"
    | "renderedSubject"
    | "renderedBody"
    | "renderedText"
    | "providerOwnershipMode"
    | "providerConfigId"
    | "platformFallbackUsed"
    | "blockedByPolicy"
    | "blockedReasonCode"
    | "overrideUsed"
  > {}

export class Notification extends Model<NotificationAttributes, NotificationCreationAttributes> {
  declare id: string;
  declare tenantId: string | null;
  declare channel: NotificationChannel;
  declare status: NotificationStatus;
  declare recipientJson: string;
  declare messageJson: string;
  declare metadataJson: string | null;
  declare idempotencyKey: string | null;
  declare providerUsed: string | null;
  declare failureCategory: FailureCategory | null;
  declare lastErrorMessage: string | null;
  declare templateId: string | null;
  declare templateVersionId: string | null;
  declare templateKey: string | null;
  declare renderedSubject: string | null;
  declare renderedBody: string | null;
  declare renderedText: string | null;
  declare providerOwnershipMode: string | null;
  declare providerConfigId: string | null;
  declare platformFallbackUsed: boolean;
  declare blockedByPolicy: boolean;
  declare blockedReasonCode: string | null;
  declare overrideUsed: boolean;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initNotificationModel(sequelize: Sequelize): void {
  Notification.init(
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
      channel: {
        type: DataTypes.ENUM("email", "sms", "push", "in-app"),
        allowNull: false,
      },
      status: {
        type: DataTypes.ENUM("accepted", "processing", "sent", "failed", "blocked"),
        allowNull: false,
        defaultValue: "accepted",
      },
      recipientJson: {
        type: DataTypes.TEXT,
        allowNull: false,
        field: "recipient_json",
      },
      messageJson: {
        type: DataTypes.TEXT,
        allowNull: false,
        field: "message_json",
      },
      metadataJson: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "metadata_json",
      },
      idempotencyKey: {
        type: DataTypes.STRING(255),
        allowNull: true,
        defaultValue: null,
        field: "idempotency_key",
      },
      providerUsed: {
        type: DataTypes.STRING(100),
        allowNull: true,
        defaultValue: null,
        field: "provider_used",
      },
      failureCategory: {
        type: DataTypes.STRING(100),
        allowNull: true,
        defaultValue: null,
        field: "failure_category",
      },
      lastErrorMessage: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "last_error_message",
      },
      templateId: {
        type: DataTypes.UUID,
        allowNull: true,
        defaultValue: null,
        field: "template_id",
      },
      templateVersionId: {
        type: DataTypes.UUID,
        allowNull: true,
        defaultValue: null,
        field: "template_version_id",
      },
      templateKey: {
        type: DataTypes.STRING(200),
        allowNull: true,
        defaultValue: null,
        field: "template_key",
      },
      renderedSubject: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "rendered_subject",
      },
      renderedBody: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "rendered_body",
      },
      renderedText: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "rendered_text",
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
      blockedByPolicy: {
        type: DataTypes.BOOLEAN,
        allowNull: false,
        defaultValue: false,
        field: "blocked_by_policy",
      },
      blockedReasonCode: {
        type: DataTypes.STRING(100),
        allowNull: true,
        defaultValue: null,
        field: "blocked_reason_code",
      },
      overrideUsed: {
        type: DataTypes.BOOLEAN,
        allowNull: false,
        defaultValue: false,
        field: "override_used",
      },
    },
    {
      sequelize,
      tableName: "notifications",
      timestamps: true,
      underscored: true,
      indexes: [
        {
          unique: true,
          fields: ["tenant_id", "idempotency_key"],
          name: "uq_notifications_tenant_idempotency",
          where: { idempotency_key: { [Symbol.for("ne")]: null } },
        },
      ],
    }
  );
}
