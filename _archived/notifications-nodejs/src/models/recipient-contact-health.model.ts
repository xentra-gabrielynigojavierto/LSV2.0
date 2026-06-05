import { DataTypes, Model, Sequelize, Optional } from "sequelize";
import { NotificationChannel } from "../types";

export type ContactHealthStatus =
  | "valid"
  | "bounced"
  | "complained"
  | "unsubscribed"
  | "suppressed"
  | "invalid"
  | "unreachable"
  | "carrier_rejected"
  | "opted_out";

interface RecipientContactHealthAttributes {
  id: string;
  tenantId: string;
  channel: NotificationChannel;
  contactValue: string;
  healthStatus: ContactHealthStatus;
  lastFailureCategory: string | null;
  lastEventType: string | null;
  lastEventAt: Date | null;
  failureCount: number;
  createdAt?: Date;
  updatedAt?: Date;
}

interface RecipientContactHealthCreationAttributes
  extends Optional<
    RecipientContactHealthAttributes,
    "id" | "lastFailureCategory" | "lastEventType" | "lastEventAt" | "failureCount"
  > {}

export class RecipientContactHealth extends Model<
  RecipientContactHealthAttributes,
  RecipientContactHealthCreationAttributes
> {
  declare id: string;
  declare tenantId: string;
  declare channel: NotificationChannel;
  declare contactValue: string;
  declare healthStatus: ContactHealthStatus;
  declare lastFailureCategory: string | null;
  declare lastEventType: string | null;
  declare lastEventAt: Date | null;
  declare failureCount: number;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initRecipientContactHealthModel(sequelize: Sequelize): void {
  RecipientContactHealth.init(
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
      contactValue: {
        type: DataTypes.STRING(512),
        allowNull: false,
        field: "contact_value",
      },
      healthStatus: {
        type: DataTypes.ENUM(
          "valid",
          "bounced",
          "complained",
          "unsubscribed",
          "suppressed",
          "invalid",
          "unreachable",
          "carrier_rejected",
          "opted_out"
        ),
        allowNull: false,
        defaultValue: "valid",
        field: "health_status",
      },
      lastFailureCategory: {
        type: DataTypes.STRING(100),
        allowNull: true,
        defaultValue: null,
        field: "last_failure_category",
      },
      lastEventType: {
        type: DataTypes.STRING(100),
        allowNull: true,
        defaultValue: null,
        field: "last_event_type",
      },
      lastEventAt: {
        type: DataTypes.DATE,
        allowNull: true,
        defaultValue: null,
        field: "last_event_at",
      },
      failureCount: {
        type: DataTypes.INTEGER,
        allowNull: false,
        defaultValue: 0,
        field: "failure_count",
      },
    },
    {
      sequelize,
      tableName: "recipient_contact_health",
      timestamps: true,
      underscored: true,
      indexes: [
        {
          unique: true,
          fields: ["tenant_id", "channel", "contact_value"],
          name: "uq_recipient_contact_health",
        },
      ],
    }
  );
}
