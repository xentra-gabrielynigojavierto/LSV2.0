import { DataTypes, Model, Sequelize, Optional } from "sequelize";

interface TenantContactPolicyAttributes {
  id: string;
  tenantId: string;
  channel: string | null;
  blockSuppressedContacts: boolean;
  blockUnsubscribedContacts: boolean;
  blockComplainedContacts: boolean;
  blockBouncedContacts: boolean;
  blockInvalidContacts: boolean;
  blockCarrierRejectedContacts: boolean;
  allowManualOverride: boolean;
  status: "active" | "inactive";
  createdAt?: Date;
  updatedAt?: Date;
}

interface TenantContactPolicyCreationAttributes
  extends Optional<
    TenantContactPolicyAttributes,
    | "id"
    | "channel"
    | "blockSuppressedContacts"
    | "blockUnsubscribedContacts"
    | "blockComplainedContacts"
    | "blockBouncedContacts"
    | "blockInvalidContacts"
    | "blockCarrierRejectedContacts"
    | "allowManualOverride"
    | "status"
  > {}

export class TenantContactPolicy extends Model<
  TenantContactPolicyAttributes,
  TenantContactPolicyCreationAttributes
> {
  declare id: string;
  declare tenantId: string;
  declare channel: string | null;
  declare blockSuppressedContacts: boolean;
  declare blockUnsubscribedContacts: boolean;
  declare blockComplainedContacts: boolean;
  declare blockBouncedContacts: boolean;
  declare blockInvalidContacts: boolean;
  declare blockCarrierRejectedContacts: boolean;
  declare allowManualOverride: boolean;
  declare status: "active" | "inactive";
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initTenantContactPolicyModel(sequelize: Sequelize): void {
  TenantContactPolicy.init(
    {
      id: { type: DataTypes.UUID, defaultValue: DataTypes.UUIDV4, primaryKey: true },
      tenantId: { type: DataTypes.UUID, allowNull: false, field: "tenant_id" },
      channel: { type: DataTypes.STRING(50), allowNull: true, defaultValue: null },
      blockSuppressedContacts: { type: DataTypes.BOOLEAN, allowNull: false, defaultValue: true, field: "block_suppressed_contacts" },
      blockUnsubscribedContacts: { type: DataTypes.BOOLEAN, allowNull: false, defaultValue: true, field: "block_unsubscribed_contacts" },
      blockComplainedContacts: { type: DataTypes.BOOLEAN, allowNull: false, defaultValue: true, field: "block_complained_contacts" },
      blockBouncedContacts: { type: DataTypes.BOOLEAN, allowNull: false, defaultValue: false, field: "block_bounced_contacts" },
      blockInvalidContacts: { type: DataTypes.BOOLEAN, allowNull: false, defaultValue: false, field: "block_invalid_contacts" },
      blockCarrierRejectedContacts: { type: DataTypes.BOOLEAN, allowNull: false, defaultValue: false, field: "block_carrier_rejected_contacts" },
      allowManualOverride: { type: DataTypes.BOOLEAN, allowNull: false, defaultValue: false, field: "allow_manual_override" },
      status: {
        type: DataTypes.ENUM("active", "inactive"),
        allowNull: false,
        defaultValue: "active",
      },
    },
    {
      sequelize,
      tableName: "tenant_contact_policies",
      timestamps: true,
      underscored: true,
      indexes: [
        { fields: ["tenant_id"] },
        { fields: ["tenant_id", "channel"] },
        { fields: ["tenant_id", "status"] },
      ],
    }
  );
}
