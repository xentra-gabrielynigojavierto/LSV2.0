import { DataTypes, Model, Sequelize, Optional } from "sequelize";

export type NormalizedEventType =
  | "accepted"
  | "queued"
  | "sent"
  | "delivered"
  | "failed"
  | "undeliverable"
  | "bounced"
  | "opened"
  | "clicked"
  | "complained"
  | "unsubscribed"
  | "rejected"
  | "deferred";

interface NotificationEventAttributes {
  id: string;
  tenantId: string | null;
  notificationId: string | null;
  notificationAttemptId: string | null;
  provider: string;
  channel: string | null;
  rawEventType: string;
  normalizedEventType: NormalizedEventType;
  eventTimestamp: Date;
  providerMessageId: string | null;
  metadataJson: string | null;
  dedupKey: string | null;
  createdAt?: Date;
  updatedAt?: Date;
}

interface NotificationEventCreationAttributes
  extends Optional<
    NotificationEventAttributes,
    | "id"
    | "tenantId"
    | "notificationId"
    | "notificationAttemptId"
    | "channel"
    | "providerMessageId"
    | "metadataJson"
    | "dedupKey"
  > {}

export class NotificationEvent extends Model<
  NotificationEventAttributes,
  NotificationEventCreationAttributes
> {
  declare id: string;
  declare tenantId: string | null;
  declare notificationId: string | null;
  declare notificationAttemptId: string | null;
  declare provider: string;
  declare channel: string | null;
  declare rawEventType: string;
  declare normalizedEventType: NormalizedEventType;
  declare eventTimestamp: Date;
  declare providerMessageId: string | null;
  declare metadataJson: string | null;
  declare dedupKey: string | null;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initNotificationEventModel(sequelize: Sequelize): void {
  NotificationEvent.init(
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
      notificationId: {
        type: DataTypes.UUID,
        allowNull: true,
        defaultValue: null,
        field: "notification_id",
      },
      notificationAttemptId: {
        type: DataTypes.UUID,
        allowNull: true,
        defaultValue: null,
        field: "notification_attempt_id",
      },
      provider: {
        type: DataTypes.STRING(100),
        allowNull: false,
      },
      channel: {
        type: DataTypes.STRING(50),
        allowNull: true,
        defaultValue: null,
      },
      rawEventType: {
        type: DataTypes.STRING(100),
        allowNull: false,
        field: "raw_event_type",
      },
      normalizedEventType: {
        type: DataTypes.ENUM(
          "accepted",
          "queued",
          "sent",
          "delivered",
          "failed",
          "undeliverable",
          "bounced",
          "opened",
          "clicked",
          "complained",
          "unsubscribed",
          "rejected",
          "deferred"
        ),
        allowNull: false,
        field: "normalized_event_type",
      },
      eventTimestamp: {
        type: DataTypes.DATE,
        allowNull: false,
        field: "event_timestamp",
      },
      providerMessageId: {
        type: DataTypes.STRING(255),
        allowNull: true,
        defaultValue: null,
        field: "provider_message_id",
      },
      metadataJson: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "metadata_json",
      },
      dedupKey: {
        type: DataTypes.STRING(500),
        allowNull: true,
        defaultValue: null,
        field: "dedup_key",
      },
    },
    {
      sequelize,
      tableName: "notification_events",
      timestamps: true,
      underscored: true,
      indexes: [
        {
          unique: true,
          fields: ["dedup_key"],
          name: "uq_notification_events_dedup_key",
          where: { dedup_key: { [Symbol.for("ne")]: null } },
        },
      ],
    }
  );
}
