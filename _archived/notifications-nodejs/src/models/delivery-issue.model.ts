import { DataTypes, Model, Sequelize, Optional } from "sequelize";
import { NotificationChannel } from "../types";

export type DeliveryIssueType =
  | "invalid_email"
  | "bounced_email"
  | "invalid_phone"
  | "sms_undelivered"
  | "provider_rejected"
  | "repeated_temporary_failure"
  | "unsubscribed_recipient"
  | "complained_recipient"
  | "opted_out_recipient";

export type DeliveryIssueStatus = "open" | "resolved" | "ignored";

interface DeliveryIssueAttributes {
  id: string;
  tenantId: string;
  notificationId: string;
  notificationAttemptId: string | null;
  channel: NotificationChannel;
  provider: string;
  issueType: DeliveryIssueType;
  status: DeliveryIssueStatus;
  recommendedAction: string | null;
  detailsJson: string | null;
  createdAt?: Date;
  updatedAt?: Date;
}

interface DeliveryIssueCreationAttributes
  extends Optional<
    DeliveryIssueAttributes,
    "id" | "notificationAttemptId" | "status" | "recommendedAction" | "detailsJson"
  > {}

export class DeliveryIssue extends Model<
  DeliveryIssueAttributes,
  DeliveryIssueCreationAttributes
> {
  declare id: string;
  declare tenantId: string;
  declare notificationId: string;
  declare notificationAttemptId: string | null;
  declare channel: NotificationChannel;
  declare provider: string;
  declare issueType: DeliveryIssueType;
  declare status: DeliveryIssueStatus;
  declare recommendedAction: string | null;
  declare detailsJson: string | null;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initDeliveryIssueModel(sequelize: Sequelize): void {
  DeliveryIssue.init(
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
      notificationAttemptId: {
        type: DataTypes.UUID,
        allowNull: true,
        defaultValue: null,
        field: "notification_attempt_id",
      },
      channel: {
        type: DataTypes.ENUM("email", "sms", "push", "in-app"),
        allowNull: false,
      },
      provider: {
        type: DataTypes.STRING(100),
        allowNull: false,
      },
      issueType: {
        type: DataTypes.ENUM(
          "invalid_email",
          "bounced_email",
          "invalid_phone",
          "sms_undelivered",
          "provider_rejected",
          "repeated_temporary_failure",
          "unsubscribed_recipient",
          "complained_recipient",
          "opted_out_recipient"
        ),
        allowNull: false,
        field: "issue_type",
      },
      status: {
        type: DataTypes.ENUM("open", "resolved", "ignored"),
        allowNull: false,
        defaultValue: "open",
      },
      recommendedAction: {
        type: DataTypes.STRING(500),
        allowNull: true,
        defaultValue: null,
        field: "recommended_action",
      },
      detailsJson: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "details_json",
      },
    },
    {
      sequelize,
      tableName: "delivery_issues",
      timestamps: true,
      underscored: true,
    }
  );
}
