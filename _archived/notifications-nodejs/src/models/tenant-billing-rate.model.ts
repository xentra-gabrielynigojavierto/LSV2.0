import { DataTypes, Model, Sequelize, Optional } from "sequelize";
import { UsageUnit } from "../types";

interface TenantBillingRateAttributes {
  id: string;
  tenantBillingPlanId: string;
  usageUnit: UsageUnit;
  channel: string | null;
  providerOwnershipMode: string | null;
  includedQuantity: number | null;
  unitPrice: number | null;
  isBillable: boolean;
  createdAt?: Date;
  updatedAt?: Date;
}

interface TenantBillingRateCreationAttributes
  extends Optional<
    TenantBillingRateAttributes,
    "id" | "channel" | "providerOwnershipMode" | "includedQuantity" | "unitPrice"
  > {}

export class TenantBillingRate extends Model<
  TenantBillingRateAttributes,
  TenantBillingRateCreationAttributes
> {
  declare id: string;
  declare tenantBillingPlanId: string;
  declare usageUnit: UsageUnit;
  declare channel: string | null;
  declare providerOwnershipMode: string | null;
  declare includedQuantity: number | null;
  declare unitPrice: number | null;
  declare isBillable: boolean;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initTenantBillingRateModel(sequelize: Sequelize): void {
  TenantBillingRate.init(
    {
      id: { type: DataTypes.UUID, defaultValue: DataTypes.UUIDV4, primaryKey: true },
      tenantBillingPlanId: { type: DataTypes.UUID, allowNull: false, field: "tenant_billing_plan_id" },
      usageUnit: { type: DataTypes.STRING(100), allowNull: false, field: "usage_unit" },
      channel: { type: DataTypes.STRING(50), allowNull: true, defaultValue: null },
      providerOwnershipMode: { type: DataTypes.STRING(50), allowNull: true, defaultValue: null, field: "provider_ownership_mode" },
      includedQuantity: { type: DataTypes.INTEGER, allowNull: true, defaultValue: null, field: "included_quantity" },
      unitPrice: { type: DataTypes.DECIMAL(14, 8), allowNull: true, defaultValue: null, field: "unit_price" },
      isBillable: { type: DataTypes.BOOLEAN, allowNull: false, defaultValue: true, field: "is_billable" },
    },
    {
      sequelize,
      tableName: "tenant_billing_rates",
      timestamps: true,
      underscored: true,
      indexes: [
        { fields: ["tenant_billing_plan_id"] },
        { fields: ["tenant_billing_plan_id", "usage_unit"] },
      ],
    }
  );
}
