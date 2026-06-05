import { DataTypes, Model, Sequelize, Optional } from "sequelize";
import { SuppressionType, SuppressionSource, SuppressionStatus } from "../types";

interface ContactSuppressionAttributes {
  id: string;
  tenantId: string;
  channel: string;
  contactValue: string;
  suppressionType: SuppressionType;
  reason: string;
  source: SuppressionSource;
  status: SuppressionStatus;
  expiresAt: Date | null;
  createdBy: string | null;
  notes: string | null;
  createdAt?: Date;
  updatedAt?: Date;
}

interface ContactSuppressionCreationAttributes
  extends Optional<
    ContactSuppressionAttributes,
    "id" | "expiresAt" | "createdBy" | "notes"
  > {}

export class ContactSuppression extends Model<
  ContactSuppressionAttributes,
  ContactSuppressionCreationAttributes
> {
  declare id: string;
  declare tenantId: string;
  declare channel: string;
  declare contactValue: string;
  declare suppressionType: SuppressionType;
  declare reason: string;
  declare source: SuppressionSource;
  declare status: SuppressionStatus;
  declare expiresAt: Date | null;
  declare createdBy: string | null;
  declare notes: string | null;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initContactSuppressionModel(sequelize: Sequelize): void {
  ContactSuppression.init(
    {
      id: { type: DataTypes.UUID, defaultValue: DataTypes.UUIDV4, primaryKey: true },
      tenantId: { type: DataTypes.UUID, allowNull: false, field: "tenant_id" },
      channel: { type: DataTypes.STRING(50), allowNull: false },
      contactValue: { type: DataTypes.STRING(512), allowNull: false, field: "contact_value" },
      suppressionType: {
        type: DataTypes.ENUM(
          "manual",
          "bounce",
          "unsubscribe",
          "complaint",
          "invalid_contact",
          "carrier_rejection",
          "system_protection"
        ),
        allowNull: false,
        field: "suppression_type",
      },
      reason: { type: DataTypes.STRING(500), allowNull: false, defaultValue: "" },
      source: {
        type: DataTypes.ENUM("provider_webhook", "manual_admin", "system_rule", "import"),
        allowNull: false,
        defaultValue: "manual_admin",
      },
      status: {
        type: DataTypes.ENUM("active", "expired", "lifted"),
        allowNull: false,
        defaultValue: "active",
      },
      expiresAt: { type: DataTypes.DATE, allowNull: true, defaultValue: null, field: "expires_at" },
      createdBy: { type: DataTypes.STRING(255), allowNull: true, defaultValue: null, field: "created_by" },
      notes: { type: DataTypes.TEXT, allowNull: true, defaultValue: null },
    },
    {
      sequelize,
      tableName: "contact_suppressions",
      timestamps: true,
      underscored: true,
      indexes: [
        { fields: ["tenant_id"] },
        { fields: ["tenant_id", "channel", "contact_value"] },
        { fields: ["tenant_id", "status"] },
        { fields: ["tenant_id", "channel", "contact_value", "suppression_type"], name: "idx_suppression_lookup" },
      ],
    }
  );
}
